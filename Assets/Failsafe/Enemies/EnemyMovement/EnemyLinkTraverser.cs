using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Общий для всех состояний паука слой маршрутизации.
/// Сравнивает пеший путь с локальным прыжком и исполняет зафиксированную траекторию.
/// </summary>
public class EnemyLinkTraverser : MonoBehaviour
{
    [Header("Настройки прыжка")]
    [Tooltip("Старая кривая сохранена для уже расставленных NavMeshLink.")]
    [SerializeField] private AnimationCurve _jumpCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f));
    [SerializeField, Min(0.05f)] private float _baseJumpDuration = 0.5f;
    [SerializeField, Min(0f)] private float _heightMultiplier = 2f;
    [SerializeField, Min(0f)] private float _dropApexHeight = 0.75f;

    [Header("Выбор короткого пути")]
    [SerializeField] private bool _enableShortcutPlanning = true;
    [SerializeField, Min(0.1f)] private float _minimumHorizontalDistance = 1f;
    [SerializeField, Min(0.1f)] private float _maximumHorizontalDistance = 8f;
    [SerializeField, Min(0f)] private float _maximumRise = 6f;
    [SerializeField, Min(0f)] private float _maximumDrop = 8f;
    [SerializeField, Min(0.1f)] private float _jumpSpeed = 8f;
    [SerializeField, Min(0f)] private float _minimumTimeSaving = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _minimumRelativeSaving = 0.1f;

    [Header("Безопасность траектории")]
    [SerializeField] private LayerMask _collisionMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(4)] private int _trajectorySegments = 12;
    [SerializeField, Min(0.01f)] private float _collisionSkin = 0.08f;
    [SerializeField, Min(0f)] private float _preparationDuration = 0.12f;
    [SerializeField, Min(0f)] private float _landingRecoveryDuration = 0.18f;

    private const float PlanningInterval = 0.75f;
    private const float PlanningStaggerWindow = 0.25f;
    private const float TargetReplanDistance = 2f;
    private const float TakeoffTolerance = 0.4f;
    private const float TakeoffStoppingDistance = 0.05f;
    private const float PostLandingPlanningCooldown = 0.25f;
    private const float EdgeSearchRadius = 7f;
    private const float LandingInset = 0.65f;
    private const float NavMeshSampleRadius = 0.8f;
    private const float MinimumVerticalShortcut = 0.75f;
    private const float MinimumWalkDetourRatio = 1.4f;
    private const int EdgeRingSamples = 8;
    private const float LandingSampleStep = 0.5f;
    private const int MaximumCandidateChecks = 96;

    private SpiderJumpPlanner _planner;

    private NavMeshAgent _agent;
    private EnemyAnimator _animator;
    private EnemyMovement _movement;
    private Coroutine _traversalRoutine;
    private SpiderJumpPlanner.Route? _plannedRoute;
    private Vector3 _requestedDestination;
    private float _requestedSpeed;
    private Transform _requestedTargetRoot;
    private bool _hasRequestedDestination;
    private bool _isTraversing;
    private bool _storedAgentState;
    private bool _previousUpdatePosition;
    private bool _previousUpdateRotation;
    private bool _previousIsStopped;
    private bool _hasStoredStoppingDistance;
    private float _destinationStoppingDistance;
    private Vector3 _traversalStartPosition;
    private float _nextPlanningTime;
#if UNITY_EDITOR
    private bool _hasReportedPlannerResult;
    private bool _lastPlannerSucceeded;
    private bool _lastPlannerTargetWasHigher;
    private SpiderJumpPlanner.PlanFailureReason _lastPlannerFailureReason;
