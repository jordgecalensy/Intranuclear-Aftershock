using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;

public class MeleeChargeState : BehaviorState
{
    private enum Phase { Prepare, Dash, Brake, WallStun }

    private struct Contact
    {
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public bool IsWall;
        public bool IsObstacle;
        public Rigidbody PushBody;
    }

    private const float ContactSkin = 0.02f;
    private const float ContactTolerance = 0.05f;
    private const float BlockedMovementGrace = 0.15f;
    private readonly Enemy _owner;
    private readonly Sensor[] _sensors;
    private readonly EnemyMovement _movement;
    private readonly EnemyAnimator _animator;
    private readonly Enemy_ScriptableObject _config;
    private readonly CapsuleCollider _bodyCollider;
    private readonly EnemyMemory _memory;
    private readonly EffectApplicationServiceResolver _effectResolver;
    private readonly Collider[] _overlaps = new Collider[16];
    private readonly RaycastHit[] _sweepHits = new RaycastHit[16];
    private readonly List<Contact> _pushContacts = new List<Contact>();
    private readonly HashSet<Rigidbody> _pushedBodies = new HashSet<Rigidbody>();
    private Phase _phase;
    private Vector3 _direction;
    private float _phaseTime;
    private float _nextChargeTime;
    private float _distanceMoved;
    private float _dashDistance;
    private float _dashSpeed;
    private float _brakeSpeed;
    private float _blockedTime;
    private bool _directMovementActive;
    private bool _hasHitPlayer;
    private bool _wasInterrupted;
    private bool _startRequested;
    private bool _hasStartedCharge;

    public bool IsFinished { get; private set; }

    public MeleeChargeState(Enemy owner, Sensor[] sensors, EnemyMovement movement,
        EnemyAnimator animator, Enemy_ScriptableObject config, CapsuleCollider bodyCollider,
        EnemyMemory memory = null)
    {
        _owner = owner;
        _sensors = sensors;
        _movement = movement;
        _animator = animator;
        _config = config;
        _bodyCollider = bodyCollider;
        _memory = memory;
        _effectResolver = new EffectApplicationServiceResolver(owner.gameObject);
    }

    public bool CanStart() => RequestStart(false);

    public bool CanStartOnFirstSight()
    {
        _startRequested = false;
        return !_hasStartedCharge && RequestStart(true);
    }

    private bool RequestStart(bool ignoreRange)
    {
        _startRequested = false;
        if (!_config.EnableCharge || Time.time < _nextChargeTime ||
            !_owner.isActiveAndEnabled || _owner.Health == null || _owner.Health.IsDead ||
            _bodyCollider == null || !_bodyCollider.enabled || _movement.IsBusy)
            return false;

        Transform target = FindVisibleTarget();
        if (target == null) return false;

        float distance = Vector3.Distance(_owner.transform.position, target.position);
        _startRequested = ignoreRange ||
            (distance >= _config.ChargeMinRange && distance <= _config.ChargeMaxRange);
        return _startRequested;
    }

    public override void Enter()
    {
        base.Enter();
        IsFinished = _wasInterrupted || !_startRequested;
        _wasInterrupted = false;
        _startRequested = false;
        if (IsFinished) return;

        _hasStartedCharge = true;
        Transform target = FindVisibleTarget();
        if (target != null)
            _memory?.SetLastKnownPlayerPosition(target.position, target.position - _owner.transform.position);

        _movement.Stop();
        _phase = Phase.Prepare;
        _phaseTime = 0f;
        _distanceMoved = 0f;
        _blockedTime = 0f;
        _hasHitPlayer = false;
        _pushedBodies.Clear();
        _nextChargeTime = Time.time + Mathf.Max(0f, _config.ChargeCooldown);
        _animator.BeginChargePreparation();
    }

    public override void Update()
    {
        if (IsFinished || Time.deltaTime <= 0f) return;

        switch (_phase)
        {
            case Phase.Prepare:
                UpdatePreparation();
                break;
            case Phase.Dash:
                UpdateDash();
                break;
            case Phase.Brake:
                UpdateBrake();
                break;
            case Phase.WallStun:
                _phaseTime += Time.deltaTime;
                if (_phaseTime >= _config.ChargeWallStunDuration)
                {
                    _animator.IsInStun(false);
                    Finish();
                }
                break;
        }
    }

