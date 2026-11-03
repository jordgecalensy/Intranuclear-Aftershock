using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ограниченный направленный поиск по точкам приземления. Между прыжками проверяет пешие пути,
/// а перед выдачей результата восстанавливает всю цепочку до конечной цели.
/// </summary>
public sealed class SpiderJumpPlanner
{
    public enum PlanFailureReason
    {
        None, InvalidAgent, NoWalkableAreas, StartOrTargetOutsideNavMesh,
        NoLandingCandidate, JumpOutsideLimits, ApproachDisconnected,
        RemainingRouteDisconnected, NotAShortcut, LandingBlocked,
        TrajectoryBlocked, NotEnoughTimeSaved,
        StartOutsideNavMesh, TargetOutsideNavMesh, SearchBudgetExceeded
    }

    public struct Settings
    {
        public float MoveSpeed;
        public float MinHorizontalDistance;
        public float MaxHorizontalDistance;
        public float MaxRise;
        public float MaxDrop;
        public float JumpSpeed;
        public float BaseJumpDuration;
        public float JumpApexHeight;
        public float DropApexHeight;
        public float PreparationDuration;
        public float LandingRecoveryDuration;
        public float MinimumTimeSaving;
        public float MinimumRelativeSaving;
        public float EdgeSearchRadius;
        public float LandingInset;
        public float NavMeshSampleRadius;
        public float MinimumVerticalShortcut;
        public float MinimumWalkDetourRatio;
        public int EdgeRingSamples;
        public float LandingSampleStep;
        public int MaximumCandidateChecks;
        public int CollisionMask;
        public int TrajectorySegments;
        public float CollisionSkin;
        public int MaximumJumps;
        public int MaximumSearchNodes;
        public int MaximumExpandedNodes;
        public float TargetGroundProbeDistance;
        public float LongJumpPenalty;
    }

    public readonly struct Route
    {
        public readonly Vector3 TakeoffPosition;
        public readonly Vector3 LandingPosition;
        // Исходная цель, а не её проекция: высота маркера не должна отменять маршрут.
        public readonly Vector3 PlannedTargetPosition;
        public readonly float NormalTravelTime;
        public readonly float JumpTravelTime;
        public readonly float FlightDuration;
        public readonly float ApexHeight;
        public float TimeSaving => NormalTravelTime - JumpTravelTime;

        public Route(Vector3 takeoffPosition, Vector3 landingPosition, Vector3 plannedTargetPosition,
            float normalTravelTime, float jumpTravelTime, float flightDuration, float apexHeight)
        {
            TakeoffPosition = takeoffPosition;
            LandingPosition = landingPosition;
            PlannedTargetPosition = plannedTargetPosition;
            NormalTravelTime = normalTravelTime;
            JumpTravelTime = jumpTravelTime;
            FlightDuration = flightDuration;
            ApexHeight = apexHeight;
        }
    }

    private sealed class SearchNode
    {
        public Vector3 Position;
        public int Parent;
        public int JumpCount;
        public float Cost;
        public float TravelTime;
        public bool Expanded;
        public bool HasVerticalJump;
        public Route IncomingJump;
        public IEnumerator Expansion;
        public int WorkBatches;
        public readonly HashSet<(Vector3, Vector3)> CheckedPairs = new HashSet<(Vector3, Vector3)>();
    }

    private readonly struct EdgeCandidate
    {
        public readonly Vector3 Position;
        public readonly Vector3 Outward;
        public EdgeCandidate(Vector3 position, Vector3 outward)
        {
            Position = position;
            Outward = outward;
        }
    }

    private const float MinimumPathCornerDistance = 0.001f;
    private const float EdgeDuplicateDistance = 0.75f;
    private const float FloorNormalThreshold = 0.55f;
    private const float MaximumFloorToNavMeshHeight = 0.65f;
    private const int ProbesPerFrame = 12;
    private readonly NavMeshPath _normalPath = new NavMeshPath();
    private readonly NavMeshPath _approachPath = new NavMeshPath();
    private readonly NavMeshPath _remainingPath = new NavMeshPath();
    private readonly NavMeshPath _betweenJumpPath = new NavMeshPath();
    private readonly List<EdgeCandidate> _edgeCandidates = new List<EdgeCandidate>(64);
    private readonly List<Vector3> _floorCandidates = new List<Vector3>(32);
    private readonly List<SearchNode> _nodes = new List<SearchNode>(256);
    private readonly List<Route> _plannedJumps = new List<Route>(8);
    private readonly RaycastHit[] _raycastHits = new RaycastHit[64];
    private readonly Collider[] _overlapHits = new Collider[32];

    private NavMeshAgent _agent;
    private Transform _ownerRoot;
    private Transform _targetRoot;
    private Vector3 _requestedTarget;
    private Settings _settings;
    private NavMeshQueryFilter _walkFilter;
    private float _normalTravelTime;
    private float _bestTravelTime;
    private float _bestRouteCost;
    private int _bestNode;
    private bool _budgetExceeded;
    private bool _hadCandidate;
    private bool _hadDistanceCandidate;
    private bool _hadApproachCandidate;
    private bool _hadRemainingRouteCandidate;
    private bool _hadTopologyCandidate;
    private bool _hadClearLandingCandidate;
    private bool _hadClearTrajectoryCandidate;
    private int _solutionRefinementBatches;
    private const int MaximumSolutionRefinementBatches = 48;

    public PlanFailureReason LastFailureReason { get; private set; }
    public Vector3 LastCandidateTakeoff { get; private set; }
    public Vector3 LastCandidateLanding { get; private set; }
    public int LastCandidateChecks { get; private set; }
    public int LastExpandedNodes { get; private set; }
    public Vector3 ResolvedTargetPosition { get; private set; }
    public bool HasResolvedTarget { get; private set; }
    public IReadOnlyList<Route> PlannedJumps => _plannedJumps;

    // Синхронный вход для инструментов и тестов. В игре используется Plan с yield.
    public bool TryPlan(NavMeshAgent agent, Transform ownerRoot, Transform targetRoot,
        Vector3 requestedTarget, Settings settings, out Route bestRoute)
    {
        IEnumerator search = Plan(agent, ownerRoot, targetRoot, requestedTarget, settings);
        while (search.MoveNext()) { }
        bestRoute = _plannedJumps.Count > 0 ? _plannedJumps[0] : default;
        return _plannedJumps.Count > 0;
    }

