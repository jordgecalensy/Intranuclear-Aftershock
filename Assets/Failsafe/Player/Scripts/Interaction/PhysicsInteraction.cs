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
        [SerializeField] private Transform _grabPoint;
        
        // ИСПРАВЛЕНИЕ: Убрана инициализация через GetMask здесь
        [SerializeField] private LayerMask _mask;

        [Tooltip("Индекс слоя (0–31), в который временно помещается переносимый объект.")]
        [SerializeField, Range(0, 31)] private int _carryingLayerIndex = 0;

        private ICarryUsable _carryUsable;
        private PhysicsController _physicsController;
        private bool _useHeld;

        [Header("Throwing")]
        [Tooltip("[при зарядке броска] Линейное сокращение дистанции переноски с Carrying Distance до указанного значения.")]
        [SerializeField] private float _carryingDistanceShorteningTo = 0.8f;

        [Header("Rotation Hold")]
        [SerializeField] private AlignmentMode _alignMode = AlignmentMode.Camera; 
        [SerializeField, Tooltip("Скорость выравнивания вращения (P-составляющая).")]
        private float _rotKp = 30f;
        [SerializeField, Tooltip("Демпфирование вращения (D-составляющая).")]
        private float _rotKd = 5f;

        public enum AlignmentMode
        {
            WorldZero,   
            Camera,      
            CameraNoRoll 
        }

        [Header("Additional Options")]
        [Tooltip("Хак от залипания на некоторых поверхностях.")]
        [SerializeField] private Vector3 _grabHelperVector = new Vector3(0f, 0.01f, 0f);

        [Header("Debug")]
        [SerializeField] public GameObject CarryingObject;
        [SerializeField] private Transform _playerCameraTransform;
        [SerializeField, ReadOnly] private float _currentCarryingDistance;

        [SerializeField] private Vector3 _draggablePositionOffset;
        [SerializeField] private float _dragSpeed = 10f; 

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

            // ИСПРАВЛЕНИЕ: Безопасная инициализация LayerMask в Awake
            if (_mask == 0)
            {
                _mask = LayerMask.GetMask("CarryObjects");
            }

            if (!_playerCameraTransform)
            {
                Camera playerCamera = transform.root.GetComponentInChildren<Camera>();
                if (playerCamera != null)
                    _playerCameraTransform = playerCamera.transform;
            }
        }

        private void Update()
        {
            if (_inputHandler.GrabOrDropAction.WasPressedThisFrame())
                GrabOrDrop();

            if (!IsDragging) return;

            if (_inputHandler.AttackTrigger.IsTriggered)
            {
                _carryUsable?.OnUseStart();
                _useHeld = true;
            }

            if (_useHeld && _inputHandler.AttackTrigger.IsPressed)
                _carryUsable?.UseTick(Time.deltaTime);

            if (_useHeld && !_inputHandler.AttackTrigger.IsPressed)
            {
                _carryUsable?.OnUseStop();
                _useHeld = false;
            }

            if (!CarryingObject)
            {
                DropItem();
                return;
            }
            if (_inputHandler.ThrowObjectAction.IsPressed())
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
            if (!CarryingObject) return;
            Quaternion targetRot = GetTargetRotation();
            ApplyRotation(targetRot);

            _grabPoint.localPosition = new Vector3(0, 0, 1) * _currentCarryingDistance;
        }

        public void GrabOrDrop()
        {
            if (!CarryingObject) GrabObject();
            else DropItem();
        }

        private void GrabObject()
        {
            Physics.Raycast(
                _playerCameraTransform.position,
                _playerCameraTransform.forward,
                out RaycastHit hitInfo,
                _maxPickupDistance,
                _mask
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

            CarryingObject = hitInfo.rigidbody.gameObject;
            _physicsController = PhysicsController.GetOrCreate(CarryingObject);
            _carryUsable = CarryingObject.GetComponent<ICarryUsable>();

            var fixRotation = true;
            if (!_physicsController.Grab(_grabPoint, fixRotation)) {
                Released();
                return;
            }
            _physicsController.Released += Released;
            _carryUsable?.OnGrabbed(_grabPoint);

            _useHeld = false;
            IsDragging = true;
            _isPreparingToThrow = false;
            _throwForceMultiplier = 0f;
        }

        public void Released()
        {
            _carryUsable?.OnDropped();
            CarryingObject = null;
            IsDragging = false;
            _carryUsable = null;
            if (_physicsController != null)
                _physicsController.Released -= Released;
            _physicsController = null;
        }

        public void ThrowObject(float throwForceMultiplier)
        {
            if (_useHeld) { _carryUsable?.OnUseStop(); _useHeld = false; }
            _physicsController.Throw(_playerModelParameters.ThrowPower * throwForceMultiplier, 
                                    _playerModelParameters.ThrowTorquePower * throwForceMultiplier, 
                                    _playerCameraTransform);
            Released();
            _isPreparingToThrow = false;
            _throwForceMultiplier = 0f;
            _currentCarryingDistance = _carryingDistance;
        }

        private void DropItem()
        {
            if (_useHeld) { _carryUsable?.OnUseStop(); _useHeld = false; }
            _physicsController?.Release();
            Released();
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
                    return Quaternion.LookRotation(_playerCameraTransform.forward, Vector3.up);
            }
        }

        private void ApplyRotation(Quaternion targetRotation)
        {
            _grabPoint.rotation = targetRotation;
        }
    }
}