using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class Projectile : MonoBehaviour
{
    [Header("Аудио Снаряда")]
    [Tooltip("Зацикленный звук пролета снаряда")]
    [SerializeField] private EventReference _flybySound;

    [Tooltip("Звук попадания/взрыва")]
    [SerializeField] private EventReference _impactSound;

    private float _speed;
    private float _maxLifetime;
    private LayerMask _hitMask;
    private Vector3 _startPosition;
    private Vector3 _previousPosition;
    private Transform _sourceRoot;
    private bool _hasImpacted;
    private readonly RaycastHit[] _sweepHits = new RaycastHit[8];
    private float _power = 1f;
    private float _impactForce;
    private float _flightFieldRadius;
    private float _flightFieldImpulse;

    private GameObject _source;
    private EffectBundle _impactEffects;
    private EffectBundle _impactPhysicsEffects;
    private IEffectApplicationService _effects;

    private EventInstance _flybyInstance;
    private readonly HashSet<Rigidbody> _flightFieldBodies =
        new HashSet<Rigidbody>();

    public void Initialize(
        float speed,
        float range,
        LayerMask mask,
        float power,
        float impactForce,
        float flightFieldRadius,
        float flightFieldImpulse,
        GameObject source,
        EffectBundle impactEffects,
        EffectBundle impactPhysicsEffects,
        IEffectApplicationService effects)
    {
        _speed = Mathf.Max(0.01f, speed);
        _hitMask = mask;
        _power = Mathf.Max(0f, power);
        _impactForce = Mathf.Max(0f, impactForce);
        _flightFieldRadius = Mathf.Max(0f, flightFieldRadius);
        _flightFieldImpulse = Mathf.Max(0f, flightFieldImpulse);
        _source = source;
        var sourceScope = source != null
            ? source.GetComponentInParent<VContainer.Unity.LifetimeScope>()
            : null;
        _sourceRoot = sourceScope != null ? sourceScope.transform : source != null ? source.transform : null;
        _hasImpacted = false;
        _impactEffects = impactEffects;
        _impactPhysicsEffects = impactPhysicsEffects;
        _effects = effects;

        _maxLifetime = range / _speed;
        _startPosition = transform.position;
        _previousPosition = transform.position;
        _flightFieldBodies.Clear();

        StartFlybySound();

        Destroy(gameObject, _maxLifetime);
    }

    private void Update()
    {
        if (_hasImpacted) return;

        Vector3 previousPosition = transform.position;
        _previousPosition = previousPosition;
        float remainingRange = Mathf.Max(0f, _speed * _maxLifetime - Vector3.Distance(_startPosition, previousPosition));
        float travel = Mathf.Min(_speed * Time.deltaTime, remainingRange);
        Vector3 currentPosition = previousPosition + transform.forward * travel;

        if (TryFindImpact(previousPosition, currentPosition, out RaycastHit hit))
        {
            transform.position = hit.point;
            ApplyFlightPhysicsField(previousPosition, hit.point);
            Impact(hit.collider, hit.point);
            return;
        }

        transform.position = currentPosition;
        ApplyFlightPhysicsField(previousPosition, currentPosition);

        if (Vector3.Distance(_startPosition, transform.position) >= _speed * _maxLifetime)
            Destroy(gameObject);
    }

    private void ApplyFlightPhysicsField(
        Vector3 previousPosition,
        Vector3 currentPosition)
    {
        if (_flightFieldRadius <= 0f || _flightFieldImpulse <= 0f)
            return;

        Collider[] colliders = Physics.OverlapCapsule(
            previousPosition,
            currentPosition,
            _flightFieldRadius,
            ~0,
            QueryTriggerInteraction.Ignore);

        Transform sourceRoot = _source != null
            ? _source.transform.root
            : null;
        Transform projectileRoot = transform.root;

        for (int index = 0; index < colliders.Length; index++)
        {
            Collider collider = colliders[index];

            if (collider == null || IsLayerInMask(collider.gameObject.layer, _hitMask))
                continue;

            Rigidbody body = ResolveRigidbody(collider);

            if (body == null ||
                body.isKinematic ||
                IsInHierarchy(body.transform, sourceRoot) ||
                IsInHierarchy(body.transform, projectileRoot) ||
                !_flightFieldBodies.Add(body))
            {
                continue;
            }

            Vector3 closestPoint = ClosestPointOnSegment(
                body.worldCenterOfMass,
                previousPosition,
                currentPosition);
            Vector3 direction = body.worldCenterOfMass - closestPoint;
            direction.y = Mathf.Max(0f, direction.y);

            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;

            body.WakeUp();
            body.AddForce(
                direction.normalized * _flightFieldImpulse,
                ForceMode.Impulse);
        }
    }

    private static Rigidbody ResolveRigidbody(Collider collider)
    {
        if (collider == null)
            return null;

        return collider.attachedRigidbody ??
               collider.GetComponentInParent<Rigidbody>();
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static bool IsInHierarchy(Transform candidate, Transform root)
    {
        return root != null &&
               (candidate == root || candidate.IsChildOf(root));
    }

    private static Vector3 ClosestPointOnSegment(
        Vector3 point,
        Vector3 start,
        Vector3 end)
    {
        Vector3 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;

        if (segmentLengthSquared <= 0.0001f)
            return start;

        float distanceAlongSegment = Mathf.Clamp01(
            Vector3.Dot(point - start, segment) / segmentLengthSquared);

        return start + segment * distanceAlongSegment;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasImpacted || other == null || other.isTrigger || IsSourceCollider(other)) return;

        // При одновременных контактах тела и уязвимой точки выбираем первый по пути снаряда.
        if (TryFindImpact(_previousPosition, transform.position, out RaycastHit hit))
            Impact(hit.collider, hit.point);
        else
            Impact(other, other.ClosestPoint(transform.position));
    }

    private bool IsSourceCollider(Collider other) => other.transform.IsChildOf(transform) ||
        (_sourceRoot != null && other.transform.IsChildOf(_sourceRoot));

    private bool TryFindImpact(Vector3 from, Vector3 to, out RaycastHit closest)
    {
        closest = default;
        Vector3 displacement = to - from;
        float distance = displacement.magnitude;
        if (distance <= 0f) return false;

        RaycastHit[] hits = _sweepHits;
        int count = Physics.RaycastNonAlloc(from, displacement / distance, hits, distance,
            ~0, QueryTriggerInteraction.Ignore);
        if (count == hits.Length)
        {
            hits = Physics.RaycastAll(from, displacement / distance, distance,
                ~0, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (IsSourceCollider(hits[i].collider) ||
                (found && hits[i].distance >= closest.distance)) continue;
            closest = hits[i];
            found = true;
        }
        return found;
    }

    private void Impact(Collider other, Vector3 point)
    {
        if (_hasImpacted) return;
        _hasImpacted = true;
        transform.position = point;

        bool isValidTarget = (_hitMask.value & (1 << other.gameObject.layer)) > 0;

        if (isValidTarget)
        {
            var impactContext = new EffectContext(
                _source,
                other,
                point,
                -transform.forward,
                transform.forward,
                _power);

            _effects?.Apply(_impactEffects, impactContext);

            if (_impactForce > 0f && _impactPhysicsEffects != null)
            {
                var physicsContext = new EffectContext(
                    _source,
                    other,
                    point,
                    -transform.forward,
                    transform.forward,
                    _impactForce);

                _effects?.Apply(_impactPhysicsEffects, physicsContext);
            }
        }

        PlayImpactSound();
        Destroy(gameObject);
    }

    private void StartFlybySound()
    {
        if (_flybySound.IsNull)
            return;

        _flybyInstance = RuntimeManager.CreateInstance(_flybySound);
        RuntimeManager.AttachInstanceToGameObject(_flybyInstance, transform);
        _flybyInstance.start();
    }

    private void PlayImpactSound()
    {
        if (!_impactSound.IsNull)
            RuntimeManager.PlayOneShot(_impactSound, transform.position);
    }

    private void OnDestroy()
    {
        if (_flybyInstance.isValid())
        {
            _flybyInstance.stop(STOP_MODE.ALLOWFADEOUT);
            _flybyInstance.release();
            _flybyInstance.clearHandle();
        }
    }
}