#endif

    public bool IsTraversing => _isTraversing;
    public bool HasCommittedTraversal => _isTraversing || _plannedRoute.HasValue;

    public void Initialize(NavMeshAgent agent, EnemyAnimator animator, EnemyMovement movement)
    {
        _agent = agent;
        _animator = animator;
        _movement = movement;
        _planner = new SpiderJumpPlanner();

        if (_agent == null)
            return;

        _agent.autoTraverseOffMeshLink = false;
        float stagger = Mathf.Abs(GetInstanceID() % 1000) / 1000f * PlanningStaggerWindow;
        _nextPlanningTime = Time.time + stagger;
    }

    /// <summary>
    /// Принимает цель от любого состояния: патруля, поиска или погони.
    /// </summary>
    public void SetDestination(Vector3 destination, float speed, Transform targetRoot = null)
    {
        if (_agent == null || _isTraversing)
            return;

        bool plannedTargetMoved = _plannedRoute.HasValue &&
                                  (_plannedRoute.Value.PlannedTargetPosition - destination).sqrMagnitude >
                                  TargetReplanDistance * TargetReplanDistance;

        _requestedDestination = destination;
        _requestedSpeed = Mathf.Max(0.1f, speed);
        _requestedTargetRoot = targetRoot;
        _hasRequestedDestination = true;

        if (plannedTargetMoved)
        {
            ReleasePlannedRoute();
            _nextPlanningTime = Time.time;
        }

        ApplyCurrentDestination();
    }

    public void ClearDestination()
    {
        if (_isTraversing)
            return;

        ReleasePlannedRoute();
        _hasRequestedDestination = false;
        _requestedTargetRoot = null;
    }

    public void CheckAndTraverseLink()
    {
        if (_agent == null || !_agent.enabled || _isTraversing)
            return;

        if (_agent.isOnOffMeshLink)
        {
            BeginLegacyLinkTraversal();
            return;
        }

        if (_plannedRoute.HasValue)
        {
            SpiderJumpPlanner.Route route = _plannedRoute.Value;

            if (!_agent.pathPending &&
                Vector3.Distance(transform.position, route.TakeoffPosition) <=
                Mathf.Max(TakeoffTolerance, _agent.radius * 0.8f))
            {
                BeginPlannedTraversal(route);
            }

            return;
        }

        if (!_enableShortcutPlanning ||
            _planner == null ||
            !_hasRequestedDestination ||
            !_agent.isOnNavMesh ||
            Time.time < _nextPlanningTime)
        {
            return;
        }

        _nextPlanningTime = Time.time + PlanningInterval;
        bool routeFound = _planner.TryPlan(
                _agent,
                transform,
                _requestedTargetRoot,
                _requestedDestination,
                CreatePlannerSettings(),
                out SpiderJumpPlanner.Route plannedRoute);

#if UNITY_EDITOR
        ReportPlannerResult(routeFound, plannedRoute);
#endif

        if (routeFound)
        {
            StoreDestinationStoppingDistance();
            _plannedRoute = plannedRoute;
            ApplyCurrentDestination();
        }
    }

    /// <summary>
    /// Прерывает и ожидание точки отрыва, и сам полёт. Используется forced-state'ами.
    /// </summary>
    public void CancelTraversal()
    {
        ReleasePlannedRoute();
        _hasRequestedDestination = false;
        _requestedTargetRoot = null;

        if (_traversalRoutine != null)
        {
            StopCoroutine(_traversalRoutine);
            _traversalRoutine = null;
        }

        if (_isTraversing)
            RestoreAfterInterruptedTraversal();
    }

    private void BeginPlannedTraversal(SpiderJumpPlanner.Route route)
    {
        if (!BeginTraversal())
            return;

        _traversalRoutine = StartCoroutine(TraversePlannedRouteRoutine(route));
    }

    private void BeginLegacyLinkTraversal()
    {
        if (!BeginTraversal())
            return;

        OffMeshLinkData data = _agent.currentOffMeshLinkData;
        Vector3 endPosition = data.endPos + Vector3.up * _agent.baseOffset;
        float distance = Vector3.Distance(transform.position, endPosition);
        float duration = Mathf.Max(_baseJumpDuration, distance / Mathf.Max(0.1f, _agent.speed));
        _traversalRoutine = StartCoroutine(TraverseLegacyLinkRoutine(endPosition, duration));
    }

    private bool BeginTraversal()
    {
        if (_isTraversing || _agent == null || !_agent.enabled)
            return false;

        _isTraversing = true;
        _traversalStartPosition = transform.position;
        _previousUpdatePosition = _agent.updatePosition;
        _previousUpdateRotation = _agent.updateRotation;
        _previousIsStopped = _agent.isStopped;
        _storedAgentState = true;

        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            if (!_agent.isOnOffMeshLink)
                _agent.ResetPath();
        }

        _agent.updatePosition = false;
        _agent.updateRotation = false;
        _movement?.SetTraversalBusy(true);
        return true;
    }

    private IEnumerator TraversePlannedRouteRoutine(SpiderJumpPlanner.Route route)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = route.LandingPosition + Vector3.up * _agent.baseOffset;
        FaceLanding(endPosition);
        _animator?.Jump();

        yield return WaitForTraversalDelay(_preparationDuration);

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, route.FlightDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            Vector3 position = SpiderJumpPlanner.EvaluateTrajectory(
                startPosition,
                endPosition,
                route.ApexHeight,
                normalizedTime);
            SetTraversalPosition(position);
            yield return null;
        }

        SetTraversalPosition(endPosition);
        _animator?.Land();
        yield return WaitForTraversalDelay(_landingRecoveryDuration);

        FinishTraversal(route.LandingPosition, false);
    }

    private IEnumerator TraverseLegacyLinkRoutine(Vector3 endPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        FaceLanding(endPosition);
        _animator?.Jump();

        float curveStart = _jumpCurve != null ? _jumpCurve.Evaluate(0f) : 0f;
        float curveEnd = _jumpCurve != null ? _jumpCurve.Evaluate(1f) : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            float rawCurveHeight = _jumpCurve != null
                ? _jumpCurve.Evaluate(normalizedTime)
                : 4f * normalizedTime * (1f - normalizedTime);
            float normalizedCurveHeight = rawCurveHeight - Mathf.Lerp(curveStart, curveEnd, normalizedTime);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, normalizedTime);
            position.y += normalizedCurveHeight * _heightMultiplier;
            SetTraversalPosition(position);
            yield return null;
        }

        SetTraversalPosition(endPosition);
        _animator?.Land();

        if (_agent.enabled && _agent.isOnOffMeshLink)
            _agent.CompleteOffMeshLink();

        FinishTraversal(endPosition - Vector3.up * _agent.baseOffset, true);
    }

    private IEnumerator WaitForTraversalDelay(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void FinishTraversal(Vector3 navMeshPosition, bool legacyLink)
    {
        ReleasePlannedRoute();
        RestoreAgentState(navMeshPosition);
        _isTraversing = false;
        _traversalRoutine = null;
        _movement?.SetTraversalBusy(false);
        _nextPlanningTime = Time.time + PostLandingPlanningCooldown;

        if (!legacyLink)
            ApplyCurrentDestination();
    }

    private void RestoreAfterInterruptedTraversal()
    {
        Vector3 safePosition = _traversalStartPosition;
        if (_agent != null && _agent.enabled)
        {
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };

            float searchRadius = Mathf.Max(2f, _maximumRise + _maximumDrop);
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, filter))
                safePosition = hit.position;
        }

        RestoreAgentState(safePosition);
        _isTraversing = false;
        _movement?.SetTraversalBusy(false);
    }

    private void RestoreAgentState(Vector3 navMeshPosition)
    {
        if (_agent == null)
            return;

        if (_agent.enabled)
        {
            if (_agent.isOnNavMesh)
                _agent.Warp(navMeshPosition);
            else
                transform.position = navMeshPosition;

            _agent.updatePosition = _storedAgentState ? _previousUpdatePosition : _agent.updatePosition;
            _agent.updateRotation = _storedAgentState ? _previousUpdateRotation : _agent.updateRotation;

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = _storedAgentState ? _previousIsStopped : true;
                _agent.velocity = Vector3.zero;
            }
        }
        else
        {
            transform.position = navMeshPosition;
        }

        _storedAgentState = false;
    }

    private void SetTraversalPosition(Vector3 position)
    {
        transform.position = position;
    }

    private void FaceLanding(Vector3 landing)
    {
        Vector3 direction = landing - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void ApplyCurrentDestination()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _isTraversing)
            return;

        Vector3 destination = _plannedRoute.HasValue
            ? _plannedRoute.Value.TakeoffPosition
            : _requestedDestination;

        if (_plannedRoute.HasValue)
        {
            if (_hasStoredStoppingDistance &&
                !Mathf.Approximately(_agent.stoppingDistance, TakeoffStoppingDistance))
            {
                _destinationStoppingDistance = _agent.stoppingDistance;
            }

            _agent.stoppingDistance = TakeoffStoppingDistance;
        }

        _agent.speed = Mathf.Max(0.1f, _requestedSpeed);
        _agent.isStopped = false;
        _agent.SetDestination(destination);
    }

    private void StoreDestinationStoppingDistance()
    {
        if (_agent == null || _hasStoredStoppingDistance)
            return;

        _destinationStoppingDistance = _agent.stoppingDistance;
        _hasStoredStoppingDistance = true;
    }

    private void ReleasePlannedRoute()
    {
        _plannedRoute = null;

        if (_agent != null && _hasStoredStoppingDistance)
            _agent.stoppingDistance = _destinationStoppingDistance;

        _hasStoredStoppingDistance = false;
    }

    private SpiderJumpPlanner.Settings CreatePlannerSettings()
    {
        return new SpiderJumpPlanner.Settings
        {
            MoveSpeed = _requestedSpeed,
            MinHorizontalDistance = _minimumHorizontalDistance,
            MaxHorizontalDistance = _maximumHorizontalDistance,
            MaxRise = _maximumRise,
            MaxDrop = _maximumDrop,
            JumpSpeed = _jumpSpeed,
            BaseJumpDuration = _baseJumpDuration,
            JumpApexHeight = _heightMultiplier,
            DropApexHeight = _dropApexHeight,
            PreparationDuration = _preparationDuration,
            LandingRecoveryDuration = _landingRecoveryDuration,
            MinimumTimeSaving = _minimumTimeSaving,
            MinimumRelativeSaving = _minimumRelativeSaving,
            EdgeSearchRadius = EdgeSearchRadius,
            LandingInset = LandingInset,
            NavMeshSampleRadius = NavMeshSampleRadius,
            MinimumVerticalShortcut = MinimumVerticalShortcut,
            MinimumWalkDetourRatio = MinimumWalkDetourRatio,
            EdgeRingSamples = EdgeRingSamples,
            LandingSampleStep = LandingSampleStep,
            MaximumCandidateChecks = MaximumCandidateChecks,
            CollisionMask = _collisionMask,
            TrajectorySegments = _trajectorySegments,
            CollisionSkin = _collisionSkin
        };
    }

