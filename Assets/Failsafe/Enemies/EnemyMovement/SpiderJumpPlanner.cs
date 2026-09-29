using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ищет локальный маршрут с одним физическим прыжком и сравнивает его
/// с полностью пешим маршрутом. Класс не хранит состояние врага и не двигает его.
/// </summary>
public sealed class SpiderJumpPlanner
{
    public enum PlanFailureReason
    {
        None,
        InvalidAgent,
        NoWalkableAreas,
        StartOrTargetOutsideNavMesh,
        NoLandingCandidate,
        JumpOutsideLimits,
        ApproachDisconnected,
        RemainingRouteDisconnected,
        NotAShortcut,
        LandingBlocked,
        TrajectoryBlocked,
        NotEnoughTimeSaved
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
    }

    public readonly struct Route
    {
        public readonly Vector3 TakeoffPosition;
        public readonly Vector3 LandingPosition;
        public readonly Vector3 PlannedTargetPosition;
        public readonly float NormalTravelTime;
        public readonly float JumpTravelTime;
        public readonly float FlightDuration;
        public readonly float ApexHeight;

        public float TimeSaving => NormalTravelTime - JumpTravelTime;

        public Route(
            Vector3 takeoffPosition,
            Vector3 landingPosition,
            Vector3 plannedTargetPosition,
            float normalTravelTime,
            float jumpTravelTime,
            float flightDuration,
            float apexHeight)
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

    private readonly NavMeshPath _normalPath = new NavMeshPath();
    private readonly NavMeshPath _approachPath = new NavMeshPath();
    private readonly NavMeshPath _remainingPath = new NavMeshPath();
    private readonly NavMeshPath _betweenJumpPath = new NavMeshPath();
    private readonly List<EdgeCandidate> _edgeCandidates = new List<EdgeCandidate>(16);
    private readonly RaycastHit[] _raycastHits = new RaycastHit[32];
    private readonly Collider[] _overlapHits = new Collider[32];

    private bool _hadDistanceCandidate;
    private bool _hadApproachCandidate;
    private bool _hadRemainingRouteCandidate;
    private bool _hadTopologyCandidate;
    private bool _hadClearLandingCandidate;
    private bool _hadClearTrajectoryCandidate;

    public PlanFailureReason LastFailureReason { get; private set; }
    public Vector3 LastCandidateTakeoff { get; private set; }
    public Vector3 LastCandidateLanding { get; private set; }
    public int LastCandidateChecks { get; private set; }

    public bool TryPlan(
        NavMeshAgent agent,
        Transform ownerRoot,
        Transform targetRoot,
        Vector3 requestedTarget,
        Settings settings,
        out Route bestRoute)
    {
        bestRoute = default;
        ResetDiagnostics();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            LastFailureReason = PlanFailureReason.InvalidAgent;
            return false;
        }

        settings = Sanitize(settings);
        int walkOnlyMask = GetWalkOnlyAreaMask(agent.areaMask);
        if (walkOnlyMask == 0)
        {
            LastFailureReason = PlanFailureReason.NoWalkableAreas;
            return false;
        }

        var movementFilter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };
        var walkOnlyFilter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = walkOnlyMask
        };

        if (!NavMesh.SamplePosition(agent.transform.position, out NavMeshHit startHit, settings.NavMeshSampleRadius, movementFilter) ||
            !NavMesh.SamplePosition(requestedTarget, out NavMeshHit targetHit, settings.NavMeshSampleRadius, movementFilter))
        {
            LastFailureReason = PlanFailureReason.StartOrTargetOutsideNavMesh;
            return false;
        }

        bool hasNormalPath = TryGetCompletePathLength(
            startHit.position,
            targetHit.position,
            walkOnlyFilter,
            _normalPath,
            out float normalLength);
        float normalTravelTime = hasNormalPath
            ? normalLength / settings.MoveSpeed
            : float.PositiveInfinity;

        bool foundRoute = false;
        float bestJumpTime = float.PositiveInfinity;
        int candidateChecks = 0;

        int remainingChecks = Mathf.Max(0, settings.MaximumCandidateChecks - candidateChecks);
        int firstSideBudget = Mathf.Max(
            CalculateDistanceSampleCount(
                settings.MinHorizontalDistance,
                settings.MaxHorizontalDistance,
                settings.LandingSampleStep),
            (remainingChecks + 1) / 2);
        int firstSideLimit = Mathf.Min(
            settings.MaximumCandidateChecks,
            candidateChecks + firstSideBudget);

        if (ShouldPrioritizeCurrentSide(startHit.position.y, targetHit.position.y))
        {
            EvaluateCurrentSideCandidates(
                startHit.position,
                targetHit.position,
                normalTravelTime,
                walkOnlyFilter,
                agent,
                ownerRoot,
                targetRoot,
                settings,
                firstSideLimit,
                ref candidateChecks,
                ref foundRoute,
                ref bestJumpTime,
                ref bestRoute);

            EvaluateTargetSideCandidates(
                startHit.position,
                targetHit.position,
                normalTravelTime,
                walkOnlyFilter,
                agent,
                ownerRoot,
                targetRoot,
                settings,
                settings.MaximumCandidateChecks,
                ref candidateChecks,
                ref foundRoute,
                ref bestJumpTime,
                ref bestRoute);
        }
        else
        {
            EvaluateTargetSideCandidates(
                startHit.position,
                targetHit.position,
                normalTravelTime,
                walkOnlyFilter,
                agent,
                ownerRoot,
                targetRoot,
                settings,
                firstSideLimit,
                ref candidateChecks,
                ref foundRoute,
                ref bestJumpTime,
                ref bestRoute);

            EvaluateCurrentSideCandidates(
                startHit.position,
                targetHit.position,
                normalTravelTime,
                walkOnlyFilter,
                agent,
                ownerRoot,
                targetRoot,
                settings,
                settings.MaximumCandidateChecks,
                ref candidateChecks,
                ref foundRoute,
                ref bestJumpTime,
                ref bestRoute);
        }

        LastFailureReason = foundRoute
            ? PlanFailureReason.None
            : ResolveFailureReason(candidateChecks);
        LastCandidateChecks = candidateChecks;
        return foundRoute;
    }

    private void EvaluateCurrentSideCandidates(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float normalTravelTime,
        NavMeshQueryFilter walkOnlyFilter,
        NavMeshAgent agent,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings,
        int candidateLimit,
        ref int candidateChecks,
        ref bool foundRoute,
        ref float bestJumpTime,
        ref Route bestRoute)
    {
        CollectEdgesNear(currentPosition, targetPosition, walkOnlyFilter, settings);
        float minimumProbeDistance = CalculateProbeDistance(
            settings.MinHorizontalDistance,
            settings.LandingInset);
        float maximumProbeDistance = CalculateProbeDistance(
            settings.MaxHorizontalDistance,
            settings.LandingInset);
        int distanceSampleCount = CalculateDistanceSampleCount(
            minimumProbeDistance,
            maximumProbeDistance,
            settings.LandingSampleStep);

        for (int edgeIndex = 0;
             edgeIndex < _edgeCandidates.Count && candidateChecks < candidateLimit;
             edgeIndex++)
        {
            EdgeCandidate edge = _edgeCandidates[edgeIndex];
            if (!TryGetInsetNavMeshPoint(
                    edge.Position,
                    -edge.Outward,
                    walkOnlyFilter,
                    settings,
                    out Vector3 takeoff))
            {
                continue;
            }

            // Для статической точки сначала проверяем детерминированную пару:
            // доступный край текущего острова -> сама цель на нужном острове.
            if (targetRoot == null && candidateChecks < candidateLimit)
            {
                TryCandidate(
                    takeoff,
                    targetPosition,
                    currentPosition,
                    targetPosition,
                    normalTravelTime,
                    walkOnlyFilter,
                    agent,
                    ownerRoot,
                    targetRoot,
                    settings,
                    ref candidateChecks,
                    ref foundRoute,
                    ref bestJumpTime,
                    ref bestRoute);
            }

            for (int sample = 1;
                 sample <= distanceSampleCount && candidateChecks < candidateLimit;
                 sample++)
            {
                float distance = GetDistanceSample(
                    minimumProbeDistance,
                    maximumProbeDistance,
                    settings.LandingSampleStep,
                    sample);
                Vector3 floorProbe = edge.Position + edge.Outward * distance;
                if (!TryFindFloorOnNavMesh(
                        floorProbe,
                        edge.Position.y,
                        currentPosition.y,
                        targetPosition.y,
                        targetPosition.y,
                        walkOnlyFilter,
                        ownerRoot,
                        targetRoot,
                        settings,
                        out Vector3 landing))
                {
                    continue;
                }

                if (Vector3.Dot(landing - edge.Position, edge.Outward) < settings.LandingInset * 0.25f)
                    continue;

                TryCandidate(
                    takeoff,
                    landing,
                    currentPosition,
                    targetPosition,
                    normalTravelTime,
                    walkOnlyFilter,
                    agent,
                    ownerRoot,
                    targetRoot,
                    settings,
                    ref candidateChecks,
                    ref foundRoute,
                    ref bestJumpTime,
                    ref bestRoute);
            }
        }
    }

    private void EvaluateTargetSideCandidates(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float normalTravelTime,
        NavMeshQueryFilter walkOnlyFilter,
        NavMeshAgent agent,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings,
        int candidateLimit,
        ref int candidateChecks,
        ref bool foundRoute,
        ref float bestJumpTime,
        ref Route bestRoute)
    {
        CollectEdgesNear(targetPosition, currentPosition, walkOnlyFilter, settings);
        float minimumProbeDistance = CalculateProbeDistance(
            settings.MinHorizontalDistance,
            settings.LandingInset);
        float maximumProbeDistance = CalculateProbeDistance(
            settings.MaxHorizontalDistance,
            settings.LandingInset);
        int distanceSampleCount = CalculateDistanceSampleCount(
            minimumProbeDistance,
            maximumProbeDistance,
            settings.LandingSampleStep);

        for (int edgeIndex = 0;
             edgeIndex < _edgeCandidates.Count && candidateChecks < candidateLimit;
             edgeIndex++)
        {
            EdgeCandidate edge = _edgeCandidates[edgeIndex];
            if (!TryGetInsetNavMeshPoint(
                    edge.Position,
                    -edge.Outward,
                    walkOnlyFilter,
                    settings,
                    out Vector3 landing))
            {
                continue;
            }

            for (int sample = 1;
                 sample <= distanceSampleCount && candidateChecks < candidateLimit;
                 sample++)
            {
                float distance = GetDistanceSample(
                    minimumProbeDistance,
                    maximumProbeDistance,
                    settings.LandingSampleStep,
                    sample);
                Vector3 floorProbe = edge.Position + edge.Outward * distance;
                if (!TryFindFloorOnNavMesh(
                        floorProbe,
                        edge.Position.y,
                        currentPosition.y,
                        targetPosition.y,
                        currentPosition.y,
                        walkOnlyFilter,
                        ownerRoot,
                        targetRoot,
                        settings,
                        out Vector3 takeoff))
                {
                    continue;
                }

                if (Vector3.Dot(takeoff - edge.Position, edge.Outward) < settings.LandingInset * 0.25f)
                    continue;

                TryCandidate(
                    takeoff,
                    landing,
                    currentPosition,
                    targetPosition,
                    normalTravelTime,
                    walkOnlyFilter,
                    agent,
                    ownerRoot,
                    targetRoot,
                    settings,
                    ref candidateChecks,
                    ref foundRoute,
                    ref bestJumpTime,
                    ref bestRoute);
            }
        }
    }

    private void TryCandidate(
        Vector3 takeoff,
        Vector3 landing,
        Vector3 currentPosition,
        Vector3 targetPosition,
        float normalTravelTime,
        NavMeshQueryFilter walkOnlyFilter,
        NavMeshAgent agent,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings,
        ref int candidateChecks,
        ref bool foundRoute,
        ref float bestJumpTime,
        ref Route bestRoute)
    {
        candidateChecks++;
        LastCandidateTakeoff = takeoff;
        LastCandidateLanding = landing;

        Vector3 horizontalDelta = landing - takeoff;
        horizontalDelta.y = 0f;
        float horizontalDistance = horizontalDelta.magnitude;
        float verticalDelta = landing.y - takeoff.y;

        if (!IsWithinJumpLimits(
                horizontalDistance,
                verticalDelta,
                settings.MinHorizontalDistance,
                settings.MaxHorizontalDistance,
                settings.MaxRise,
                settings.MaxDrop))
        {
            return;
        }

        _hadDistanceCandidate = true;

        if (!TryGetCompletePathLength(
                currentPosition,
                takeoff,
                walkOnlyFilter,
                _approachPath,
                out float approachLength))
            return;

        _hadApproachCandidate = true;

        if (!TryGetCompletePathLength(
                landing,
                targetPosition,
                walkOnlyFilter,
                _remainingPath,
                out float remainingLength))
        {
            return;
        }

        _hadRemainingRouteCandidate = true;

        bool hasWalkPathAcrossJump = TryGetCompletePathLength(
            takeoff,
            landing,
            walkOnlyFilter,
            _betweenJumpPath,
            out float walkDistanceAcrossJump);

        if (!IsTopologyShortcut(
                verticalDelta,
                Vector3.Distance(takeoff, landing),
                hasWalkPathAcrossJump,
                walkDistanceAcrossJump,
                settings.MinimumVerticalShortcut,
                settings.MinimumWalkDetourRatio))
        {
            return;
        }

        _hadTopologyCandidate = true;

        if (!LandingIsClear(landing, agent, ownerRoot, settings))
            return;

        _hadClearLandingCandidate = true;

        float preferredApexHeight = SelectApexHeight(
            verticalDelta,
            settings.JumpApexHeight,
            settings.DropApexHeight);
        preferredApexHeight = Mathf.Max(
            preferredApexHeight,
            CalculateMinimumDropApexHeight(
                verticalDelta,
                horizontalDistance,
                settings.LandingInset,
                agent.radius,
                settings.CollisionSkin));

        if (!TryFindClearApexHeight(
                takeoff,
                landing,
                preferredApexHeight,
                agent,
                ownerRoot,
                targetRoot,
                settings,
                out float apexHeight))
            return;

        _hadClearTrajectoryCandidate = true;

        float flightDuration = EstimateFlightDuration(
            takeoff,
            landing,
            settings.JumpSpeed,
            settings.BaseJumpDuration);

        float jumpTravelTime = EstimateJumpRouteTime(
            approachLength,
            remainingLength,
            settings.MoveSpeed,
            settings.PreparationDuration,
            flightDuration,
            settings.LandingRecoveryDuration);

        bool isVerticalShortcut =
            Mathf.Abs(verticalDelta) >= settings.MinimumVerticalShortcut;
        if (!SavesEnoughTimeForRoute(
                normalTravelTime,
                jumpTravelTime,
                settings.MinimumTimeSaving,
                settings.MinimumRelativeSaving,
                isVerticalShortcut) ||
            jumpTravelTime >= bestJumpTime)
        {
            return;
        }

        bestJumpTime = jumpTravelTime;
        bestRoute = new Route(
            takeoff,
            landing,
            targetPosition,
            normalTravelTime,
            jumpTravelTime,
            flightDuration,
            apexHeight);
        foundRoute = true;
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

        const int apexAttempts = 5;
        for (int attempt = 0; attempt < apexAttempts; attempt++)
        {
            float apexHeight;
            if (attempt == 0)
            {
                apexHeight = preferredApexHeight;
            }
            else
            {
                float normalizedAttempt = attempt / (float)(apexAttempts - 1);
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
        _edgeCandidates.Clear();
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
                TryAddEdge(probe, center, filter);
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

    private bool TryFindFloorOnNavMesh(
        Vector3 horizontalProbe,
        float edgeHeight,
        float currentHeight,
        float targetHeight,
        float preferredHeight,
        NavMeshQueryFilter filter,
        Transform ownerRoot,
        Transform targetRoot,
        Settings settings,
        out Vector3 point)
    {
        float highestRelevantPoint = Mathf.Max(edgeHeight, Mathf.Max(currentHeight, targetHeight));
        Vector3 rayOrigin = new Vector3(
            horizontalProbe.x,
            highestRelevantPoint + settings.MaxRise + 1f,
            horizontalProbe.z);
        float rayDistance = settings.MaxRise + settings.MaxDrop + 4f;

        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            _raycastHits,
            rayDistance,
            settings.CollisionMask,
            QueryTriggerInteraction.Ignore);

        float closestHeightDifference = float.PositiveInfinity;
        float closestRayDistance = float.PositiveInfinity;
        point = default;
        bool found = false;

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

            float heightDifference = Mathf.Abs(navHit.position.y - preferredHeight);
            if (heightDifference > closestHeightDifference + 0.01f ||
                (Mathf.Abs(heightDifference - closestHeightDifference) <= 0.01f &&
                 hit.distance >= closestRayDistance))
            {
                continue;
            }

            closestHeightDifference = heightDifference;
            closestRayDistance = hit.distance;
            point = navHit.position;
            found = true;
        }

        return found;
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
        _hadApproachCandidate = false;
        _hadRemainingRouteCandidate = false;
        _hadTopologyCandidate = false;
        _hadClearLandingCandidate = false;
        _hadClearTrajectoryCandidate = false;
    }

    private PlanFailureReason ResolveFailureReason(int candidateChecks)
    {
        if (candidateChecks == 0)
            return PlanFailureReason.NoLandingCandidate;
        if (!_hadDistanceCandidate)
            return PlanFailureReason.JumpOutsideLimits;
        if (!_hadApproachCandidate)
            return PlanFailureReason.ApproachDisconnected;
        if (!_hadRemainingRouteCandidate)
            return PlanFailureReason.RemainingRouteDisconnected;
        if (!_hadTopologyCandidate)
            return PlanFailureReason.NotAShortcut;
        if (!_hadClearLandingCandidate)
            return PlanFailureReason.LandingBlocked;
        if (!_hadClearTrajectoryCandidate)
            return PlanFailureReason.TrajectoryBlocked;

        return PlanFailureReason.NotEnoughTimeSaved;
    }
}