    public override void Exit()
    {
        _wasInterrupted = !IsFinished;
        EndMovement();
        if (_phase == Phase.WallStun)
            _animator.IsInStun(false);
        _animator.FinishCharge();
        base.Exit();
    }

    private Transform FindVisibleTarget()
    {
        foreach (Sensor sensor in _sensors)
        {
            if (sensor is VisualSensor visual && visual.IsActivated() &&
                visual.Target != null && visual.SignalSourcePosition.HasValue)
                return visual.Target;
        }
        return null;
    }

    private void UpdatePreparation()
    {
        Transform target = FindVisibleTarget();
        if (target == null)
        {
            Finish();
            return;
        }

        _memory?.SetLastKnownPlayerPosition(target.position, target.position - _owner.transform.position);
        _movement.LookAt(target.position);
        _phaseTime += Time.deltaTime;
        if (_phaseTime < _config.ChargePrepareDuration) return;

        _direction = target.position - _owner.transform.position;
        _direction.y = 0f;
        _dashDistance = Mathf.Min(Mathf.Max(0f, _config.ChargeDistance), _direction.magnitude + 1.5f);
        _dashSpeed = Mathf.Max(0f, _config.ChargeSpeed * _movement.SpeedMultiplier);
        if (_direction.sqrMagnitude < 0.001f || _dashDistance <= 0f ||
            _dashSpeed <= 0f || !_movement.BeginDirectMovement())
        {
            Finish();
            return;
        }

        _direction.Normalize();
        _owner.transform.rotation = Quaternion.LookRotation(_direction);
        _directMovementActive = true;
        _phase = Phase.Dash;
        _phaseTime = 0f;
        _animator.PlayChargeDash();
    }

    private void UpdateDash()
    {
        Transform target = FindVisibleTarget();
        if (target != null)
        {
            Vector3 targetDirection = target.position - _owner.transform.position;
            targetDirection.y = 0f;
            _memory?.SetLastKnownPlayerPosition(target.position, targetDirection);
            if (targetDirection.sqrMagnitude > 0.001f && Vector3.Dot(_direction, targetDirection) > 0f)
            {
                _direction = Vector3.RotateTowards(_direction, targetDirection.normalized,
                    Mathf.Max(0f, _config.ChargeTurnSpeed) * Mathf.Deg2Rad * Time.deltaTime, 0f);
                _owner.transform.rotation = Quaternion.LookRotation(_direction);
            }
        }

        float distance = Mathf.Min(_dashSpeed * Time.deltaTime, _dashDistance - _distanceMoved);
        MoveDistance(Mathf.Max(0f, distance));
        if (_phase == Phase.Dash && _distanceMoved >= _dashDistance - 0.001f)
            BeginBrake(_dashSpeed);
    }

    private void UpdateBrake()
    {
        float duration = Mathf.Max(0.001f, _config.ChargeRecoveryDuration);
        float middleTime = _phaseTime + Time.deltaTime * 0.5f;
        _phaseTime += Time.deltaTime;
        float speed = _brakeSpeed * (1f - Mathf.Clamp01(middleTime / duration));
        float remainingDistance = Mathf.Max(0f, _config.ChargeDistance - _distanceMoved);
        if (_directMovementActive)
            MoveDistance(Mathf.Min(speed * Time.deltaTime, remainingDistance));

        if (_phase == Phase.Brake && _phaseTime >= duration)
            Finish();
    }

    private void MoveDistance(float distance)
    {
        if (distance <= 0f)
        {
            if (_phase == Phase.Dash)
                BeginBrake(_dashSpeed);
            else
                EndMovement();
            return;
        }

        bool hasContact = FindContact(distance + ContactSkin, out Contact contact);
        float safeDistance = hasContact
            ? Mathf.Clamp(contact.Distance - ContactSkin, 0f, distance)
            : distance;
        Vector3 actualMovement = _movement.MoveDirect(_direction * safeDistance);
        _distanceMoved += actualMovement.magnitude;
        float forwardDistance = Mathf.Max(0f, Vector3.Dot(actualMovement, _direction));
        float blockingDistance = hasContact ? contact.Distance : distance + ContactSkin;
        PushObjectsBefore(Mathf.Min(blockingDistance, forwardDistance + ContactSkin));

        if (hasContact && forwardDistance >= safeDistance - ContactTolerance)
        {
            if (contact.IsWall)
                EnterWallStun();
            else if (contact.IsObstacle)
                BeginBrake(0f);
            else
                HitPlayer(contact);
        }
        else if (safeDistance > 0.001f && forwardDistance < safeDistance * 0.2f)
        {
            _blockedTime += Time.deltaTime;
            if (_blockedTime >= BlockedMovementGrace)
            {
                // NavMesh remains authoritative at ledges and disconnected surfaces.
                if (_phase == Phase.Brake)
                    EndMovement();
                else
                    BeginBrake(0f);
            }
        }
        else
            _blockedTime = 0f;
    }

