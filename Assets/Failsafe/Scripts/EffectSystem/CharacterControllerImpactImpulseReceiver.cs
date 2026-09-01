using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [DisallowMultipleComponent]
    public class CharacterControllerImpactImpulseReceiver : MonoBehaviour, IImpactImpulseReceiver
    {
        [Header("Target")]
        [SerializeField] private CharacterController _characterController;

        [Header("Impulse")]
        [Tooltip("Условная масса. Чем больше значение, тем слабее отталкивание.")]
        [SerializeField] private float _mass = 1f;

        [Tooltip("Если true, новые импульсы складываются с текущей скоростью отталкивания.")]
        [SerializeField] private bool _accumulateImpulses = true;

        [Tooltip("Максимальная горизонтальная скорость от импульса.")]
        [SerializeField] private float _maxHorizontalSpeed = 12f;

        [Header("Damping")]
        [SerializeField] private float _horizontalDamping = 8f;
        [SerializeField] private float _gravity = 20f;

        [Header("Debug")]
        [SerializeField] private bool _log;

        private Vector3 _velocity;

        private void Awake()
        {
            if (_characterController == null)
                _characterController = GetComponent<CharacterController>() ??
                                       GetComponentInChildren<CharacterController>(true);
        }

        private void Update()
        {
            if (_velocity.sqrMagnitude <= 0.0001f)
                return;

            float deltaTime = Time.deltaTime;

            ApplyGravity(deltaTime);
            Move(deltaTime);
            DampHorizontal(deltaTime);
        }

        public void AddImpactImpulse(
            Vector3 impulse,
            Vector3 impactPoint,
            GameObject source)
        {
            float mass = Mathf.Max(0.01f, _mass);
            Vector3 velocityChange = impulse / mass;

            if (!_accumulateImpulses)
                _velocity = Vector3.zero;

            _velocity += velocityChange;
            ClampHorizontalVelocity();

            if (_log)
            {
                EffectLog.Info(EffectLog.Physics,
                    $"[CharacterControllerImpactImpulseReceiver] {name}: impulse {impulse}, velocity {_velocity}",
                    this);
            }
        }

        private void ApplyGravity(float deltaTime)
        {
            if (_characterController != null &&
                _characterController.enabled &&
                _characterController.isGrounded &&
                _velocity.y < 0f)
            {
                _velocity.y = 0f;
                return;
            }

            _velocity.y -= _gravity * deltaTime;
        }

        private void Move(float deltaTime)
        {
            Vector3 displacement = _velocity * deltaTime;

            if (_characterController != null && _characterController.enabled)
            {
                CollisionFlags flags = _characterController.Move(displacement);

                if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f)
                    _velocity.y = 0f;

                if ((flags & CollisionFlags.Below) != 0 && _velocity.y < 0f)
                    _velocity.y = 0f;

                return;
            }

            transform.position += displacement;
        }

        private void DampHorizontal(float deltaTime)
        {
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);

            float damping = Mathf.Max(0f, _horizontalDamping);
            float t = 1f - Mathf.Exp(-damping * deltaTime);

            horizontal = Vector3.Lerp(horizontal, Vector3.zero, t);

            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;

            if (Mathf.Abs(_velocity.y) < 0.01f &&
                horizontal.sqrMagnitude < 0.0001f)
            {
                _velocity = Vector3.zero;
            }
        }

        private void ClampHorizontalVelocity()
        {
            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            float maxSpeed = Mathf.Max(0f, _maxHorizontalSpeed);

            if (maxSpeed <= 0f)
                return;

            if (horizontal.magnitude <= maxSpeed)
                return;

            horizontal = horizontal.normalized * maxSpeed;

            _velocity.x = horizontal.x;
            _velocity.z = horizontal.z;
        }
    }
}