    public IEnumerator Plan(NavMeshAgent agent, Transform ownerRoot, Transform targetRoot,
        Vector3 requestedTarget, Settings settings)
    {
        ResetDiagnostics();
        _nodes.Clear();
        _plannedJumps.Clear();
        HasResolvedTarget = false;
        LastExpandedNodes = 0;
        _bestNode = -1;
        _budgetExceeded = false;
        _solutionRefinementBatches = 0;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            LastFailureReason = PlanFailureReason.InvalidAgent;
            yield break;
        }

        _agent = agent;
        _ownerRoot = ownerRoot;
        _targetRoot = targetRoot;
        _requestedTarget = requestedTarget;
        _settings = Sanitize(settings);
        _walkFilter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = GetWalkOnlyAreaMask(agent.areaMask)
        };
        if (_walkFilter.areaMask == 0)
        {
            LastFailureReason = PlanFailureReason.NoWalkableAreas;
            yield break;
        }

        Vector3 start = agent.nextPosition - Vector3.up * agent.baseOffset;
        if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, _settings.NavMeshSampleRadius, _walkFilter))
        {
            LastFailureReason = PlanFailureReason.StartOutsideNavMesh;
            yield break;
        }
        if (!TryResolveTarget(requestedTarget, _walkFilter, ownerRoot, targetRoot, _settings, out Vector3 target))
        {
            LastFailureReason = PlanFailureReason.TargetOutsideNavMesh;
            yield break;
        }
        HasResolvedTarget = true;
        ResolvedTargetPosition = target;
        _normalTravelTime = TryGetCompletePathLength(startHit.position, target, _walkFilter, _normalPath, out float length)
            ? length / _settings.MoveSpeed : float.PositiveInfinity;
        _bestTravelTime = _normalTravelTime;
        _bestRouteCost = _normalTravelTime;
        _nodes.Add(new SearchNode { Position = startHit.position, Parent = -1 });

        while (TrySelectNextNode(out int nodeIndex))
        {
            SearchNode node = _nodes[nodeIndex];
            if (node.Expansion == null)
            {
                if (LastExpandedNodes >= _settings.MaximumExpandedNodes || node.JumpCount >= _settings.MaximumJumps)
                {
                    node.Expanded = true;
                    _budgetExceeded = true;
                    continue;
                }
                LastExpandedNodes++;
                node.Expansion = ExpandNode(nodeIndex);
            }
            node.Expanded = !node.Expansion.MoveNext();
            node.WorkBatches++;
            // Выдаём полную найденную цепочку после короткого уточнения стоимости.
            // Иначе улучшение уже готового маршрута может занимать минуты по кадрам.
            if (_bestNode >= 0 && ++_solutionRefinementBatches >= MaximumSolutionRefinementBatches)
                break;
            yield return null;
        }

        if (_bestNode >= 0)
        {
            for (int index = _bestNode; _nodes[index].Parent >= 0; index = _nodes[index].Parent)
            {
                Route jump = _nodes[index].IncomingJump;
                _plannedJumps.Add(new Route(jump.TakeoffPosition, jump.LandingPosition, requestedTarget,
                    _normalTravelTime, _bestTravelTime, jump.FlightDuration, jump.ApexHeight));
            }
            _plannedJumps.Reverse();
            LastFailureReason = PlanFailureReason.None;
        }
        else
        {
            LastFailureReason = _budgetExceeded ? PlanFailureReason.SearchBudgetExceeded
                : ResolveFailureReason();
        }
    }

    private bool TrySelectNextNode(out int selected)
    {
        selected = -1;
        float bestScore = float.PositiveInfinity;
        for (int index = 0; index < _nodes.Count; index++)
        {
            SearchNode node = _nodes[index];
            if (node.Expanded)
                continue;
            float lowerBound = Vector3.Distance(node.Position, ResolvedTargetPosition) /
                Mathf.Max(_settings.MoveSpeed, _settings.JumpSpeed);
            float score = node.Cost + lowerBound;
            if (score >= _bestRouteCost)
                continue;
            // Не исчерпываем тысячи проб одной площадки до первого продолжения.
            // Старые узлы остаются в очереди и возвращаются к поиску позже.
            Vector3 toTarget = ResolvedTargetPosition - node.Position;
            float verticalDistance = Mathf.Abs(toTarget.y);
            toTarget.y = 0f;
            // Направленный поиск: подъём на следующий этаж важнее множества
            // дешёвых переходов по нижнему полу. Нижняя оценка выше всё ещё
            // используется отдельно для отсечения по цене готового маршрута.
            score = node.Cost + 2f * (toTarget.magnitude + verticalDistance * 2f) / _settings.MoveSpeed;
            score += node.WorkBatches * 0.1f;
            if (score < bestScore)
            {
                selected = index;
                bestScore = score;
            }
        }
        return selected >= 0;
    }

    private IEnumerator ExpandNode(int nodeIndex)
    {
        SearchNode node = _nodes[nodeIndex];
        _edgeCandidates.Clear();
        CollectEdgesNear(node.Position, ResolvedTargetPosition, _walkFilter, _settings);
        CollectEdgesNear(ResolvedTargetPosition, node.Position, _walkFilter, _settings);
        NavMesh.CalculatePath(node.Position, ResolvedTargetPosition, _walkFilter, _remainingPath);
        Vector3[] corners = _remainingPath.corners;
        if (corners.Length > 1)
            CollectEdgesNear(corners[corners.Length - 1], ResolvedTargetPosition, _walkFilter, _settings);
        // У каждого отложенного раскрытия собственные данные, не общие буферы.
        EdgeCandidate[] edges = _edgeCandidates.ToArray();
        var insets = new Vector3[edges.Length];
        var validInsets = new bool[edges.Length];
        var reachableInsets = new bool[edges.Length];
        for (int index = 0; index < edges.Length; index++)
        {
            validInsets[index] = TryGetInsetNavMeshPoint(edges[index].Position, -edges[index].Outward,
                _walkFilter, _settings, out insets[index]);
            reachableInsets[index] = validInsets[index] && TryGetCompletePathLength(node.Position,
                insets[index], _walkFilter, _approachPath, out _);
        }
        yield return null;
        // Сначала соединяем уже обнаруженные края разных площадок. В узком
        // проходе это находит переход сразу, без прохода всех коротких проб
        // по нижним этажам, ещё не достигающих противоположной полки.
        int edgePairs = 0;
        for (int from = 0; from < edges.Length; from++)
        {
            if (!reachableInsets[from])
                continue;
            for (int to = 0; to < edges.Length; to++)
            {
                if (!validInsets[to] || reachableInsets[to])
                    continue;
                TryCandidate(nodeIndex, insets[from], insets[to]);
                if (++edgePairs % ProbesPerFrame == 0)
                    yield return null;
            }
        }
        float minProbe = CalculateProbeDistance(_settings.MinHorizontalDistance, _settings.LandingInset);
        float maxProbe = CalculateProbeDistance(_settings.MaxHorizontalDistance, _settings.LandingInset);
        int samples = CalculateDistanceSampleCount(minProbe, maxProbe, _settings.LandingSampleStep);
        int checksAtStart = LastCandidateChecks;
        int probes = 0;
        // Расстояние — внешний цикл: один край не расходует весь бюджет узла.
        for (int sample = 1; sample <= samples; sample++)
        {
            float distance = GetDistanceSample(minProbe, maxProbe, _settings.LandingSampleStep, sample);
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                EdgeCandidate edge = edges[edgeIndex];
                if (!TryGetInsetNavMeshPoint(edge.Position, -edge.Outward, _walkFilter, _settings, out Vector3 inset))
                    continue;
                if (sample == 1 && _targetRoot == null)
                    TryCandidate(nodeIndex, inset, ResolvedTargetPosition);

                for (int directionIndex = 0; directionIndex < 3; directionIndex++)
                {
                    float angle = directionIndex == 0 ? 0f : (directionIndex == 1 ? -30f : 30f);
                    Vector3 direction = Quaternion.Euler(0f, angle, 0f) * edge.Outward;
                    float verticalRange = Mathf.Max(_settings.MaxRise, _settings.MaxDrop);
                    CollectFloorPoints(edge.Position + direction * distance,
                        edge.Position.y - verticalRange, edge.Position.y + verticalRange,
                        _walkFilter, _ownerRoot, _targetRoot, _settings);
                    Vector3[] floors = _floorCandidates.ToArray();
                    for (int floorIndex = 0; floorIndex < floors.Length; floorIndex++)
                    {
                        Vector3 floor = floors[floorIndex];
                        if (Vector3.Dot(floor - edge.Position, edge.Outward) < _settings.LandingInset * 0.25f)
                            continue;
                        TryCandidate(nodeIndex, inset, floor);
                        TryCandidate(nodeIndex, floor, inset);
                        if (LastCandidateChecks - checksAtStart >= _settings.MaximumCandidateChecks)
                        {
                            // Порция проверок закончилась, но края этой площадки ещё не исчерпаны.
                            // Продолжаем в следующем кадре, иначе заблокированные близкие пары
                            // не дают добраться до выхода на промежуточную платформу.
                            checksAtStart = LastCandidateChecks;
                            yield return null;
                        }
                    }
                    if (++probes % ProbesPerFrame == 0)
                        yield return null;
                }
            }
        }
    }

    private void TryCandidate(int parentIndex, Vector3 takeoff, Vector3 landing)
    {
        if (!_nodes[parentIndex].CheckedPairs.Add((takeoff, landing)))
            return;
        _hadCandidate = true;
        LastCandidateTakeoff = takeoff;
        LastCandidateLanding = landing;
        Vector3 horizontal = landing - takeoff;
        horizontal.y = 0f;
        float verticalDelta = landing.y - takeoff.y;
        if (!IsWithinJumpLimits(horizontal.magnitude, verticalDelta, _settings.MinHorizontalDistance,
                _settings.MaxHorizontalDistance, _settings.MaxRise, _settings.MaxDrop))
            return;
        _hadDistanceCandidate = true;
        SearchNode parent = _nodes[parentIndex];
        if (!TryGetCompletePathLength(parent.Position, takeoff, _walkFilter, _approachPath, out float approachLength))
            return;
        _hadApproachCandidate = true;
        bool walkAcross = TryGetCompletePathLength(takeoff, landing, _walkFilter, _betweenJumpPath, out float acrossLength);
        if (!IsTopologyShortcut(verticalDelta, Vector3.Distance(takeoff, landing), walkAcross, acrossLength,
                _settings.MinimumVerticalShortcut, _settings.MinimumWalkDetourRatio))
            return;
        _hadTopologyCandidate = true;

        float duration = EstimateFlightDuration(takeoff, landing, _settings.JumpSpeed, _settings.BaseJumpDuration);
        float segmentTime = EstimateJumpRouteTime(approachLength, 0f, _settings.MoveSpeed,
            _settings.PreparationDuration, duration, _settings.LandingRecoveryDuration);
        float effort = CalculateJumpPenalty(horizontal.magnitude, verticalDelta, _settings.MaxHorizontalDistance,
            _settings.MaxRise, _settings.MaxDrop, _settings.LongJumpPenalty);
        float cost = parent.Cost + segmentTime + effort;
        if (cost >= _bestRouteCost || IsDominated(landing, cost, parent.TravelTime + segmentTime, parent.JumpCount + 1))
            return;
        LastCandidateChecks++;
        if (!LandingIsClear(landing, _agent, _ownerRoot, _settings))
            return;
        _hadClearLandingCandidate = true;
        float preferredApex = Mathf.Max(SelectApexHeight(verticalDelta, _settings.JumpApexHeight, _settings.DropApexHeight),
            CalculateMinimumDropApexHeight(verticalDelta, horizontal.magnitude, _settings.LandingInset,
                _agent.radius, _settings.CollisionSkin));
        if (!TryFindClearApexHeight(takeoff, landing, preferredApex, _agent, _ownerRoot, _targetRoot, _settings, out float apex))
            return;
        _hadClearTrajectoryCandidate = true;
        duration = EstimateFlightDuration(takeoff, landing, apex, _settings.JumpSpeed, _settings.BaseJumpDuration);
        segmentTime = EstimateJumpRouteTime(approachLength, 0f, _settings.MoveSpeed,
            _settings.PreparationDuration, duration, _settings.LandingRecoveryDuration);
        cost = parent.Cost + segmentTime + effort;
        float travelTime = parent.TravelTime + segmentTime;
        if (cost >= _bestRouteCost || IsDominated(landing, cost, travelTime, parent.JumpCount + 1))
            return;
        int replacement = FindPendingLanding(landing, cost, parent.JumpCount + 1);
        if (replacement < 0 && _nodes.Count >= _settings.MaximumSearchNodes)
        {
            _budgetExceeded = true;
            return;
        }

        var next = new SearchNode
        {
            Position = landing, Parent = parentIndex, JumpCount = parent.JumpCount + 1, Cost = cost,
            TravelTime = travelTime,
            HasVerticalJump = parent.HasVerticalJump || Mathf.Abs(verticalDelta) >= _settings.MinimumVerticalShortcut,
            IncomingJump = new Route(takeoff, landing, _requestedTarget, _normalTravelTime, cost, duration, apex)
        };
        int nextIndex = replacement >= 0 ? replacement : _nodes.Count;
        if (replacement >= 0)
            _nodes[nextIndex] = next;
        else
            _nodes.Add(next);
        if (!TryGetCompletePathLength(landing, ResolvedTargetPosition, _walkFilter, _remainingPath, out float remainingLength))
            return; // Площадка остаётся в очереди: отсюда можно прыгнуть дальше.
        _hadRemainingRouteCandidate = true;
        float totalTime = travelTime + remainingLength / _settings.MoveSpeed;
        float totalCost = cost + remainingLength / _settings.MoveSpeed;
        if (totalCost < _bestRouteCost && SavesEnoughTimeForRoute(_normalTravelTime, totalTime,
                _settings.MinimumTimeSaving, _settings.MinimumRelativeSaving, next.HasVerticalJump))
        {
            _bestNode = nextIndex;
            _bestTravelTime = totalTime;
            _bestRouteCost = totalCost;
        }
    }

    private bool IsDominated(Vector3 position, float cost, float travelTime, int jumpCount)
    {
        for (int index = 0; index < _nodes.Count; index++)
        {
            SearchNode existing = _nodes[index];
            // Не склеиваем разные этажи и противоположные стороны стены.
            if (existing.JumpCount > jumpCount || Mathf.Abs(existing.Position.y - position.y) > 0.25f ||
                (existing.Position - position).sqrMagnitude > 4f ||
                NavMesh.Raycast(existing.Position, position, out _, _walkFilter))
                continue;
            float walkingCost = Vector3.Distance(existing.Position, position) / _settings.MoveSpeed;
            if (existing.Cost + walkingCost <= cost + 0.001f &&
                existing.TravelTime + walkingCost <= travelTime + 0.001f)
                return true;
            // Близкие входы на одну открытую площадку не должны занимать всю
            // очередь раньше, чем поиск перейдёт на следующий этаж.
            if (existing.JumpCount > 0 &&
                LandingPriority(existing.Position, existing.Cost) <= LandingPriority(position, cost) + 0.25f)
                return true;
        }
        return false;
    }

    private float LandingPriority(Vector3 position, float cost)
    {
        return cost + Vector3.Distance(position, ResolvedTargetPosition) / _settings.MoveSpeed;
    }

    private int FindPendingLanding(Vector3 position, float cost, int jumpCount)
    {
        for (int index = 1; index < _nodes.Count; index++)
        {
            SearchNode existing = _nodes[index];
            if (index != _bestNode && existing.Expansion == null && !existing.Expanded &&
                existing.JumpCount >= jumpCount && Mathf.Abs(existing.Position.y - position.y) <= 0.25f &&
                (existing.Position - position).sqrMagnitude <= 4f &&
                LandingPriority(position, cost) < LandingPriority(existing.Position, existing.Cost) &&
                !NavMesh.Raycast(existing.Position, position, out _, _walkFilter))
                return index;
        }
        return -1;
    }

    public bool TryResolveTarget(Vector3 requestedTarget, NavMeshQueryFilter filter,
        Transform ownerRoot, Transform targetRoot, Settings settings, out Vector3 point)
    {
        settings = Sanitize(settings);
        // Сначала опора под маркером. Большой сферический SamplePosition выбирает соседний этаж.
        int count = Physics.RaycastNonAlloc(requestedTarget + Vector3.up * 0.2f, Vector3.down,
            _raycastHits, settings.TargetGroundProbeDistance + 0.2f, settings.CollisionMask, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        RaycastHit support = default;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = _raycastHits[index];
            if (hit.collider != null && hit.normal.y >= FloorNormalThreshold &&
                !IsIgnored(hit.collider, ownerRoot, targetRoot) && hit.distance < nearest)
            {
                nearest = hit.distance;
                support = hit;
            }
        }
        if (!float.IsPositiveInfinity(nearest))
        {
            if (NavMesh.SamplePosition(support.point, out NavMeshHit floor, settings.NavMeshSampleRadius, filter) &&
                Mathf.Abs(floor.position.y - support.point.y) <= MaximumFloorToNavMeshHeight)
            {
                point = floor.position;
                return true;
            }
            point = default;
            return false; // Нельзя провалиться на другой этаж, если ближайшая опора непроходима.
        }
        if (NavMesh.SamplePosition(requestedTarget, out NavMeshHit direct, settings.NavMeshSampleRadius, filter))
        {
            point = direct.position;
            return true;
        }
        point = default;
        return false;
    }

    public bool IsJumpStillClear(Route jump, Vector3 actualTakeoff, NavMeshAgent agent,
        Transform ownerRoot, Transform targetRoot, Settings settings)
    {
        settings = Sanitize(settings);
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = GetWalkOnlyAreaMask(agent.areaMask)
        };
        Vector3 horizontal = jump.LandingPosition - actualTakeoff;
        horizontal.y = 0f;
        return IsWithinJumpLimits(horizontal.magnitude, jump.LandingPosition.y - actualTakeoff.y,
                Mathf.Max(0f, settings.MinHorizontalDistance - 0.05f), settings.MaxHorizontalDistance + 0.05f,
                settings.MaxRise + 0.05f, settings.MaxDrop + 0.05f) &&
            NavMesh.SamplePosition(jump.LandingPosition, out NavMeshHit landing, 0.25f, filter) &&
            (landing.position - jump.LandingPosition).sqrMagnitude < 0.0625f &&
            LandingIsClear(jump.LandingPosition, agent, ownerRoot, settings) &&
            TrajectoryIsClear(actualTakeoff, jump.LandingPosition, jump.ApexHeight, agent, ownerRoot, targetRoot, settings);
    }

    // NavMeshLink проходит те же ограничения и проверки, что и автоматически найденный прыжок.
    public bool TryCreateJump(Vector3 takeoff, Vector3 landing, NavMeshAgent agent,
        Transform ownerRoot, Transform targetRoot, Settings settings, out Route jump)
    {
        settings = Sanitize(settings);
        jump = default;
        Vector3 horizontal = landing - takeoff;
        horizontal.y = 0f;
        float rise = landing.y - takeoff.y;
        if (!IsWithinJumpLimits(horizontal.magnitude, rise, settings.MinHorizontalDistance,
                settings.MaxHorizontalDistance, settings.MaxRise, settings.MaxDrop) ||
            !LandingIsClear(landing, agent, ownerRoot, settings))
            return false;
        float preferredApex = Mathf.Max(SelectApexHeight(rise, settings.JumpApexHeight, settings.DropApexHeight),
            CalculateMinimumDropApexHeight(rise, horizontal.magnitude, settings.LandingInset, agent.radius, settings.CollisionSkin));
        if (!TryFindClearApexHeight(takeoff, landing, preferredApex, agent, ownerRoot, targetRoot, settings, out float apex))
            return false;
        jump = new Route(takeoff, landing, landing, 0f, 0f,
            EstimateFlightDuration(takeoff, landing, apex, settings.JumpSpeed, settings.BaseJumpDuration), apex);
        return IsJumpStillClear(jump, takeoff, agent, ownerRoot, targetRoot, settings);
    }
    private bool TryFindClearApexHeight(
        Vector3 takeoff,
        Vector3 landing,
        float preferredApexHeight,
        NavMeshAgent agent,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings,
        out float clearApexHeight)
    {
        float normalApexHeight = Mathf.Max(preferredApexHeight, settings.JumpApexHeight);
        float extraClearance = Mathf.Max(1f, Mathf.Max(agent.height, agent.radius * 2f));
        float maximumApexHeight = Mathf.Max(
            normalApexHeight + extraClearance * 2f,
            preferredApexHeight);

        Vector3 horizontal = landing - takeoff;
        horizontal.y = 0f;
        float minimumApexHeight = CalculateMinimumDropApexHeight(landing.y - takeoff.y,
            horizontal.magnitude, settings.LandingInset, agent.radius, settings.CollisionSkin);
        const int apexAttempts = 9;
        for (int attempt = 0; attempt < apexAttempts; attempt++)
        {
            float apexHeight;
            if (attempt == 0)
            {
                apexHeight = preferredApexHeight;
            }
            else if (attempt <= 4)
            {
                // На этажерках высокая дуга упирается в потолок ещё до края.
                // Проверяем также низкие дуги, сохраняя клиренс при спуске.
                apexHeight = Mathf.Lerp(preferredApexHeight, minimumApexHeight, attempt / 4f);
            }
            else
            {
                float normalizedAttempt = (attempt - 4) / 4f;
                apexHeight = Mathf.Lerp(normalApexHeight, maximumApexHeight, normalizedAttempt);
            }

            if (TrajectoryIsClear(
                    takeoff,
                    landing,
                    apexHeight,
                    agent,
                    ownerRoot,
                    targetRoot,
                    settings))
            {
                clearApexHeight = apexHeight;
                return true;
            }
        }

        clearApexHeight = 0f;
        return false;
    }

    private void CollectEdgesNear(
        Vector3 center,
        Vector3 towardPosition,
        NavMeshQueryFilter filter,
        Settings settings)
    {
        TryAddDirectedEdge(center, towardPosition, filter);
        TryAddEdge(center, center, filter);

        int ringSamples = Mathf.Max(4, settings.EdgeRingSamples);
        for (int ring = 1; ring <= 2; ring++)
        {
            float radius = settings.EdgeSearchRadius * ring * 0.5f;
            for (int index = 0; index < ringSamples; index++)
            {
                float angle = index * Mathf.PI * 2f / ringSamples;
                Vector3 probe = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                TryAddDirectedEdge(center, probe, filter);
                TryAddEdge(probe, center, filter);
                CollectFloorPoints(probe, center.y - settings.MaxDrop, center.y + settings.MaxRise,
                    filter, _ownerRoot, _targetRoot, settings);
                for (int floor = 0; floor < _floorCandidates.Count; floor++)
                    TryAddEdge(_floorCandidates[floor], center, filter);
            }
        }
    }

    private void TryAddDirectedEdge(
        Vector3 center,
        Vector3 towardPosition,
        NavMeshQueryFilter filter)
    {
        Vector3 direction = towardPosition - center;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
            return;

        Vector3 horizontalTarget = center + direction;
        if (NavMesh.Raycast(center, horizontalTarget, out NavMeshHit edgeHit, filter))
        {
            Vector3 outward = edgeHit.normal;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = direction;
            }
            else if (Vector3.Dot(outward, direction) < 0f)
            {
                outward = -outward;
            }

            AddEdgeCandidate(edgeHit.position, outward.normalized);
        }
    }

    private void TryAddEdge(Vector3 probe, Vector3 center, NavMeshQueryFilter filter)
    {
        if (!NavMesh.SamplePosition(probe, out NavMeshHit sampleHit, 1.25f, filter) ||
            !NavMesh.FindClosestEdge(sampleHit.position, out NavMeshHit edgeHit, filter))
        {
            return;
        }

        Vector3 insideToEdge = edgeHit.position - sampleHit.position;
        insideToEdge.y = 0f;
        Vector3 outward = edgeHit.normal;
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.01f)
        {
            outward = insideToEdge;
            outward.y = 0f;
        }
        else if (insideToEdge.sqrMagnitude > 0.01f &&
                 Vector3.Dot(outward, insideToEdge) < 0f)
        {
            outward = -outward;
        }

        if (outward.sqrMagnitude < 0.01f)
        {
            outward = edgeHit.position - center;
            outward.y = 0f;
        }

        if (outward.sqrMagnitude < 0.01f)
            return;

        AddEdgeCandidate(edgeHit.position, outward.normalized);
    }

    private void AddEdgeCandidate(Vector3 position, Vector3 outward)
    {
        if (outward.sqrMagnitude < 0.01f)
            return;

        Vector3 normalizedOutward = outward.normalized;
        for (int index = 0; index < _edgeCandidates.Count; index++)
        {
            if ((_edgeCandidates[index].Position - position).sqrMagnitude <
                    EdgeDuplicateDistance * EdgeDuplicateDistance &&
                Vector3.Dot(_edgeCandidates[index].Outward, normalizedOutward) > 0.8f)
            {
                return;
            }
        }

        _edgeCandidates.Add(new EdgeCandidate(position, normalizedOutward));
    }

    private static bool TryGetInsetNavMeshPoint(
        Vector3 edgePosition,
        Vector3 insetDirection,
        NavMeshQueryFilter filter,
        Settings settings,
        out Vector3 point)
    {
        Vector3 probe = edgePosition + insetDirection.normalized * settings.LandingInset;
        if (NavMesh.SamplePosition(probe, out NavMeshHit hit, settings.NavMeshSampleRadius, filter) &&
            Mathf.Abs(hit.position.y - edgePosition.y) <= MaximumFloorToNavMeshHeight)
        {
            point = hit.position;
            return true;
        }

        point = default;
        return false;
    }

    private void CollectFloorPoints(
        Vector3 horizontalProbe,
        float minimumHeight,
        float maximumHeight,
        NavMeshQueryFilter filter,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings)
    {
        _floorCandidates.Clear();
        Vector3 rayOrigin = new Vector3(
            horizontalProbe.x,
            maximumHeight + 0.5f,
            horizontalProbe.z);
        float rayDistance = maximumHeight - minimumHeight + 1f;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            _raycastHits,
            rayDistance,
            settings.CollisionMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = _raycastHits[index];
            if (hit.collider == null ||
                hit.normal.y < FloorNormalThreshold ||
                IsIgnored(hit.collider, ownerRoot, targetRoot))
            {
                continue;
            }

            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, settings.NavMeshSampleRadius, filter) ||
                Mathf.Abs(navHit.position.y - hit.point.y) > MaximumFloorToNavMeshHeight)
            {
                continue;
            }

            bool duplicate = false;
            for (int floor = 0; floor < _floorCandidates.Count; floor++)
                duplicate |= (_floorCandidates[floor] - navHit.position).sqrMagnitude < 0.01f;
            if (!duplicate)
                _floorCandidates.Add(navHit.position);
        }
    }

    private bool TrajectoryIsClear(
        Vector3 takeoff,
        Vector3 landing,
        float apexHeight,
        NavMeshAgent agent,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings)
    {
        int segments = Mathf.Max(4, settings.TrajectorySegments);
        Vector3 previous = EvaluateTrajectory(takeoff, landing, apexHeight, 0f);

        for (int segment = 1; segment <= segments; segment++)
        {
            float normalizedTime = segment / (float)segments;
            Vector3 next = EvaluateTrajectory(takeoff, landing, apexHeight, normalizedTime);
            Vector3 direction = next - previous;
            float distance = direction.magnitude;

            if (distance > MinimumPathCornerDistance)
            {
                GetCapsule(previous, agent, settings.CollisionSkin, out Vector3 bottom, out Vector3 top, out float radius);
                int hitCount = Physics.CapsuleCastNonAlloc(
                    bottom,
                    top,
                    radius,
                    direction / distance,
                    _raycastHits,
                    distance,
                    settings.CollisionMask,
                    QueryTriggerInteraction.Ignore);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    Collider collider = _raycastHits[hitIndex].collider;
                    if (collider != null && !IsIgnored(collider, ownerRoot, targetRoot))
                        return false;
                }
            }

            previous = next;
        }

        return true;
    }

    private bool LandingIsClear(
        Vector3 landing,
        NavMeshAgent agent,
        Transform ownerRoot,
        Settings settings)
    {
        GetCapsule(landing, agent, settings.CollisionSkin, out Vector3 bottom, out Vector3 top, out float radius);
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            _overlapHits,
            settings.CollisionMask,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < overlapCount; index++)
        {
            Collider collider = _overlapHits[index];
            if (collider != null && !IsSameHierarchy(collider.transform, ownerRoot))
                return false;
        }

        return true;
    }

    private static void GetCapsule(
        Vector3 surfacePosition,
        NavMeshAgent agent,
        float skin,
        out Vector3 bottom,
        out Vector3 top,
        out float radius)
    {
        radius = Mathf.Max(0.05f, agent.radius - skin);
        float height = Mathf.Max(agent.height, radius * 2f);
        float verticalSkin = Mathf.Max(0.01f, skin);
        bottom = surfacePosition + Vector3.up * (radius + verticalSkin);
        top = surfacePosition + Vector3.up * (height - radius + verticalSkin);
    }

    private static bool IsIgnored(Collider collider, Transform ownerRoot, Transform targetRoot)
    {
        Transform colliderTransform = collider.transform;
        return IsSameHierarchy(colliderTransform, ownerRoot) || IsSameHierarchy(colliderTransform, targetRoot);
    }

    private static bool IsSameHierarchy(Transform candidate, Transform root)
    {
        return root != null && (candidate == root || candidate.IsChildOf(root));
    }

    private static bool TryGetCompletePathLength(
        Vector3 from,
        Vector3 to,
        NavMeshQueryFilter filter,
        NavMeshPath path,
        out float length)
    {
        length = 0f;
        if (!NavMesh.CalculatePath(from, to, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        Vector3[] corners = path.corners;
        for (int index = 1; index < corners.Length; index++)
            length += Vector3.Distance(corners[index - 1], corners[index]);

        return true;
    }

    private static Settings Sanitize(Settings settings)
    {
        settings.MoveSpeed = Mathf.Max(0.1f, settings.MoveSpeed);
        settings.MinHorizontalDistance = Mathf.Max(0.1f, settings.MinHorizontalDistance);
        settings.MaxHorizontalDistance = Mathf.Max(settings.MinHorizontalDistance, settings.MaxHorizontalDistance);
        settings.MaxRise = Mathf.Max(0f, settings.MaxRise);
        settings.MaxDrop = Mathf.Max(0f, settings.MaxDrop);
        settings.JumpSpeed = Mathf.Max(0.1f, settings.JumpSpeed);
        settings.BaseJumpDuration = Mathf.Max(0.05f, settings.BaseJumpDuration);
        settings.JumpApexHeight = Mathf.Max(0f, settings.JumpApexHeight);
        settings.DropApexHeight = Mathf.Max(0f, settings.DropApexHeight);
        settings.EdgeSearchRadius = Mathf.Max(0.5f, settings.EdgeSearchRadius);
        settings.LandingInset = Mathf.Max(0.1f, settings.LandingInset);
        settings.NavMeshSampleRadius = Mathf.Max(0.1f, settings.NavMeshSampleRadius);
        settings.EdgeRingSamples = Mathf.Max(4, settings.EdgeRingSamples);
        settings.LandingSampleStep = Mathf.Max(0.1f, settings.LandingSampleStep);
        settings.MaximumCandidateChecks = Mathf.Max(1, settings.MaximumCandidateChecks);
        settings.TrajectorySegments = Mathf.Max(4, settings.TrajectorySegments);
        settings.CollisionSkin = Mathf.Max(0.01f, settings.CollisionSkin);
        settings.MaximumJumps = settings.MaximumJumps > 0 ? settings.MaximumJumps : 8;
        settings.MaximumSearchNodes = settings.MaximumSearchNodes > 0 ? settings.MaximumSearchNodes : 256;
        settings.MaximumExpandedNodes = settings.MaximumExpandedNodes > 0 ? settings.MaximumExpandedNodes : 64;
        settings.TargetGroundProbeDistance = settings.TargetGroundProbeDistance > 0f ? settings.TargetGroundProbeDistance : 3f;
        settings.LongJumpPenalty = Mathf.Max(0f, settings.LongJumpPenalty);
        return settings;
    }

    public static int CalculateDistanceSampleCount(
        float minimumDistance,
        float maximumDistance,
        float sampleStep)
    {
        float safeMinimum = Mathf.Max(0.1f, minimumDistance);
        float safeMaximum = Mathf.Max(safeMinimum, maximumDistance);
        float safeStep = Mathf.Max(0.1f, sampleStep);
        return Mathf.CeilToInt((safeMaximum - safeMinimum) / safeStep) + 1;
    }

    public static float CalculateProbeDistance(float jumpDistance, float landingInset)
    {
        return Mathf.Max(0.1f, jumpDistance - Mathf.Max(0f, landingInset));
    }

    public static bool IsWithinJumpLimits(
        float horizontalDistance,
        float verticalDelta,
        float minimumHorizontalDistance,
        float maximumHorizontalDistance,
        float maximumRise,
        float maximumDrop)
    {
        float safeMinimum = Mathf.Max(0f, minimumHorizontalDistance);
        float safeMaximum = Mathf.Max(safeMinimum, maximumHorizontalDistance);
        return horizontalDistance >= safeMinimum &&
               horizontalDistance <= safeMaximum &&
               verticalDelta <= Mathf.Max(0f, maximumRise) &&
               verticalDelta >= -Mathf.Max(0f, maximumDrop);
    }

    private static float GetDistanceSample(
        float minimumDistance,
        float maximumDistance,
        float sampleStep,
        int oneBasedSampleIndex)
    {
        float safeMinimum = Mathf.Max(0.1f, minimumDistance);
        float safeMaximum = Mathf.Max(safeMinimum, maximumDistance);
        float safeStep = Mathf.Max(0.1f, sampleStep);
        int safeIndex = Mathf.Max(1, oneBasedSampleIndex);
        return Mathf.Min(safeMaximum, safeMinimum + (safeIndex - 1) * safeStep);
    }

    public static Vector3 EvaluateTrajectory(Vector3 start, Vector3 end, float apexHeight, float normalizedTime)
    {
        float time = Mathf.Clamp01(normalizedTime);
        return Vector3.Lerp(start, end, time) + Vector3.up * (4f * time * (1f - time) * apexHeight);
    }

    public static float SelectApexHeight(float verticalDelta, float jumpApexHeight, float dropApexHeight)
    {
        return verticalDelta < -0.25f
            ? Mathf.Max(0f, dropApexHeight)
            : Mathf.Max(0f, jumpApexHeight);
    }

    public static float CalculateMinimumDropApexHeight(
        float verticalDelta,
        float horizontalDistance,
        float takeoffInset,
        float bodyRadius,
        float clearanceSkin)
    {
        if (verticalDelta >= -0.25f)
            return 0f;

        float safeHorizontalDistance = Mathf.Max(0.01f, horizontalDistance);
        float distanceUntilBodyClearsEdge =
            Mathf.Max(0f, takeoffInset) + Mathf.Max(0f, bodyRadius);
        float edgeClearTime = Mathf.Clamp(
            distanceUntilBodyClearsEdge / safeHorizontalDistance,
            0.05f,
            0.8f);
        float requiredVerticalClearance = Mathf.Max(0.05f, clearanceSkin * 2f);
        float denominator = 4f * edgeClearTime * (1f - edgeClearTime);

        return Mathf.Max(
            0f,
            (requiredVerticalClearance - verticalDelta * edgeClearTime) /
            Mathf.Max(0.01f, denominator));
    }

    public static bool ShouldPrioritizeCurrentSide(float currentHeight, float targetHeight)
    {
        return targetHeight <= currentHeight;
    }

    public static float EstimateFlightDuration(
        Vector3 takeoff,
        Vector3 landing,
        float jumpSpeed,
        float baseJumpDuration)
    {
        return Mathf.Max(
            Mathf.Max(0.05f, baseJumpDuration),
            Vector3.Distance(takeoff, landing) / Mathf.Max(0.1f, jumpSpeed));
    }

    public static float EstimateFlightDuration(Vector3 takeoff, Vector3 landing, float apexHeight,
        float jumpSpeed, float baseJumpDuration)
    {
        Vector3 delta = landing - takeoff;
        // Производная параболы: максимальная скорость достигается на одном из концов.
        // Длина прямой между платформами недооценивает необходимое время высокой дуги.
        float verticalSpeed = Mathf.Abs(delta.y) + 4f * Mathf.Max(0f, apexHeight);
        float maximumDerivative = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z + verticalSpeed * verticalSpeed);
        return Mathf.Max(Mathf.Max(0.05f, baseJumpDuration), maximumDerivative / Mathf.Max(0.1f, jumpSpeed));
    }

    public static float CalculateJumpPenalty(float horizontalDistance, float verticalDelta,
        float maximumDistance, float maximumRise, float maximumDrop, float penalty)
    {
        float horizontalLoad = horizontalDistance / Mathf.Max(0.1f, maximumDistance);
        float verticalLoad = Mathf.Abs(verticalDelta) /
            Mathf.Max(0.1f, verticalDelta >= 0f ? maximumRise : maximumDrop);
        // До половины дальности/перепада переход считается обычным.
        // Ближе к пределу стоимость растёт плавно: длинный прыжок остаётся возможен,
        // но удобная промежуточная площадка делает цепочку предпочтительнее.
        float effort = Mathf.Max(0f, Mathf.Max(horizontalLoad, verticalLoad) * 2f - 1f);
        return Mathf.Max(0f, penalty) * effort * effort;
    }

    public static float EstimateJumpRouteTime(
        float approachLength,
        float remainingLength,
        float moveSpeed,
        float preparationDuration,
        float flightDuration,
        float landingRecoveryDuration)
    {
        float safeSpeed = Mathf.Max(0.1f, moveSpeed);
        return Mathf.Max(0f, approachLength) / safeSpeed +
               Mathf.Max(0f, preparationDuration) +
               Mathf.Max(0f, flightDuration) +
               Mathf.Max(0f, landingRecoveryDuration) +
               Mathf.Max(0f, remainingLength) / safeSpeed;
    }

    public static bool SavesEnoughTime(
        float normalTravelTime,
        float jumpTravelTime,
        float minimumAbsoluteSaving,
        float minimumRelativeSaving)
    {
        if (float.IsNaN(jumpTravelTime) || float.IsInfinity(jumpTravelTime))
            return false;

        if (float.IsPositiveInfinity(normalTravelTime))
            return true;

        if (normalTravelTime <= 0f || jumpTravelTime >= normalTravelTime)
            return false;

        float saving = normalTravelTime - jumpTravelTime;
        float relativeSaving = saving / normalTravelTime;
        return saving >= Mathf.Max(0f, minimumAbsoluteSaving) &&
               relativeSaving >= Mathf.Max(0f, minimumRelativeSaving);
    }

    public static bool SavesEnoughTimeForRoute(
        float normalTravelTime,
        float jumpTravelTime,
        float minimumAbsoluteSaving,
        float minimumRelativeSaving,
        bool isVerticalShortcut)
    {
        if (!isVerticalShortcut)
        {
            return SavesEnoughTime(
                normalTravelTime,
                jumpTravelTime,
                minimumAbsoluteSaving,
                minimumRelativeSaving);
        }

        if (float.IsNaN(jumpTravelTime) || float.IsInfinity(jumpTravelTime))
            return false;
        if (float.IsPositiveInfinity(normalTravelTime))
            return true;

        return normalTravelTime > 0f && jumpTravelTime < normalTravelTime;
    }

    public static bool IsTopologyShortcut(
        float verticalDelta,
        float directDistance,
        bool walkPathFound,
        float walkPathLength,
        float minimumVerticalDelta,
        float minimumWalkDetourRatio)
    {
        if (Mathf.Abs(verticalDelta) >= Mathf.Max(0f, minimumVerticalDelta))
            return true;

        if (!walkPathFound)
            return true;

        float safeDirectDistance = Mathf.Max(0.01f, directDistance);
        return walkPathLength >= safeDirectDistance * Mathf.Max(1f, minimumWalkDetourRatio);
    }

    public static int GetWalkOnlyAreaMask(int sourceMask)
    {
        int mask = sourceMask;
        mask = RemoveAreaFromMask(mask, NavMesh.GetAreaFromName("Jump"));
        mask = RemoveAreaFromMask(mask, NavMesh.GetAreaFromName("JumpLink"));
        return mask;
    }

    public static int RemoveAreaFromMask(int mask, int areaIndex)
    {
        if (areaIndex < 0 || areaIndex > 31)
            return mask;

        return mask & ~(1 << areaIndex);
    }

    private void ResetDiagnostics()
    {
        LastFailureReason = PlanFailureReason.None;
        LastCandidateTakeoff = default;
        LastCandidateLanding = default;
        LastCandidateChecks = 0;
        _hadDistanceCandidate = false;
        _hadCandidate = false;
        _hadApproachCandidate = false;
        _hadRemainingRouteCandidate = false;
        _hadTopologyCandidate = false;
        _hadClearLandingCandidate = false;
        _hadClearTrajectoryCandidate = false;
    }

    private PlanFailureReason ResolveFailureReason()
    {
        if (!_hadCandidate)
            return PlanFailureReason.NoLandingCandidate;
        if (!_hadDistanceCandidate)
            return PlanFailureReason.JumpOutsideLimits;
        if (!_hadApproachCandidate)
            return PlanFailureReason.ApproachDisconnected;
        if (!_hadTopologyCandidate)
            return PlanFailureReason.NotAShortcut;
        if (!_hadClearLandingCandidate)
            return PlanFailureReason.LandingBlocked;
        if (!_hadClearTrajectoryCandidate)
            return PlanFailureReason.TrajectoryBlocked;
        if (!_hadRemainingRouteCandidate)
            return PlanFailureReason.RemainingRouteDisconnected;

        return PlanFailureReason.NotEnoughTimeSaved;
    }
}