    private void BeginBrake(float speed)
    {
        _phase = Phase.Brake;
        _phaseTime = 0f;
        _brakeSpeed = speed;
        if (speed <= 0f) EndMovement();
    }

    private void HitPlayer(Contact contact)
    {
        _hasHitPlayer = true;
        BeginBrake(0f);
        _effectResolver.TryResolve(Time.unscaledTime, true);
        if (_config.ChargeHitEffects == null || _effectResolver.Service == null)
        {
            Debug.LogWarning("MeleeChargeState: charge hit effects or effect service are missing.", _owner);
            return;
        }

        _effectResolver.Service.Apply(_config.ChargeHitEffects, new EffectContext(
            _owner.gameObject, contact.Collider, contact.Point, contact.Normal, _direction));
    }

    private void EnterWallStun()
    {
        EndMovement();
        _phase = Phase.WallStun;
        _phaseTime = 0f;
        _animator.FinishCharge();
        _animator.TryStun();
        _animator.IsInStun(true);
    }

    private void EndMovement()
    {
        if (_directMovementActive)
        {
            _movement.EndDirectMovement();
            _directMovementActive = false;
        }
        _movement.Stop();
    }

    private void Finish()
    {
        EndMovement();
        _animator.FinishCharge();
        IsFinished = true;
    }

    private bool FindContact(float distance, out Contact closest)
    {
        GetCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);
        int mask = _config.ChargeObstacleLayers.value | _config.ChargeTargetLayers.value |
            _config.ChargePushableLayers.value;
        closest = default;
        bool found = false;
        _pushContacts.Clear();

        Collider[] overlaps = _overlaps;
        int overlapCount = Physics.OverlapCapsuleNonAlloc(pointA, pointB, radius,
            overlaps, mask, QueryTriggerInteraction.Ignore);
        if (overlapCount == overlaps.Length)
        {
            overlaps = Physics.OverlapCapsule(pointA, pointB, radius, mask, QueryTriggerInteraction.Ignore);
            overlapCount = overlaps.Length;
        }

        Vector3 center = (pointA + pointB) * 0.5f;
        float bottom = Mathf.Min(pointA.y, pointB.y) - radius;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider other = overlaps[i];
            if (!CreateContact(other, out Contact contact)) continue;

            Vector3 point = other.ClosestPoint(center);
            if ((contact.IsObstacle || contact.PushBody != null) &&
                (point.y <= bottom + ContactSkin * 2f ||
                !IsMovingInto(other, center))) continue;

