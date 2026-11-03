using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryDragController3D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryRuntimeController _inventory;
        [SerializeField] private Camera _playerCamera;

        [Header("Raycast")]
        [SerializeField] private string _inventoryLayerName = "Inventory";
        [SerializeField, Min(0.01f)] private float _maximumRayDistance = 10f;

        [Header("Input")]
        [SerializeField] private bool _interactionEnabled = true;

        [Header("World Drop")]
        [SerializeField, Min(0.1f)] private float _worldDropDistance = 1.5f;

        [Inject] private PlayerHandsContainer _playerHandsContainer;

        public bool IsDragging => CurrentSession != null;
        public bool HasValidTarget { get; private set; }
        public InventoryDragSession CurrentSession { get; private set; }

        private int _inventoryLayerMask;
        private bool _pointerIsOnGrid;
        private bool _hasPointerLocalPoint;
        private Vector3 _pointerLocalPoint;
        private Vector3 _initialPreviewGrabOffset;
        private InventoryFailureReason _currentTargetFailureReason;
        private string _mergeTargetInstanceId;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponentInParent<InventoryRuntimeController>();

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            int inventoryLayer = LayerMask.NameToLayer(_inventoryLayerName);
            _inventoryLayerMask = inventoryLayer >= 0
                ? 1 << inventoryLayer
                : 0;
        }

        private void Update()
        {
            if (!_interactionEnabled ||
                _inventory == null ||
                !_inventory.IsInitialized ||
                _playerCamera == null ||
                Mouse.current == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            Ray pointerRay = _playerCamera.ScreenPointToRay(
                mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
                TryBeginDrag(pointerRay);

            if (!IsDragging)
                return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelDrag();
                return;
            }

            TryUpdateDrag(pointerRay);

            if (mouse.rightButton.wasPressedThisFrame)
                TryRotatePreview();

            if (mouse.leftButton.wasReleasedThisFrame)
                EndDrag(pointerRay);
        }

        public void SetInteractionEnabled(bool interactionEnabled)
        {
            if (_interactionEnabled == interactionEnabled)
                return;

            _interactionEnabled = interactionEnabled;

            if (!_interactionEnabled)
                CancelDrag();
        }

        public bool TryBeginDrag(Ray pointerRay)
        {
            if (IsDragging ||
                _inventory == null ||
                !_inventory.IsInitialized ||
                _inventoryLayerMask == 0 ||
                _maximumRayDistance <= 0f)
            {
                return false;
            }

            if (!Physics.Raycast(
                    pointerRay,
                    out RaycastHit hit,
                    _maximumRayDistance,
                    _inventoryLayerMask,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            InventoryItemHitTarget3D hitTarget =
                hit.collider.GetComponentInParent<InventoryItemHitTarget3D>();

            if (hitTarget == null ||
                !TryGetHitCell(hit, out InventoryGridPosition grabbedCell) ||
                !_inventory.Grid.TryGetPlacement(
                    hitTarget.InstanceId,
                    out InventoryPlacement placement) ||
                !placement.Contains(grabbedCell))
            {
                return false;
            }

            InventoryGridPresenter3D presenter = _inventory.Presenter;

            if (presenter == null ||
                !presenter.TryGetView(
                    hitTarget.InstanceId,
                    out InventoryItemView3D view) ||
                !InventoryGridRaycaster3D.TryGetLocalPointOnGridPlane(
                    pointerRay,
                    presenter.transform,
                    out _pointerLocalPoint))
            {
                return false;
            }

            CurrentSession = new InventoryDragSession(
                placement,
                grabbedCell);

            _initialPreviewGrabOffset =
                view.transform.localPosition - _pointerLocalPoint;
            _hasPointerLocalPoint = true;
            _pointerIsOnGrid = true;
            HasValidTarget = PreviewCurrentTarget();

            if (!HasValidTarget)
            {
                ClearSession();
                return false;
            }

            PreviewFreeAtPointer();

            return true;
        }

        public bool TryUpdateDrag(Ray pointerRay)
        {
            if (!IsDragging)
                return false;

            InventoryGridPresenter3D presenter = _inventory.Presenter;

            if (presenter == null ||
                !InventoryGridRaycaster3D.TryGetLocalPointOnGridPlane(
                    pointerRay,
                    presenter.transform,
                    out _pointerLocalPoint))
            {
                _pointerIsOnGrid = false;
                _hasPointerLocalPoint = false;
                HasValidTarget = false;
                _currentTargetFailureReason = InventoryFailureReason.OutOfBounds;
                presenter?.HidePlacementHighlight();
                return false;
            }

            _hasPointerLocalPoint = true;

            if (!presenter.GridSpace.TryGetGridPosition(
                    _pointerLocalPoint,
                    out InventoryGridPosition pointerCell))
            {
                _pointerIsOnGrid = false;
                HasValidTarget = false;
                _currentTargetFailureReason = InventoryFailureReason.OutOfBounds;
                presenter.HidePlacementHighlight();
                PreviewFreeAtPointer();
                return false;
            }

            _pointerIsOnGrid = true;
            CurrentSession.UpdatePointer(pointerCell);
            HasValidTarget = PreviewCurrentTarget();
            PreviewFreeAtPointer();
            return HasValidTarget;
        }

        public bool TryRotatePreview()
        {
            if (!IsDragging || !CurrentSession.TryToggleRotation())
                return false;

            HasValidTarget = _pointerIsOnGrid && PreviewCurrentTarget();
            PreviewFreeAtPointer();
            return true;
        }

        public InventoryOperationResult EndDrag()
        {
            Ray pointerRay = _playerCamera != null
                ? new Ray(
                    _playerCamera.transform.position,
                    _playerCamera.transform.forward)
                : default;

            return EndDrag(pointerRay);
        }

        public InventoryOperationResult EndDrag(Ray pointerRay)
        {
            if (!IsDragging)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            InventoryDragSession session = CurrentSession;
            InventoryOperationResult result;

            if (HasValidTarget && _mergeTargetInstanceId != null)
            {
                bool preferSourceWorldItem =
                    _playerHandsContainer != null &&
                    _inventory.TryGetWorldItem(
                        session.InstanceId,
                        out Item sourceWorldItem) &&
                    _playerHandsContainer.ItemInHand?.ItemObject ==
                    sourceWorldItem;

                result = _inventory.MergeStacks(
                    session.InstanceId,
                    _mergeTargetInstanceId,
                    preferSourceWorldItem);
            }
            else if (HasValidTarget)
            {
                result = _inventory.Relocate(
                    session.InstanceId,
                    session.TargetOrigin,
                    session.TargetRotation);
            }
            else if (!_pointerIsOnGrid)
            {
                result = TryDropInstanceIntoWorld(
                    session.InstanceId,
                    pointerRay,
                    out _);
            }
            else
            {
                result = InventoryOperationResult.Failure(
                    _currentTargetFailureReason == InventoryFailureReason.None
                        ? InventoryFailureReason.OutOfBounds
                        : _currentTargetFailureReason);
            }

            if (_inventory.Presenter != null &&
                _inventory.Grid != null &&
                _inventory.Grid.TryGetPlacement(session.InstanceId, out _))
            {
                _inventory.Presenter.RestorePlacement(session.InstanceId);
            }

            ClearSession();
            return result;
        }

        public bool TryDropItemIntoWorld(
            string instanceId,
            out string error)
        {
            Transform cameraTransform = _playerCamera != null
                ? _playerCamera.transform
                : null;

            Ray pointerRay = cameraTransform != null
                ? new Ray(
                    cameraTransform.position,
                    cameraTransform.forward)
                : default;

            InventoryOperationResult result =
                TryDropInstanceIntoWorld(
                    instanceId,
                    pointerRay,
                    out error);

            return result.IsSuccess;
        }

        private InventoryOperationResult TryDropInstanceIntoWorld(
            string instanceId,
            Ray pointerRay,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                _inventory == null ||
                !_inventory.IsInitialized)
            {
                error =
                    "An initialized inventory and a valid instance ID " +
                    "are required to drop an item.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (_playerHandsContainer == null)
            {
                Debug.LogError(
                    "Inventory drag controller cannot drop a world item " +
                    "because PlayerHandsContainer was not injected.",
                    this);

                error = "PlayerHandsContainer was not injected.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (!_inventory.TryGetWorldItem(
                    instanceId,
                    out Item worldItem))
            {
                Debug.LogWarning(
                    $"Inventory item '{instanceId}' has no linked " +
                    "world item and cannot be dropped.",
                    this);

                error =
                    $"Inventory item '{instanceId}' has no linked " +
                    "world item.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);
            }

            bool itemIsInHand =
                _playerHandsContainer.ItemInHand?.ItemObject == worldItem;

            InventoryOperationResult result =
                _inventory.DetachWorldItem(
                    instanceId,
                    out Item detachedItem,
                    out error);

            if (!result.IsSuccess || detachedItem == null)
            {
                Debug.LogWarning(
                    $"Could not drop inventory item " +
                    $"'{instanceId}': {error}",
                    this);

                return result;
            }

            if (itemIsInHand)
            {
                Item droppedFromHand =
                    _playerHandsContainer.DropItemFromHand();

                if (droppedFromHand != detachedItem)
                {
                    Debug.LogError(
                        $"Inventory item '{instanceId}' was detached, " +
                        "but the hand system returned a different item.",
                        this);
                }
            }

            PlaceDroppedItem(detachedItem, pointerRay);
            error = null;
            return result;
        }

        private void PlaceDroppedItem(Item worldItem, Ray pointerRay)
        {
            Transform cameraTransform = _playerCamera != null
                ? _playerCamera.transform
                : null;

            Vector3 direction = pointerRay.direction.sqrMagnitude > 0f
                ? pointerRay.direction.normalized
                : cameraTransform != null
                    ? cameraTransform.forward
                    : Vector3.forward;

            Vector3 origin = pointerRay.direction.sqrMagnitude > 0f
                ? pointerRay.origin
                : cameraTransform != null
                    ? cameraTransform.position
                    : worldItem.transform.position;

            worldItem.transform.SetParent(null, true);
            worldItem.transform.position =
                origin + direction * _worldDropDistance;

            worldItem.ToWorldState();

            if (!worldItem.TryGetComponent(out Rigidbody body))
            {
                Debug.LogWarning(
                    $"Dropped item '{worldItem.name}' has no Rigidbody and " +
                    "cannot fall.",
                    worldItem);

                return;
            }

#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            body.useGravity = true;
            body.WakeUp();
        }

        public bool CancelDrag()
        {
            if (!IsDragging)
                return false;

            string instanceId = CurrentSession.InstanceId;

            if (_inventory != null &&
                _inventory.Presenter != null)
            {
                _inventory.Presenter.RestorePlacement(instanceId);
            }

            ClearSession();
            return true;
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        private bool TryGetHitCell(
            RaycastHit hit,
            out InventoryGridPosition hitCell)
        {
            InventoryGridPresenter3D presenter = _inventory.Presenter;

            if (presenter == null)
            {
                hitCell = default;
                return false;
            }

            Vector3 localHitPoint = presenter.transform.InverseTransformPoint(
                hit.point);

            return presenter.GridSpace.TryGetGridPosition(
                localHitPoint,
                out hitCell);
        }

        private bool PreviewCurrentTarget()
        {
            InventoryDragSession session = CurrentSession;
            _mergeTargetInstanceId = null;

            if (session == null ||
                _inventory == null ||
                _inventory.Grid == null ||
                _inventory.Presenter == null)
            {
                _currentTargetFailureReason =
                    InventoryFailureReason.InvalidItem;

                return false;
            }

            if (_inventory.Grid.TryGetItemAt(
                    session.PointerCell,
                    out InventoryItemModel targetItem) &&
                targetItem.InstanceId != session.InstanceId)
            {
                InventoryOperationResult mergeValidation =
                    _inventory.ValidateMergeStacks(
                        session.InstanceId,
                        targetItem.InstanceId);

                _currentTargetFailureReason =
                    mergeValidation.FailureReason;

                bool mergePreviewIsInsideGrid =
                    _inventory.Presenter.TryPreviewPlacement(
                        session.InstanceId,
                        session.TargetOrigin,
                        session.TargetFootprint,
                        session.TargetRotation);

                if (_inventory.Grid.TryGetPlacement(
                        targetItem.InstanceId,
                        out InventoryPlacement targetPlacement))
                {
                    _inventory.Presenter.ShowPlacementHighlight(
                        targetPlacement.Origin,
                        targetPlacement.Footprint,
                        mergeValidation.IsSuccess);
                }

                if (mergeValidation.IsSuccess &&
                    mergePreviewIsInsideGrid)
                {
                    _mergeTargetInstanceId = targetItem.InstanceId;
                    return true;
                }

                return false;
            }

            InventoryOperationResult validation =
                _inventory.Grid.ValidateRelocation(
                    session.InstanceId,
                    session.TargetOrigin,
                    session.TargetRotation);

            _currentTargetFailureReason = validation.FailureReason;

            bool viewIsInsideGrid =
                _inventory.Presenter.TryPreviewPlacement(
                    session.InstanceId,
                    session.TargetOrigin,
                    session.TargetFootprint,
                    session.TargetRotation);

            _inventory.Presenter.ShowPlacementHighlight(
                session.TargetOrigin,
                session.TargetFootprint,
                validation.IsSuccess && viewIsInsideGrid);

            return validation.IsSuccess && viewIsInsideGrid;
        }

        private bool PreviewFreeAtPointer()
        {
            InventoryDragSession session = CurrentSession;

            if (session == null ||
                !_hasPointerLocalPoint ||
                _inventory == null ||
                _inventory.Presenter == null)
            {
                return false;
            }

            Vector3 rotatedGrabOffset = GetRotatedGrabOffset(session);

            return _inventory.Presenter.TryPreviewFreePosition(
                session.InstanceId,
                _pointerLocalPoint + rotatedGrabOffset,
                session.TargetFootprint,
                session.TargetRotation);
        }

        private Vector3 GetRotatedGrabOffset(InventoryDragSession session)
        {
            float initialAngle = session.InitialRotation ==
                                 InventoryItemRotation.Clockwise90
                ? 90f
                : 0f;

            float targetAngle = session.TargetRotation ==
                                InventoryItemRotation.Clockwise90
                ? 90f
                : 0f;

            return Quaternion.AngleAxis(
                       targetAngle - initialAngle,
                       Vector3.up) *
                   _initialPreviewGrabOffset;
        }

        private void ClearSession()
        {
            if (_inventory != null && _inventory.Presenter != null)
                _inventory.Presenter.HidePlacementHighlight();

            CurrentSession = null;
            HasValidTarget = false;
            _pointerIsOnGrid = false;
            _hasPointerLocalPoint = false;
            _pointerLocalPoint = default;
            _initialPreviewGrabOffset = default;
            _currentTargetFailureReason = InventoryFailureReason.None;
            _mergeTargetInstanceId = null;
        }
    }
}
