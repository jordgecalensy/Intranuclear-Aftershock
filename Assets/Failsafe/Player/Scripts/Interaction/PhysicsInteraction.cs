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
        [Inject] PlayerModelParameters _playerModelParameters;

        [Header("Picking Up")]
        [SerializeField] private float _maxPickupDistance = 5f;

        [Header("Carrying")]
        [SerializeField] private float _carryingDistance = 2.5f;
        [SerializeField] private float _carrySpeed = 10f;

        [Tooltip("Старое поле маски оставлено для обратной совместимости. Для смены слоя используйте индекс ниже.")]
        [SerializeField] private LayerMask _carryingObjectLayer;

        [Tooltip("Индекс слоя (0–31), в который временно помещается переносимый объект.")]
        [SerializeField, Range(0, 31)] private int _carryingLayerIndex = 0;

        private ICarryUsable _carryUsable;
        private bool _useHeld;

        [Header("Throwing")]
        [Tooltip("[при зарядке броска] Линейное сокращение дистанции переноски с Carrying Distance до указанного значения.")]
        [SerializeField] private float _carryingDistanceShorteningTo = 0.8f;

        [Header("Rotation Hold")]
        [SerializeField, Tooltip("Тянуть предмет к мировому (0,0,0).")]
        private bool _alignToWorldZero = true;

        [SerializeField, Tooltip("Скорость выравнивания вращения (пропорциональная составляющая).")]
        private float _rotKp = 30f;

        [SerializeField, Tooltip("Демпфирование вращения (дифференциальная составляющая).")]
        private float _rotKd = 5f;

        [Header("Additional Options")]
        [Tooltip("Хак от залипания на некоторых поверхностях.")]
        [SerializeField] private Vector3 _grabHelperVector = new Vector3(0f, 0.01f, 0f);

        [Header("Debug")]
        [SerializeField] public GameObject CarryingObject;
        [SerializeField] public Rigidbody CarryingBody;
        [SerializeField] private Transform _playerCameraTransform;
        [SerializeField, ReadOnly] private float _currentCarryingDistance;

        [SerializeField] private Vector3 _draggablePositionOffset;
        [SerializeField] private float _dragSpeed = 10f; // зарезервировано

        private Quaternion _relativeRotation;

        [Inject] private InputHandler _inputHandler;
        [Inject] private PlayerHandsContainer _playerHandsContainer;

        private bool _isPreparingToThrow;
        private float _throwForceMultiplier;
        private const float _maxForceMultiplier = 3f;

        private int _cachedCarryingLayer;

        [Header("Crosshair")]
        [SerializeField] private Image _crosshairImage;
        [SerializeField] private float _normalSize = 0.2f;
        [SerializeField] private float _hoverSize = 0.6f;
        [SerializeField] private float _scaleSpeed = 8f;

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
            UpdateCrosshairScale();

            if (_inputHandler.GrabOrDropAction.WasPressedThisFrame())
            {
                GrabOrDrop();
            }

            if (IsDragging)
            {
                // старт использования — единичный импульс
                if (_inputHandler.UseTrigger.IsTriggered)
                {
                    _carryUsable?.OnUseStart();
                    _useHeld = true;
                }

                // удержание — каждый кадр
                if (_useHeld && _inputHandler.UseTrigger.IsPressed)
                {
                    _carryUsable?.UseTick(Time.deltaTime);
                }

                // отпускание — фронт вниз
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

                if (_inputHandler.AttackTriggered)
                {
                    _throwForceMultiplier = Mathf.Clamp(_throwForceMultiplier + Time.deltaTime, _throwForceMultiplier, _maxForceMultiplier);
                    _isPreparingToThrow = true;

                    if (_throwForceMultiplier < _maxForceMultiplier)
                    {
                        _currentCarryingDistance = Mathf.Lerp(_currentCarryingDistance, _carryingDistanceShorteningTo, Time.deltaTime);
                    }
                }
                else if (_isPreparingToThrow)
                {
                    ThrowObject(_throwForceMultiplier);
                }
            }
        }

        private void FixedUpdate()
        {
            if (CarryingObject)
            {
                DragObject();

                if (_alignToWorldZero && CarryingBody)
                {
                    // цель — мировой ноль (0,0,0)
                    ApplyRotationHold(CarryingBody, Quaternion.identity, _rotKp, _rotKd);
                }
            }
        }

        public void GrabOrDrop()
        {
            if (!CarryingObject)
            {
                GrabObject();
            }
            else
            {
                DropItem();
            }
        }

        private void DragObject()
        {
            // Целевая позиция перед камерой
            Vector3 targetPosition =
                _playerCameraTransform.position +
                _playerCameraTransform.forward * _currentCarryingDistance;

            // Позиционная «подтяжка» скоростью
            Vector3 toTarget = targetPosition - CarryingBody.position;
            CarryingBody.linearVelocity = toTarget * _carrySpeed;

            // Вращение не задаём напрямую — удерживает PD в FixedUpdate
            // Угловую скорость не обнуляем — PD сам демпфирует через -Kd * ω
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
                {
                    _playerHandsContainer.DropItemFromHand();
                }
                _playerHandsContainer.TryTakeItemInHand(itemObject);
                return;
            }

            CarryingBody = hitInfo.rigidbody;
            CarryingBody.useGravity = false;

            CarryingObject = hitInfo.rigidbody.gameObject;
            _carryUsable = CarryingObject.GetComponent<ICarryUsable>();
            _carryUsable?.OnGrabbed(_playerCameraTransform);
            _useHeld = false;

            // Сохраняем относительный поворот (на будущее, если пригодится)
            CarryingObject.transform.parent = _playerCameraTransform;
            _relativeRotation = CarryingObject.transform.localRotation;
            CarryingObject.transform.parent = null;

            // Лёгкий подъём от поверхности
            CarryingObject.transform.position += _grabHelperVector;

            // Слои
            _cachedCarryingLayer = CarryingObject.layer;
            CarryingObject.layer = _carryingLayerIndex; // используем индекс слоя

            // Сброс угловой скорости, чтобы старт был стабильным
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

        private void UpdateCrosshairScale()
        {
            float targetScale = _normalSize;

            if (!IsDragging)
            {
                Ray ray = new Ray(_playerCameraTransform.position, _playerCameraTransform.forward);
                if (Physics.Raycast(ray, out RaycastHit hit, _maxPickupDistance))
                {
                    if (hit.rigidbody != null)
                    {
                        targetScale = _hoverSize;
                    }
                }
            }

            float current = _crosshairImage.rectTransform.localScale.x;
            float next = Mathf.Lerp(current, targetScale, Time.deltaTime * _scaleSpeed);
            _crosshairImage.rectTransform.localScale = new Vector3(next, next, 1f);
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

            // Нормируем угол к [-180; 180], переводим в радианы
            if (angleDeg > 180f) angleDeg -= 360f;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Пропорционалка + демпфирование по текущей угловой
            Vector3 torque = axis.normalized * (angleRad * kp) - rb.angularVelocity * kd;

            rb.AddTorque(torque, ForceMode.Acceleration);
        }
    }
}