#if UNITY_EDITOR
    private void ReportPlannerResult(bool routeFound, SpiderJumpPlanner.Route route)
    {
        SpiderJumpPlanner.PlanFailureReason failureReason =
            routeFound ? SpiderJumpPlanner.PlanFailureReason.None : _planner.LastFailureReason;
        bool targetWasHigher = _requestedDestination.y > transform.position.y + 0.25f;
        if (_hasReportedPlannerResult &&
            _lastPlannerSucceeded == routeFound &&
            _lastPlannerFailureReason == failureReason &&
            _lastPlannerTargetWasHigher == targetWasHigher)
        {
            return;
        }

        _hasReportedPlannerResult = true;
        _lastPlannerSucceeded = routeFound;
        _lastPlannerFailureReason = failureReason;
        _lastPlannerTargetWasHigher = targetWasHigher;

        if (routeFound)
        {
            Debug.Log(
                $"[{nameof(EnemyLinkTraverser)}] '{name}' выбрал прыжок: " +
                $"{route.TakeoffPosition} -> {route.LandingPosition}, " +
                $"дуга {route.ApexHeight:F2} м, экономия {route.TimeSaving:F2} с.",
                this);
            return;
        }

        Debug.Log(
            $"[{nameof(EnemyLinkTraverser)}] '{name}' не выбрал прыжок: {failureReason}. " +
            $"Позиция {transform.position}, цель {_requestedDestination}, " +
            $"последняя пара {_planner.LastCandidateTakeoff} -> {_planner.LastCandidateLanding}, " +
            $"проверок: {_planner.LastCandidateChecks}.",
            this);
    }
#endif

    private void OnDisable()
    {
        CancelTraversal();
    }
}
