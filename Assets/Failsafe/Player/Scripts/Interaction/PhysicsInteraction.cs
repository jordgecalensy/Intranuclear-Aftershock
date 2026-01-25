using Failsafe.Player.Model;
using Failsafe.PlayerMovements;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace Failsafe.Player.Scripts.Interaction
{
    public class PhysicsInteraction : MonoBehaviour
    {
        [Inject] private PlayerModelParameters _playerModelParameters;

        [Header("Picking Up")]
        [SerializeField] private float _maxPickupDistance = 5f;

        [Header("Carrying")]
        [SerializeField] private float _carryingDistance = 2.5f;
        [SerializeField] private float _carrySpeed = 4f;

        [Tooltip("Индекс слоя (0–31), в который временно помещается переносимый объект.")]
        [SerializeField, Range(0, 31)] private int _carryingLayerIndex = 0;

        private ICarryUsable _carryUsable;
        private bool _useHeld;

        [Header("Throwing")]
        [Tooltip("[при зарядке броска] Линейное сокращение дистанции переноски с Carrying Distance до указанного значения.")]
        [SerializeField] private float _carryingDistanceShorteningTo = 0.8f;

        [Header("Rotation Hold")]
        [SerializeField] private AlignmentMode _alignMode = AlignmentMode.Camera; // ← смотрит туда же, куда игрок
        [SerializeField, Tooltip("Скорость выравнивания вращения (P-составляющая).")]
        private float _rotKp = 30f;
        [SerializeField, Tooltip("Демпфирование вращения (D-составляющая).")]
        private float _rotKd = 5f;

        public enum AlignmentMode
        {
            WorldZero,   // к (0,0,0)
            Camera,      // точь-в-точь как камера (включая крен)
            CameraNoRoll // направление как у камеры, но без крена (up = Vector3.up)
        }

        [Header("Additional Options")]
        [Tooltip("Хак от залипания на некоторых поверхностях.")]
        [SerializeField] private Vector3 _grabHelperVector = new Vector3(0f, 0.01f, 0f);

        [Header("Debug")]
        [SerializeField] public GameObject CarryingObject;
        [SerializeField] public Rigidbody CarryingBody;
        [SerializeField] private Transform _playerCameraTransform;
        [SerializeField, ReadOnly] private float _currentCarryingDistance;

        [SerializeField] private Vector3 _draggablePositionOffset;
        [SerializeField] private float _dragSpeed = 10f; // резерв

        private Quaternion _relativeRotation;

        [Inject] private InputHandler _inputHandler;
        [Inject] private PlayerHandsContainer _playerHandsContainer;

        private bool _isPreparingToThrow;
        private float _throwForceMultiplier;
        private const float _maxForceMultiplier = 3f;

        private int _cachedCarryingLayer;
        
        public bool IsDragging { get; private set; }

        private void Awake()
        {
            _currentCarryingDistance = _carryingDistance;

            if (!_playerCameraTransform)
            {
                Camera playerCamera = transform.root.GetComponentInChildren<Camera>();
                _playerCameraTransform = playerCamera.transform;
            }
        }

        private void Update()
        {

            if (_inputHandler.GrabOrDropAction.WasPressedThisFrame())
                GrabOrDrop();

            if (!IsDragging) return;

            // Use-start (edge)
            if (_inputHandler.UseTrigger.IsTriggered)
            {
                _carryUsable?.OnUseStart();
                _useHeld = true;
            }

            // Use-hold (every frame)
            if (_useHeld && _inputHandler.UseTrigger.IsPressed)
                _carryUsable?.UseTick(Time.deltaTime);

            // Use-stop (edge)
            if (_useHeld && !_inputHandler.UseTrigger.IsPressed)
            {
                _carryUsable?.OnUseStop();
                _useHeld = false;
            }

            if (!CarryingObject || !CarryingBody)
            {
                DropItem();
                return;
            }

            // Подготовка броска
            if (_inputHandler.AttackTriggered)
            {
                _throwForceMultiplier = Mathf.Clamp(_throwForceMultiplier + Time.deltaTime, _throwForceMultiplier, _maxForceMultiplier);
                _isPreparingToThrow = true;

                if (_throwForceMultiplier < _maxForceMultiplier)
                    _currentCarryingDistance = Mathf.Lerp(_currentCarryingDistance, _carryingDistanceShorteningTo, Time.deltaTime);
            }
            else if (_isPreparingToThrow)
            {
                ThrowObject(_throwForceMultiplier);
            }
        }

        private void FixedUpdate()
        {
            if (!CarryingObject || !CarryingBody) return;

            DragObject();

            // Вращение тянем PD-контроллером к ориентации камеры (или выбранной)
            Quaternion targetRot = GetTargetRotation();
            ApplyRotationHold(CarryingBody, targetRot, _rotKp, _rotKd);
        }

        public void GrabOrDrop()
        {
            if (!CarryingObject) GrabObject();
            else DropItem();
        }

        private void DragObject()
        {
            // Целевая позиция перед камерой
            Vector3 targetPosition =
                _playerCameraTransform.position +
                _playerCameraTransform.forward * _currentCarryingDistance;

            // Позиционная «подтяжка» скоростью (можно заменить на PD по желанию)
            Vector3 toTarget = targetPosition - CarryingBody.position;
            CarryingBody.linearVelocity = toTarget * _carrySpeed;
        }

        private void GrabObject()
        {
            Physics.Raycast(
                _playerCameraTransform.position,
                _playerCameraTransform.forward,
                out RaycastHit hitInfo,
                _maxPickupDistance
            );

            if (!hitInfo.rigidbody)
                return;

            if (hitInfo.transform.TryGetComponent<Item>(out var itemObject))
            {
                if (_playerHandsContainer.State == PlayerHandsContainer.HandState.ItemInHand)
                    _playerHandsContainer.DropItemFromHand();

                _playerHandsContainer.TryTakeItemInHand(itemObject);
                return;
            }

            CarryingBody = hitInfo.rigidbody;
            CarryingBody.useGravity = false;

            CarryingObject = hitInfo.rigidbody.gameObject;
            _carryUsable = CarryingObject.GetComponent<ICarryUsable>();
            _carryUsable?.OnGrabbed(_playerCameraTransform);
            _useHeld = false;

            // Сохраняем относительный поворот (на будущее)
            CarryingObject.transform.parent = _playerCameraTransform;
            _relativeRotation = CarryingObject.transform.localRotation;
            CarryingObject.transform.parent = null;

            // Лёгкий подъём от поверхности
            CarryingObject.transform.position += _grabHelperVector;

            // Слои
            _cachedCarryingLayer = CarryingObject.layer;
            CarryingObject.layer = _carryingLayerIndex;

            // Стабилизация старта
            CarryingBody.angularVelocity = Vector3.zero;

            IsDragging = true;
            _isPreparingToThrow = false;
            _throwForceMultiplier = 0f;
        }

        public void ThrowObject(float throwForceMultiplier)
        {
            if (CarryingBody)
            {
                CarryingBody.useGravity = true;

                CarryingBody.AddForce(
                    _playerCameraTransform.forward * (_playerModelParameters.ThrowPower * throwForceMultiplier),
                    ForceMode.Impulse
                );

                CarryingBody.AddTorque(
                    _playerCameraTransform.forward * (_playerModelParameters.ThrowTorquePower * throwForceMultiplier),
                    ForceMode.Impulse
                );
            }

            if (CarryingObject) CarryingObject.layer = _cachedCarryingLayer;
            if (_useHeld) { _carryUsable?.OnUseStop(); _useHeld = false; }
            _carryUsable?.OnDropped();
            _carryUsable = null;
            CarryingBody = null;
            CarryingObject = null;
            IsDragging = false;
            _isPreparingToThrow = false;
            _throwForceMultiplier = 0f;
            _currentCarryingDistance = _carryingDistance;
        }

        private void DropItem()
        {
            if (CarryingObject) CarryingObject.layer = _cachedCarryingLayer;
            if (CarryingBody) CarryingBody.useGravity = true;
            if (_useHeld) { _carryUsable?.OnUseStop(); _useHeld = false; }
            _carryUsable?.OnDropped();
            _carryUsable = null;
            CarryingBody = null;
            CarryingObject = null;
            IsDragging = false;
            _currentCarryingDistance = _carryingDistance;
        }

      
        private Quaternion GetTargetRotation()
        {
            switch (_alignMode)
            {
                case AlignmentMode.WorldZero:
                    return Quaternion.identity;

                case AlignmentMode.Camera:
                    return _playerCameraTransform.rotation;

                case AlignmentMode.CameraNoRoll:
                default:
                    // смотрим туда же, что и камера, но без крена (up — мировой)
                    return Quaternion.LookRotation(_playerCameraTransform.forward, Vector3.up);
            }
        }

        /// <summary>
        /// Выравнивание ориентации к targetRotation через PD-регулятор.
        /// </summary>
        private static void ApplyRotationHold(Rigidbody rb, Quaternion targetRotation, float kp, float kd)
        {
            if (!rb) return;

            // Ошибка ориентации: qErr = qTarget * inv(qCurrent)
            Quaternion qErr = targetRotation * Quaternion.Inverse(rb.rotation);
            qErr.ToAngleAxis(out float angleDeg, out Vector3 axis);

            if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z))
                return;

            if (angleDeg > 180f) angleDeg -= 360f;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // P + D
            Vector3 torque = axis.normalized * (angleRad * kp) - rb.angularVelocity * kd;
            rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }
}