            contact.Distance = 0f;
            contact.Point = point;
            contact.Normal = -_direction;
            CollectContact(contact, ref found, ref closest);
        }

        RaycastHit[] hits = _sweepHits;
        int hitCount = Physics.CapsuleCastNonAlloc(pointA, pointB, radius, _direction,
            hits, distance, mask, QueryTriggerInteraction.Ignore);
        if (hitCount == hits.Length)
        {
            hits = Physics.CapsuleCastAll(pointA, pointB, radius, _direction,
                distance, mask, QueryTriggerInteraction.Ignore);
            hitCount = hits.Length;
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (!CreateContact(hit.collider, out Contact contact)) continue;
            if (contact.IsObstacle && (hit.normal.y > 0.65f ||
                (hit.distance <= ContactSkin && !IsMovingInto(hit.collider, center)))) continue;

            contact.Distance = hit.distance;
            contact.Point = hit.point;
            contact.Normal = hit.normal;
            CollectContact(contact, ref found, ref closest);
        }
        return found;
    }

    private bool CreateContact(Collider other, out Contact contact)
    {
        contact = default;
        if (other == null || other.isTrigger || other.transform.IsChildOf(_owner.transform))
            return false;

        int layer = 1 << other.gameObject.layer;
        bool obstacleLayer = (_config.ChargeObstacleLayers.value & layer) != 0;
        bool pushableLayer = (_config.ChargePushableLayers.value & layer) != 0;
        contact.Collider = other;

        if (!obstacleLayer && (_config.ChargeTargetLayers.value & layer) != 0)
            return !_hasHitPlayer;

        if (other.GetComponentInParent<Failsafe.Player.View.PlayerView>() != null ||
            other.GetComponentInParent<Enemy>() != null)
            return false;

        Rigidbody body = other.attachedRigidbody;
        if (body != null && !body.isKinematic)
        {
            if (_pushedBodies.Contains(body)) return false;
            if (!obstacleLayer && !pushableLayer) return false;

            if (pushableLayer && CanPush(body))
                contact.PushBody = body;
            else
                contact.IsObstacle = true;
            return true;
        }

        contact.IsWall = obstacleLayer && body == null;
        contact.IsObstacle = obstacleLayer || pushableLayer;
        return contact.IsObstacle;
    }

    private bool CanPush(Rigidbody body)
    {
        if (_config.ChargePushSpeed <= 0f || body.mass > _config.ChargePushMaxMass) return false;
        RigidbodyConstraints frozenHorizontal = RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ;
        if ((body.constraints & frozenHorizontal) == frozenHorizontal) return false;

        Bounds bounds = default;
        bool foundCollider = false;
        foreach (Collider collider in body.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
            if (foundCollider)
                bounds.Encapsulate(collider.bounds);
            else
            {
                bounds = collider.bounds;
                foundCollider = true;
            }
        }
        return foundCollider && Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) <=
            _config.ChargePushMaxSize;
    }

    private bool IsMovingInto(Collider other, Vector3 bodyCenter)
    {
        Transform bodyTransform = _bodyCollider.transform;
        if (Physics.ComputePenetration(_bodyCollider, bodyTransform.position, bodyTransform.rotation,
            other, other.transform.position, other.transform.rotation, out Vector3 separation, out _))
            return separation.y <= 0.65f && Vector3.Dot(_direction, separation) < -0.05f;

        Vector3 towardContact = other.ClosestPoint(bodyCenter) - bodyCenter;
        towardContact.y = 0f;
        return towardContact.sqrMagnitude < 0.0001f ||
            Vector3.Dot(_direction, towardContact.normalized) > 0.05f;
    }

    private void CollectContact(Contact candidate, ref bool found, ref Contact closest)
    {
        if (candidate.PushBody != null)
            _pushContacts.Add(candidate);
        else
            ConsiderContact(candidate, ref found, ref closest);
    }

    private void PushObjectsBefore(float blockingDistance)
    {
        Vector3 bodyCenter = _bodyCollider.transform.TransformPoint(_bodyCollider.center);
        foreach (Contact contact in _pushContacts)
        {
            Rigidbody body = contact.PushBody;
            if (body == null || contact.Distance > blockingDistance + 0.0001f ||
                !_pushedBodies.Add(body)) continue;

            Vector3 outward = body.worldCenterOfMass - bodyCenter;
            outward.y = 0f;
            Vector3 pushDirection = (_direction + outward.normalized + Vector3.up * 0.2f).normalized;
            body.AddForce(pushDirection * Mathf.Max(0f, _config.ChargePushSpeed), ForceMode.VelocityChange);
        }
    }

    private static void ConsiderContact(Contact candidate, ref bool found, ref Contact closest)
    {
        bool nearer = !found || candidate.Distance < closest.Distance - 0.0001f;
        bool wallAtSameDistance = found && candidate.IsWall && !closest.IsWall &&
            Mathf.Abs(candidate.Distance - closest.Distance) <= 0.0001f;
        if (!nearer && !wallAtSameDistance) return;

        closest = candidate;
        found = true;
    }

    private void GetCapsule(out Vector3 pointA, out Vector3 pointB, out float radius)
    {
        Transform bodyTransform = _bodyCollider.transform;
        Vector3 scale = bodyTransform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        int axis = _bodyCollider.direction;
        Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        float radiusScale = Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
        float bodyRadius = _bodyCollider.radius * radiusScale;
        radius = Mathf.Max(0.001f, bodyRadius - ContactSkin);
        float halfSegment = Mathf.Max(0f, _bodyCollider.height * scale[axis] * 0.5f - bodyRadius);
        Vector3 center = bodyTransform.TransformPoint(_bodyCollider.center);
        Vector3 offset = bodyTransform.rotation * localAxis * halfSegment;
        pointA = center - offset;
        pointB = center + offset;
    }
}
