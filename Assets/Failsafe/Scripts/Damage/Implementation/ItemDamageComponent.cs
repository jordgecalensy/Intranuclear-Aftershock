using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class ItemDamageComponent : MonoBehaviour
{
    [SerializeField]
    private Damage_ScriptableObject _config;

    [Header("Incoming Object Filter")]
    [SerializeField]
    private LayerMask _damagingObjectMask = ~0;

    [Header("Effects Applied To This Object")]
    [SerializeField]
    private EffectBundle _impactEffects;

    [Header("Damage Cooldown")]
    [SerializeField]
    private float _sameSourceCooldown = 0.25f;

    [Header("Debug")]
    [SerializeField]
    private bool _debugLogs = true;

    private readonly List<Collider> _ownColliders = new();
    private readonly Dictionary<Transform, float> _lastHitTimes = new();

    private Rigidbody _ownRigidbody;
    private IEffectApplicationService _effects;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _effects = effects;
    }

    private void Awake()
    {
        _ownRigidbody = GetComponentInParent<Rigidbody>();

        _ownColliders.Clear();
        GetComponentsInChildren(true, _ownColliders);
    }

    private void Start()
    {
        if (_ownColliders.Count == 0)
            Debug.LogError("[ItemDamageComponent] На игроке не найден ни один Collider.", this);

        if (_config == null)
            Debug.LogError("[ItemDamageComponent] Damage config не назначен.", this);

        ResolveEffectsIfNeeded();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        Collider ownHitCollider = GetOwnColliderFromCollision(collision);
        Collider sourceCollider = GetSourceColliderFromCollision(collision);

        if (ownHitCollider == null || sourceCollider == null)
            return;

        Vector3 point = GetCollisionPoint(collision);
        Vector3 direction = GetDirectionFromSource(sourceCollider);
        float speed = GetImpactSpeed(collision, sourceCollider);
        Rigidbody sourceRigidbody = sourceCollider.attachedRigidbody;

        ApplyImpactFromSource(
            sourceCollider,
            sourceRigidbody,
            ownHitCollider,
            point,
            direction,
            speed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        if (IsOwnCollider(other))
            return;

        Collider ownHitCollider = GetPrimaryOwnCollider();

        if (ownHitCollider == null)
            return;

        Rigidbody sourceRigidbody = other.attachedRigidbody;

        Vector3 point = ownHitCollider.ClosestPoint(other.transform.position);
        Vector3 direction = GetDirectionFromSource(other);
        float speed = GetSourceSpeed(other);

        ApplyImpactFromSource(
            other,
            sourceRigidbody,
            ownHitCollider,
            point,
            direction,
            speed);
    }

    private void ApplyImpactFromSource(
        Collider sourceCollider,
        Rigidbody sourceRigidbody,
        Collider ownHitCollider,
        Vector3 point,
        Vector3 direction,
        float speed)
    {
        if (_config == null)
            return;

        if (_impactEffects == null)
            return;

        if (sourceCollider == null || ownHitCollider == null)
            return;

        if (!IsDamagingObjectAllowed(sourceCollider))
        {
            if (_debugLogs)
                Debug.Log($"[ItemDamageComponent] Source ignored by mask: {sourceCollider.name}", sourceCollider);

            return;
        }

        Transform sourceRoot = GetSourceRoot(sourceCollider);

        if (sourceRoot == null)
            return;

        if (IsOnCooldown(sourceRoot))
            return;

        ResolveEffectsIfNeeded();

        if (_effects == null)
        {
            if (_debugLogs)
                Debug.LogWarning("[ItemDamageComponent] IEffectApplicationService не найден.", this);

            return;
        }

        float sourceMass = sourceRigidbody != null
            ? sourceRigidbody.mass
            : 1f;

        float damageAmount = speed * sourceMass * _config.DamageMultiplier;

        if (damageAmount <= _config.DamageThreshhold)
            return;

        damageAmount = Mathf.Min(damageAmount, _config.MaxDamage);

        var context = new EffectContext(
            sourceCollider.gameObject,
            ownHitCollider,
            point,
            Vector3.up,
            direction,
            damageAmount);

        _effects.Apply(_impactEffects, context);

        _lastHitTimes[sourceRoot] = Time.time;

        if (_debugLogs)
        {
            Debug.Log(
                $"[ItemDamageComponent] Impact effects applied TO SELF. Source: {sourceCollider.name}. Target: {ownHitCollider.name}. Power: {damageAmount}",
                ownHitCollider);
        }
    }

    private Collider GetOwnColliderFromCollision(Collision collision)
    {
        if (collision.contacts != null)
        {
            for (int i = 0; i < collision.contacts.Length; i++)
            {
                ContactPoint contact = collision.contacts[i];

                if (IsOwnCollider(contact.thisCollider))
                    return contact.thisCollider;

                if (IsOwnCollider(contact.otherCollider))
                    return contact.otherCollider;
            }
        }

        return GetPrimaryOwnCollider();
    }

    private Collider GetSourceColliderFromCollision(Collision collision)
    {
        if (collision.contacts != null)
        {
            for (int i = 0; i < collision.contacts.Length; i++)
            {
                ContactPoint contact = collision.contacts[i];

                if (contact.thisCollider != null && !IsOwnCollider(contact.thisCollider))
                    return contact.thisCollider;

                if (contact.otherCollider != null && !IsOwnCollider(contact.otherCollider))
                    return contact.otherCollider;
            }
        }

        if (collision.collider != null && !IsOwnCollider(collision.collider))
            return collision.collider;

        return null;
    }

    private Collider GetPrimaryOwnCollider()
    {
        for (int i = 0; i < _ownColliders.Count; i++)
        {
            Collider collider = _ownColliders[i];

            if (collider != null && collider.enabled)
                return collider;
        }

        return null;
    }

    private bool IsOwnCollider(Collider collider)
    {
        if (collider == null)
            return false;

        for (int i = 0; i < _ownColliders.Count; i++)
        {
            if (_ownColliders[i] == collider)
                return true;
        }

        if (_ownRigidbody != null && collider.attachedRigidbody == _ownRigidbody)
            return true;

        return false;
    }

    private bool IsDamagingObjectAllowed(Collider sourceCollider)
    {
        if (sourceCollider == null)
            return false;

        if (IsLayerAllowed(sourceCollider.gameObject.layer))
            return true;

        if (sourceCollider.attachedRigidbody != null &&
            IsLayerAllowed(sourceCollider.attachedRigidbody.gameObject.layer))
            return true;

        if (sourceCollider.transform.root != null &&
            IsLayerAllowed(sourceCollider.transform.root.gameObject.layer))
            return true;

        return false;
    }

    private bool IsLayerAllowed(int layer)
    {
        return (_damagingObjectMask.value & (1 << layer)) != 0;
    }

    private bool IsOnCooldown(Transform sourceRoot)
    {
        if (sourceRoot == null)
            return true;

        if (!_lastHitTimes.TryGetValue(sourceRoot, out float lastHitTime))
            return false;

        return Time.time < lastHitTime + Mathf.Max(0.01f, _sameSourceCooldown);
    }

    private Transform GetSourceRoot(Collider sourceCollider)
    {
        if (sourceCollider == null)
            return null;

        if (sourceCollider.attachedRigidbody != null)
            return sourceCollider.attachedRigidbody.transform;

        return sourceCollider.transform.root;
    }

    private Vector3 GetCollisionPoint(Collision collision)
    {
        if (collision.contacts != null && collision.contacts.Length > 0)
            return collision.contacts[0].point;

        return transform.position;
    }

    private Vector3 GetDirectionFromSource(Collider sourceCollider)
    {
        if (sourceCollider == null)
            return transform.forward;

        Vector3 direction = transform.position - sourceCollider.transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            return transform.forward;

        return direction.normalized;
    }

    private float GetImpactSpeed(Collision collision, Collider sourceCollider)
    {
        float relativeSpeed = collision.relativeVelocity.magnitude;
        float sourceSpeed = GetSourceSpeed(sourceCollider);

        return Mathf.Max(relativeSpeed, sourceSpeed);
    }

    private float GetSourceSpeed(Collider sourceCollider)
    {
        if (sourceCollider == null)
            return 0f;

        Rigidbody sourceRigidbody = sourceCollider.attachedRigidbody;

        if (sourceRigidbody == null)
            return 0f;

        return sourceRigidbody.linearVelocity.magnitude;
    }

    private void ResolveEffectsIfNeeded()
    {
        if (_effects != null)
            return;

        LifetimeScope scope = GetComponentInParent<LifetimeScope>();

        if (scope == null)
            scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);

        if (scope == null || scope.Container == null)
            return;

        try
        {
            _effects = scope.Container.Resolve<IEffectApplicationService>();
        }
        catch
        {
            _effects = null;
        }
    }
}