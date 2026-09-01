using System;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Health;
using FMODUnity;
using UnityEngine;

namespace Failsafe.Scripts.Destruction
{
    [DisallowMultipleComponent]
    public sealed class BreakableObject : MonoBehaviour, IDamageable, IBreakable
    {
        [Header("Health")]
        [SerializeField, Min(0.01f)] private float _maxHealth = 100f;

        [Header("Model Parts")]
        [Tooltip("Child object containing the intact model, colliders and renderers.")]
        [SerializeField] private GameObject _intactRoot;
        [Tooltip("Separate child object containing the pre-fractured pieces.")]
        [SerializeField] private GameObject _fragmentsRoot;

        [Header("Debris")]
        [SerializeField] private bool _releaseFragmentRigidbodies = true;
        [SerializeField, Min(0f)] private float _fragmentImpulse = 2f;
        [SerializeField] private bool _destroyAfterLifetime = true;
        [SerializeField, Min(0f)] private float _debrisLifetime = 10f;

        [Header("FMOD")]
        [SerializeField] private EventReference _damageSound;
        [SerializeField] private EventReference _breakSound;

        public event Action<float> OnHealthChanged = delegate { };
        public event Action OnBroken = delegate { };

        private SimpleHealth _health;
        private DamageInfo _lastDamage;
        private bool _hasDamageContext;

        public float MaxHealth => Mathf.Max(0.01f, _maxHealth);
        public float CurrentHealth => _health?.CurrentHealth ?? MaxHealth;
        public bool IsBroken { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (_health == null)
                return;

            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnDeath -= HandleDeath;
        }

        public void TakeDamage(IDamage damage)
        {
            Initialize();

            if (IsBroken || damage == null)
                return;

            if (!TryResolveDamage(damage, out float amount))
                return;

            amount = Mathf.Max(0f, amount);

            if (amount <= 0f)
                return;

            if (damage is DamageInfo damageInfo)
            {
                _lastDamage = damageInfo;
                _hasDamageContext = true;
            }
            else
            {
                _lastDamage = default;
                _hasDamageContext = false;
            }

            _health.AddHealth(-amount);

            if (!IsBroken)
                PlayOneShot(_damageSound, ResolveSoundPosition());
        }

        public void Break()
        {
            Initialize();
            BreakInternal();
        }

        private void Initialize()
        {
            if (_health != null)
                return;

            _health = new SimpleHealth(MaxHealth);
            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnDeath += HandleDeath;

            if (_intactRoot != null && _intactRoot != gameObject)
                _intactRoot.SetActive(true);

            if (_fragmentsRoot != null && _fragmentsRoot != gameObject)
                _fragmentsRoot.SetActive(false);
        }

        private void HandleHealthChanged(float currentHealth)
        {
            OnHealthChanged.Invoke(currentHealth);
        }

        private void HandleDeath()
        {
            BreakInternal();
        }

        private void BreakInternal()
        {
            if (IsBroken)
                return;

            IsBroken = true;
            Vector3 soundPosition = ResolveSoundPosition();

            if (_intactRoot != null && _intactRoot != gameObject)
                _intactRoot.SetActive(false);

            if (_fragmentsRoot != null && _fragmentsRoot != gameObject)
            {
                _fragmentsRoot.SetActive(true);
                ReleaseFragments();
            }

            PlayOneShot(_breakSound, soundPosition);
            OnBroken.Invoke();
            ScheduleDebrisCleanup();
        }

        private void ReleaseFragments()
        {
            if (!_releaseFragmentRigidbodies || _fragmentsRoot == null)
                return;

            Rigidbody[] rigidbodies =
                _fragmentsRoot.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];

                if (rigidbody == null)
                    continue;

                rigidbody.isKinematic = false;
                rigidbody.detectCollisions = true;
                rigidbody.WakeUp();
                ApplyFragmentImpulse(rigidbody);
            }
        }

        private void ApplyFragmentImpulse(Rigidbody rigidbody)
        {
            if (!_hasDamageContext || _fragmentImpulse <= 0f)
                return;

            Vector3 direction = _lastDamage.Direction;
            Vector3 point = ResolveDamagePoint();
            Vector3 outward = rigidbody.worldCenterOfMass - point;

            if (outward.sqrMagnitude > 0.0001f)
                direction += outward.normalized;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            float power = Mathf.Max(0.01f, _lastDamage.Power);
            Vector3 impulse = direction.normalized * _fragmentImpulse * power;

            rigidbody.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

        private void ScheduleDebrisCleanup()
        {
            if (!_destroyAfterLifetime)
                return;

            float lifetime = Mathf.Max(0f, _debrisLifetime);

            if (lifetime <= 0f)
                Destroy(gameObject);
            else
                Destroy(gameObject, lifetime);
        }

        private Vector3 ResolveSoundPosition()
        {
            return _hasDamageContext
                ? ResolveDamagePoint()
                : transform.position;
        }

        private Vector3 ResolveDamagePoint()
        {
            if (!_hasDamageContext)
                return transform.position;

            return _lastDamage.Point == default
                ? transform.position
                : _lastDamage.Point;
        }

        private static bool TryResolveDamage(IDamage damage, out float amount)
        {
            switch (damage)
            {
                case DamageInfo damageInfo:
                    amount = damageInfo.Amount;
                    return true;
                case FlatDamage flatDamage:
                    amount = flatDamage.DamageAmount;
                    return true;
                case FireContactDamage contactDamage:
                    amount = contactDamage.Amount;
                    return true;
                case FireDotTickDamage dotDamage:
                    amount = dotDamage.Amount;
                    return true;
                case FireDamage fireDamage:
                    amount = fireDamage.DamagePerTick;
                    return true;
                default:
                    amount = 0f;
                    return false;
            }
        }

        private static void PlayOneShot(
            EventReference eventReference,
            Vector3 position)
        {
            if (eventReference.IsNull)
                return;

            RuntimeManager.PlayOneShot(eventReference, position);
        }

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(0.01f, _maxHealth);
            _fragmentImpulse = Mathf.Max(0f, _fragmentImpulse);
            _debrisLifetime = Mathf.Max(0f, _debrisLifetime);

            if (_intactRoot == gameObject)
            {
                Debug.LogWarning(
                    $"[{nameof(BreakableObject)}] Intact Root must be a child object, not the component owner.",
                    this);
            }

            if (_fragmentsRoot == gameObject)
            {
                Debug.LogWarning(
                    $"[{nameof(BreakableObject)}] Fragments Root must be a child object, not the component owner.",
                    this);
            }

            if (_intactRoot != null &&
                _fragmentsRoot != null &&
                _fragmentsRoot.transform.IsChildOf(_intactRoot.transform))
            {
                Debug.LogWarning(
                    $"[{nameof(BreakableObject)}] Fragments Root cannot be a child of Intact Root because it would stay disabled after breaking.",
                    this);
            }
        }
    }
}
