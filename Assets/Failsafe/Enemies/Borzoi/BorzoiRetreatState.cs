using UnityEngine;
using UnityEngine.AI;

public sealed class BorzoiRetreatState : BehaviorState
{
    private const float PathRetryDelay = 0.65f;
    private const float PathBoundaryClearance = 0.25f;
    private const float MinDistanceGain = 0.75f;
    private const float MaxRetreatDuration = 2.5f;
    private const float SeparationCheckInterval = 1.1f;
    private const float MinSeparationGain = 0.65f;
    private static readonly float[] EscapeAngles = { 0f, -35f, 35f, -70f, 70f };

    private readonly BorzoiGrenadeLauncher _launcher;
    private readonly VisualSensor _vision;
    private readonly EnemyMovement _movement;
    private readonly NavMeshAgent _agent;
    private readonly EnemyMemory _memory;
    private readonly float _meleeRange;

    private Vector3 _target;
    private Vector3 _pathTarget;
    private Vector3 _destination;
    private Vector3 _progressPosition;
    private float _nextPathAttempt;
    private float _replanAt;
    private float _sightDeadline;
    private float _progressDeadline;
    private float _canBeCaughtAt;
    private float _previousStoppingDistance;
    private float _retreatDeadline;
    private float _separationCheckAt;
    private float _distanceAtLastCheck;

    public bool IsFinished { get; private set; }
    public bool HasRetreatedSinceLastAttack { get; private set; }

    public BorzoiRetreatState(BorzoiGrenadeLauncher launcher, VisualSensor vision,
        EnemyMovement movement, NavMeshAgent agent, EnemyMemory memory, float meleeRange)
    {
        _launcher = launcher;
        _vision = vision;
        _movement = movement;
        _agent = agent;
        _memory = memory;
        _meleeRange = meleeRange;
    }

    public bool CanEnter()
    {
        if (HasRetreatedSinceLastAttack || !_launcher.HasGrenades || !CanMove || Time.time < _nextPathAttempt ||
            !ReadTarget() || !_launcher.IsTooClose(_target))
            return false;

        if (TryFindDestination())
            return true;

        _nextPathAttempt = Time.time + PathRetryDelay;
        return false;
    }

    public void AllowRetreat() => HasRetreatedSinceLastAttack = false;

    public override void Enter()
    {
        base.Enter();
        IsFinished = false;
        _previousStoppingDistance = _movement.StoppingDistance;
        _movement.Stop();
        _movement.StoppingDistance = 0.15f;
        _sightDeadline = Time.time + 0.5f;
        _progressPosition = _agent.transform.position;
        _progressDeadline = Time.time + 0.85f;
        _canBeCaughtAt = Time.time + 0.75f;

        // A forced state can return here after the player has moved.
        if (HasRetreatedSinceLastAttack || !_launcher.HasGrenades || !CanMove || !ReadTarget() ||
            !_launcher.IsTooClose(_target) || !TryFindDestination())
        {
            Finish();
            return;
        }

        HasRetreatedSinceLastAttack = true;
        _retreatDeadline = Time.time + MaxRetreatDuration;
        _separationCheckAt = Time.time + SeparationCheckInterval;
        _distanceAtLastCheck = Vector3.Distance(_progressPosition, _target);
        MoveToDestination();
    }

    public override void Update()
    {
        if (IsFinished)
            return;
        if (!_launcher.HasGrenades || !CanMove)
        {
            Finish();
            return;
        }

        bool seesPlayer = ReadTarget();
        if (seesPlayer)
            _sightDeadline = Time.time + 0.5f;
        else if (Time.time >= _sightDeadline)
        {
            Finish();
            return;
        }

        Vector3 position = _agent.transform.position;
        _movement.LookAt(_target);
        float distance = Vector3.Distance(position, _target);
        if (distance >= _launcher.PreferredRange || Time.time >= _retreatDeadline ||
            (seesPlayer && Time.time >= _canBeCaughtAt && distance <= _meleeRange))
        {
            Finish();
            return;
        }

        if (seesPlayer && Time.time >= _separationCheckAt)
        {
            if (distance - _distanceAtLastCheck < MinSeparationGain)
            {
                Finish();
                return;
            }

            _distanceAtLastCheck = distance;
            _separationCheckAt = Time.time + SeparationCheckInterval;
        }

        if ((position - _progressPosition).sqrMagnitude >= 0.16f)
        {
            _progressPosition = position;
            _progressDeadline = Time.time + 0.85f;
        }
        else if (Time.time >= _progressDeadline)
        {
            Finish();
            return;
        }

        bool reachedDestination = _movement.IsPointReached(0.35f);
        bool needsNewPath = reachedDestination || (_target - _pathTarget).sqrMagnitude >= 1.5f * 1.5f;
        if (Time.time < _replanAt || !needsNewPath)
            return;

        if (reachedDestination && _launcher.IsInRange(_target))
            Finish();
        else if (TryFindDestination())
            MoveToDestination();
        else
            Finish();
    }

    public override void Exit()
    {
        _movement.Stop();
        _movement.StoppingDistance = _previousStoppingDistance;
        base.Exit();
    }

    private bool CanMove => _agent.isActiveAndEnabled && _agent.isOnNavMesh &&
        !_agent.isOnOffMeshLink && !_movement.IsBusy;

    private bool ReadTarget()
    {
        if (_vision == null || !_vision.SeenPlayer || !_vision.SignalSourcePosition.HasValue)
            return false;

        _target = _vision.Target.position;
        _memory.SetLastKnownPlayerPosition(_target, _target - _agent.transform.position);
        return true;
    }

    private bool TryFindDestination()
    {
        Vector3 origin = _agent.nextPosition - Vector3.up * _agent.baseOffset;
        Vector3 away = Vector3.ProjectOnPlane(origin - _target, Vector3.up);
        float currentDistance = away.magnitude;
        away = currentDistance > 0.01f ? away / currentDistance : -_agent.transform.forward;
        float retreatDistance = Mathf.Clamp(_launcher.PreferredRange - currentDistance, 1.5f, 6f);
        float bestDistance = currentDistance + MinDistanceGain;
        var filter = new NavMeshQueryFilter { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };
        bool found = false;

        foreach (float angle in EscapeAngles)
        {
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            Vector3 candidate = origin + direction * retreatDistance;
            if (NavMesh.Raycast(origin, candidate, out NavMeshHit hit, filter))
            {
                float clearDistance = Vector3.Dot(hit.position - origin, direction) - PathBoundaryClearance;
                if (clearDistance < MinDistanceGain)
                    continue;
                candidate = origin + direction * clearDistance;
            }

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit floor, 0.6f, filter))
                continue;

            float distance = Vector3.ProjectOnPlane(floor.position - _target, Vector3.up).magnitude;
            if (distance <= bestDistance || NavMesh.Raycast(origin, floor.position, out _, filter))
                continue;

            bestDistance = distance;
            _destination = floor.position;
            found = true;
        }

        return found;
    }

    private void MoveToDestination()
    {
        _pathTarget = _target;
        _replanAt = Time.time + 0.35f;
        _movement.MoveTo(_destination, _launcher.RetreatSpeed);
    }

    private void Finish()
    {
        _movement.Stop();
        IsFinished = true;
        _nextPathAttempt = Time.time + PathRetryDelay;
    }
}
