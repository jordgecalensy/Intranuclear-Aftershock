using Failsafe.Player.Model;
using Failsafe.PlayerMovements;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Failsafe.Player.Scripts.Interaction
{
    public class PhysicsInteraction : MonoBehaviour
    {
        [Inject] private PlayerModelParameters _playerModelParameters;
        [Inject] private InputHandler _inputHandler;
        [Inject] private PlayerHandsContainer _playerHandsContainer;

        [Header("Picking Up")]
        [SerializeField] private float _maxPickupDistance = 5f;
        [SerializeField] private LayerMask _mask;

        [Header("Carrying")]
        [SerializeField] private float _carryingDistance = 2.5f;
        [SerializeField] private float _carrySpeed = 4f;
        [SerializeField] private Transform _grabPoint;

        [Tooltip("Индекс слоя (0–31), в который временно помещается переносимый объект.")]
        [SerializeField, Range(0, 31)] private int _carryingLayerIndex = 0;

        [Header("Throwing")]
        [Tooltip("[при зарядке броска] Линейное сокращение дистанции переноски с Carrying Distance до указанного значения.")]
        [SerializeField] private float _carryingDistanceShorteningTo = 0.8f;

        [SerializeField] private float _maxForceMultiplier = 3f;

        [Header("Rotation Hold")]
        [SerializeField] private AlignmentMode _alignMode = AlignmentMode.Camera;

        [SerializeField, Tooltip("Скорость выравнивания вращения (P-составляющая).")]
        private float _rotKp = 30f;

        [SerializeField, Tooltip("Демпфирование вращения (D-составляющая).")]
        private float _rotKd = 5f;

        [Header("Additional Options")]
        [Tooltip("Хак от залипания на некоторых поверхностях.")]
        [SerializeField] private Vector3 _grabHelperVector = new Vector3(0f, 0.01f, 0f);

        [Header("Debug")]
        [SerializeField] public GameObject CarryingObject;
        [SerializeField] private Transform _playerCameraTransform;
        [SerializeField] private float _currentCarryingDistance;

        [SerializeField] private Vector3 _draggablePositionOffset;
        [SerializeField] private float _dragSpeed = 10f;

        private ICarryUsable _carryUsable;
        private PhysicsController _physicsController;

        private bool _useHeld;
        private bool _isPreparingToThrow;
        private float _throwForceMultiplier;

        private int _carryObjectsLayer;

        public bool IsDragging { get; private set; }

        public enum AlignmentMode
        {
            WorldZero,
            Camera,
            CameraNoRoll
        }

        private void Awake()
        {
            _currentCarryingDistance = _carryingDistance;

            _carryObjectsLayer = LayerMask.NameToLayer("CarryObjects");

            if (_carryObjectsLayer == -1)
            {
                Debug.LogError("PhysicsInteraction: слой 'CarryObjects' не найден. Создай его в Project Settings > Tags and Layers.");
            }

            if (_mask.value == 0 && _carryObjectsLayer != -1)
            {
                _mask = 1 << _carryObjectsLayer;
            }

            if (!_playerCameraTransform)
            {
                Camera playerCamera = transform.root.GetComponentInChildren<Camera>();

                if (playerCamera != null)
                {
                    _playerCameraTransform = playerCamera.transform;
                }
            }

            if (!_playerCameraTransform)
            {
                Debug.LogWarning("PhysicsInteraction: не назначена камера игрока.");
            }

            if (!_grabPoint)
            {
                Debug.LogWarning("PhysicsInteraction: не назначен Grab Point.");
            }
        }

        private void Update()
        {
            if (_inputHandler.GrabOrDropAction.WasPressedThisFrame())
            {
                GrabOrDrop();
            }

            if (!IsDragging)
            {
                return;
            }

            if (!CarryingObject || _physicsController == null)
            {
                Released();
                return;
            }

            HandleUseInput();
            HandleThrowInput();
        }

        private void FixedUpdate()
        {
            if (!CarryingObject || !_grabPoint)
            {
                return;
            }

            _grabPoint.localPosition = Vector3.forward * _currentCarryingDistance;

            Quaternion targetRot = GetTargetRotation();
            ApplyRotation(targetRot);
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

        private void GrabObject()
        {
            if (!_playerCameraTransform)
            {
                Debug.LogWarning("PhysicsInteraction: нельзя подобрать объект, потому что не назначена камера.");
                return;
            }

            if (!_grabPoint)
            {
                Debug.LogWarning("PhysicsInteraction: нельзя подобрать объект, потому что не назначен Grab Point.");
                return;
            }

            bool hasHit = Physics.Raycast(
                _playerCameraTransform.position,
                _playerCameraTransform.forward,
                out RaycastHit hitInfo,
                _maxPickupDistance,
                _mask
            );

            Debug.DrawRay(
                _playerCameraTransform.position,
                _playerCameraTransform.forward * _maxPickupDistance,
                hasHit ? Color.green : Color.red,
                1f
            );

            if (!hasHit)
            {
                return;
            }

            if (!hitInfo.rigidbody)
            {
                Debug.Log($"PhysicsInteraction: объект {hitInfo.collider.name} попал в Raycast, но у него нет Rigidbody.");
                return;
            }

            Item itemObject = hitInfo.collider.GetComponentInParent<Item>();

            if (itemObject != null)
            {
                if (_playerHandsContainer.State == PlayerHandsContainer.HandState.ItemInHand)
                {
                    _playerHandsContainer.DropItemFromHand();
                }

                _playerHandsContainer.TryTakeItemInHand(itemObject);
                return;
            }

            CarryingObject = hitInfo.rigidbody.gameObject;
            _physicsController = PhysicsController.GetOrCreate(CarryingObject);
            _carryUsable = CarryingObject.GetComponent<ICarryUsable>();

            if (_carryUsable == null)
            {
                _carryUsable = hitInfo.collider.GetComponentInParent<ICarryUsable>();
            }

            bool fixRotation = true;

            if (!_physicsController.Grab(_grabPoint, fixRotation))
            {
                Released();
                return;
            }

            _physicsController.Released += Released;

            _carryUsable?.OnGrabbed(_grabPoint);

            _useHeld = false;
            IsDragging = true;
            _isPreparingToThrow = false;
            _throwForceMultiplier = 0f;
            _currentCarryingDistance = _carryingDistance;
        }

        private void HandleUseInput()
        {
            if (_inputHandler.AttackTrigger.IsTriggered)
            {
                _carryUsable?.OnUseStart();
                _useHeld = true;
            }

            if (_useHeld && _inputHandler.AttackTrigger.IsPressed)
            {
                _carryUsable?.UseTick(Time.deltaTime);
            }

            if (_useHeld && !_inputHandler.AttackTrigger.IsPressed)
            {
                _carryUsable?.OnUseStop();
                _useHeld = false;
            }
        }

        private void HandleThrowInput()
        {
            if (_inputHandler.ThrowObjectAction.IsPressed())
            {
                if (!_isPreparingToThrow)
                {
                    _isPreparingToThrow = true;
                    _throwForceMultiplier = 1f;
                }

                _throwForceMultiplier = Mathf.Clamp(
                    _throwForceMultiplier + Time.deltaTime,
                    1f,
                    _maxForceMultiplier
                );

                if (_throwForceMultiplier < _maxForceMultiplier)
                {
                    _currentCarryingDistance = Mathf.Lerp(
                        _currentCarryingDistance,
                        _carryingDistanceShorteningTo,
                        Time.deltaTime * 4f
                    );
                }

                return;
            }

            if (_isPreparingToThrow)
            {
                ThrowObject(_throwForceMultiplier);
            }
        }

        public void ThrowObject(float throwForceMultiplier)
        {
            if (_physicsController == null)
            {
                Released();
                return;
            }

            if (_useHeld)
            {
                _carryUsable?.OnUseStop();
                _useHeld = false;
            }

            throwForceMultiplier = Mathf.Clamp(
                throwForceMultiplier,
                1f,
                _maxForceMultiplier
            );

            PhysicsController controller = _physicsController;

            controller.Released -= Released;

            controller.Throw(
                _playerModelParameters.ThrowPower * throwForceMultiplier,
                _playerModelParameters.ThrowTorquePower * throwForceMultiplier,
                _playerCameraTransform
            );

            Released();
        }

        private void DropItem()
        {
            if (_useHeld)
            {
                _carryUsable?.OnUseStop();
                _useHeld = false;
            }

            if (_physicsController != null)
            {
                PhysicsController controller = _physicsController;

                controller.Released -= Released;
                controller.Release();
            }

            Released();
        }

        public void Released()
        {
            if (_useHeld)
            {
                _carryUsable?.OnUseStop();
                _useHeld = false;
            }

            _carryUsable?.OnDropped();

            if (_physicsController != null)
            {
                _physicsController.Released -= Released;
            }

            CarryingObject = null;
            _physicsController = null;
            _carryUsable = null;

            IsDragging = false;
            _isPreparingToThrow = false;
            _throwForceMultiplier = 0f;
            _currentCarryingDistance = _carryingDistance;
        }

        private Quaternion GetTargetRotation()
        {
            if (!_playerCameraTransform)
            {
                return Quaternion.identity;
            }

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
            if (!_grabPoint)
            {
                return;
            }

            _grabPoint.rotation = targetRotation;
        }
    }
}
