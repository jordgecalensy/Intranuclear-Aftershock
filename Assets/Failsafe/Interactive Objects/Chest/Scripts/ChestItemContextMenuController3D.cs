using Failsafe.Inventory.Core;
using Failsafe.Inventory.Integration;
using Failsafe.Inventory.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Failsafe.Chests
{
    [DisallowMultipleComponent]
    public sealed class ChestItemContextMenuController3D : MonoBehaviour
    {
        [Header("Context Menu")]
        [SerializeField] private RectTransform _menuRoot;
        [SerializeField] private RectTransform _menuClampRoot;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _takeButton;
        [SerializeField] private Button _infoButton;

        [Header("Info")]
        [SerializeField] private InventoryItemInfoPanel3D _infoPanel;

        [Header("Raycast")]
        [SerializeField] private string _inventoryLayerName = "Inventory";
        [SerializeField, Min(0.01f)] private float _maximumRayDistance = 10f;

        [Header("Pointer Input")]
        [SerializeField, Min(0.05f)] private float _doubleClickInterval = 0.3f;
        [SerializeField, Min(1f)] private float _dragThresholdPixels = 6f;

        private ChestInventoryController _chest;
        private ChestInventoryPresenter3D _presentation;
        private ChestTransferService _transferService;
        private Camera _playerCamera;
        private int _inventoryLayerMask;
        private bool _viewsBound;
        private bool _interactionActive;
        private InventoryDragSession _dragSession;
        private Vector2 _pressPosition;
        private Vector3 _initialGrabOffset;
        private string _lastClickedInstanceId;
        private float _lastClickTime = float.NegativeInfinity;
        private bool _hasValidTarget;

        public bool IsDragging { get; private set; }

        public bool IsInitialized { get; private set; }
        public bool IsMenuOpen =>
            _menuRoot != null && _menuRoot.gameObject.activeSelf;
        public bool IsInfoOpen =>
            _infoPanel != null && _infoPanel.IsOpen;
        public bool IsModalOpen => IsMenuOpen || IsInfoOpen;
        public string SelectedInstanceId { get; private set; }

        private void Awake()
        {
            SetMenuVisible(false);
            _infoPanel?.Hide();
        }

        private void Update()
        {
            if (!IsInitialized ||
                !_interactionActive ||
                _presentation == null ||
                !_presentation.IsVisible ||
                IsInfoOpen)
            {
                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null)
                return;

            Vector2 pointerPosition = mouse.position.ReadValue();
            Ray pointerRay = _playerCamera.ScreenPointToRay(pointerPosition);

            // ChestInteractable owns Escape, including cancellation before closing.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (IsMenuOpen)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        _menuRoot, pointerPosition, _playerCamera))
                    return;

                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                    CloseAll();
                else
                    return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
                BeginPointerPress(pointerRay, pointerPosition);

            if (_dragSession != null)
            {
                if (!IsDragging &&
                    ((pointerPosition - _pressPosition).sqrMagnitude >=
                     _dragThresholdPixels * _dragThresholdPixels ||
                     mouse.rightButton.wasPressedThisFrame))
                {
                    IsDragging = true;
                    ResetClickSequence();
                }

                if (IsDragging)
                {
                    if (mouse.rightButton.wasPressedThisFrame)
                        _dragSession.TryToggleRotation();
                    UpdateDrag(pointerRay);
                }

                if (mouse.leftButton.wasReleasedThisFrame)
                    EndPointerPress(pointerRay, pointerPosition);
                else if (!mouse.leftButton.isPressed)
                    CancelPointerInteraction();
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame && !TryOpenMenu(pointerPosition))
                CloseAll();
        }

        public bool Initialize(
            ChestInventoryController chest,
            ChestInventoryPresenter3D presentation,
            ChestTransferService transferService,
            Camera playerCamera,
            out string error)
        {
            _chest = chest;
            _presentation = presentation;
            _transferService = transferService;
            _playerCamera = playerCamera;

            int inventoryLayer = LayerMask.NameToLayer(_inventoryLayerName);
            _inventoryLayerMask = inventoryLayer >= 0
                ? 1 << inventoryLayer
                : 0;

            if (!TryValidateSetup(out error))
            {
                IsInitialized = false;
                return false;
            }

            ConfigureCanvasCameras();
            BindViews();
            _infoPanel.Initialize(_playerCamera);
            IsInitialized = true;
            CloseAll();
            error = null;
            return true;
        }

        public void SetInteractionActive(bool active)
        {
            _interactionActive = active && IsInitialized;

            if (!_interactionActive)
                CloseAll();
        }

        public bool TryCloseTopmost()
        {
            if (_dragSession != null)
            {
                CancelPointerInteraction();
                return true;
            }

            if (IsInfoOpen)
            {
                CloseAll();
                return true;
            }

            if (IsMenuOpen)
            {
                CloseAll();
                return true;
            }

            return false;
        }

        public void CloseAll()
        {
            CancelPointerInteraction();
            _presentation?.Presenter?.ClearSelectedItem();
            SelectedInstanceId = null;
            SetMenuVisible(false);
            _infoPanel?.Hide();
        }

        private bool TryOpenMenu(Vector2 pointerPosition)
        {
            CancelPointerInteraction();
            if (!TryHitItem(_playerCamera.ScreenPointToRay(pointerPosition), out RaycastHit hit,
                    out InventoryPlacement placement))
                return false;

            SelectedInstanceId = placement.Item.InstanceId;
            _presentation.Presenter.SetSelectedItem(SelectedInstanceId);
            _infoPanel.Hide();
            PositionMenu(pointerPosition);
            SetMenuVisible(true);
            return true;
        }

        private void ConfigureCanvasCameras()
        {
            // The player camera need not have the MainCamera tag.
            foreach (Canvas canvas in _presentation.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    canvas.worldCamera = _playerCamera;
            }

            Canvas menuCanvas = _menuRoot.GetComponentInParent<Canvas>(true);
            if (menuCanvas != null && menuCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                menuCanvas.worldCamera = _playerCamera;
        }

        private bool TryHitItem(Ray pointerRay, out RaycastHit hit,
            out InventoryPlacement placement)
        {
            hit = default;
            placement = null;
            if (_chest == null ||
                _presentation?.Presenter == null ||
                _playerCamera == null ||
                _inventoryLayerMask == 0 ||
                _maximumRayDistance <= 0f)
            {
                return false;
            }

            if (!Physics.Raycast(
                    pointerRay,
                    out hit,
                    _maximumRayDistance,
                    _inventoryLayerMask,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            InventoryItemHitTarget3D hitTarget =
                hit.collider.GetComponentInParent<
                    InventoryItemHitTarget3D>();

            return hitTarget != null &&
                   hitTarget.GetComponentInParent<InventoryGridPresenter3D>() == _presentation.Presenter &&
                   _chest.Grid.TryGetPlacement(hitTarget.InstanceId, out placement);
        }

        private void BeginPointerPress(Ray ray, Vector2 pointerPosition)
        {
            CancelPointerInteraction(resetClicks: false);
            if (!TryHitItem(ray, out RaycastHit hit, out InventoryPlacement placement))
            {
                ResetClickSequence();
                return;
            }

            InventoryGridPresenter3D presenter = _presentation.Presenter;
            if (!presenter.GridSpace.TryGetGridPosition(
                    presenter.transform.InverseTransformPoint(hit.point), out InventoryGridPosition cell) ||
                !placement.Contains(cell) ||
                !presenter.TryGetView(placement.Item.InstanceId, out InventoryItemView3D view) ||
                !InventoryGridRaycaster3D.TryGetLocalPointOnGridPlane(ray, presenter.transform, out Vector3 point))
                return;

            _dragSession = new InventoryDragSession(placement, cell);
            _pressPosition = pointerPosition;
            _initialGrabOffset = view.transform.localPosition - point;
        }

        private void UpdateDrag(Ray ray)
        {
            InventoryGridPresenter3D presenter = _presentation.Presenter;
            _hasValidTarget = false;
            if (!InventoryGridRaycaster3D.TryGetLocalPointOnGridPlane(ray,
                    presenter.transform, out Vector3 point))
            {
                presenter.HidePlacementHighlight();
                return;
            }

            if (presenter.GridSpace.TryGetGridPosition(point, out InventoryGridPosition cell))
            {
                _dragSession.UpdatePointer(cell);
                _hasValidTarget = _chest.Grid.ValidateRelocation(_dragSession.InstanceId,
                    _dragSession.TargetOrigin, _dragSession.TargetRotation).IsSuccess;
                bool insideGrid = presenter.TryPreviewPlacement(_dragSession.InstanceId,
                    _dragSession.TargetOrigin, _dragSession.TargetFootprint, _dragSession.TargetRotation);
                _hasValidTarget &= insideGrid;
                presenter.ShowPlacementHighlight(_dragSession.TargetOrigin,
                    _dragSession.TargetFootprint, _hasValidTarget);
            }
            else
                presenter.HidePlacementHighlight();

            float initialAngle = _dragSession.InitialRotation == InventoryItemRotation.Clockwise90 ? 90f : 0f;
            float targetAngle = _dragSession.TargetRotation == InventoryItemRotation.Clockwise90 ? 90f : 0f;
            Vector3 offset = Quaternion.AngleAxis(targetAngle - initialAngle, Vector3.up) * _initialGrabOffset;
            presenter.TryPreviewFreePosition(_dragSession.InstanceId, point + offset,
                _dragSession.TargetFootprint, _dragSession.TargetRotation);
        }

        private void EndPointerPress(Ray ray, Vector2 pointerPosition)
        {
            string instanceId = _dragSession.InstanceId;
            if (IsDragging)
            {
                UpdateDrag(ray);
                if (_hasValidTarget)
                    _chest.Relocate(instanceId, _dragSession.TargetOrigin, _dragSession.TargetRotation);
                CancelPointerInteraction();
                return;
            }

            CancelPointerInteraction(resetClicks: false);
            if ((pointerPosition - _pressPosition).sqrMagnitude > _dragThresholdPixels * _dragThresholdPixels ||
                !TryHitItem(ray, out _, out InventoryPlacement placement) ||
                placement.Item.InstanceId != instanceId)
            {
                ResetClickSequence();
                return;
            }

            if (RegisterClick(instanceId, Time.unscaledTime))
            {
                SelectedInstanceId = instanceId;
                HandleTakeClicked();
            }
        }

        private bool RegisterClick(string instanceId, float time)
        {
            bool doubleClick = instanceId == _lastClickedInstanceId &&
                               time >= _lastClickTime && time - _lastClickTime <= _doubleClickInterval;
            if (doubleClick)
                ResetClickSequence();
            else
            {
                _lastClickedInstanceId = instanceId;
                _lastClickTime = time;
            }
            return doubleClick;
        }

        private void ResetClickSequence()
        {
            _lastClickedInstanceId = null;
            _lastClickTime = float.NegativeInfinity;
        }

        private void CancelPointerInteraction(bool resetClicks = true)
        {
            if (_dragSession != null)
                _presentation?.Presenter?.RestorePlacement(_dragSession.InstanceId);
            _presentation?.Presenter?.HidePlacementHighlight();
            _dragSession = null;
            IsDragging = false;
            _hasValidTarget = false;
            if (resetClicks)
                ResetClickSequence();
        }

        private void PositionMenu(Vector2 pointerPosition)
        {
            if (_menuRoot == null)
                return;

            RectTransform clampRoot = _menuClampRoot != null
                ? _menuClampRoot
                : _menuRoot.parent as RectTransform;

            if (clampRoot == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    clampRoot,
                    pointerPosition,
                    _playerCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Rect bounds = clampRoot.rect;
            Rect menuRect = _menuRoot.rect;
            Vector2 pivot = _menuRoot.pivot;
            float minimumX = bounds.xMin + menuRect.width * pivot.x;
            float maximumX = bounds.xMax - menuRect.width * (1f - pivot.x);
            float minimumY = bounds.yMin + menuRect.height * pivot.y;
            float maximumY = bounds.yMax - menuRect.height * (1f - pivot.y);

            _menuRoot.anchoredPosition = new Vector2(
                Mathf.Clamp(localPoint.x, minimumX, maximumX),
                Mathf.Clamp(localPoint.y, minimumY, maximumY));
        }

        private void HandleEquipClicked()
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId))
                return;

            if (!_transferService.TryEquip(
                    _chest,
                    SelectedInstanceId,
                    out _,
                    out _,
                    out string error))
            {
                Debug.LogWarning(
                    $"Could not equip chest item " +
                    $"'{SelectedInstanceId}': {error}",
                    this);
                return;
            }

            CloseAll();
        }

        private void HandleTakeClicked()
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId))
                return;

            if (!_transferService.TryTake(
                    _chest,
                    SelectedInstanceId,
                    out _,
                    out string error))
            {
                Debug.LogWarning(
                    $"Could not take chest item " +
                    $"'{SelectedInstanceId}': {error}",
                    this);
                return;
            }

            CloseAll();
        }

        private void HandleInfoClicked()
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId) ||
                !_chest.TryGetItemData(
                    SelectedInstanceId,
                    out ItemData itemData))
            {
                return;
            }

            if (!_infoPanel.TryShow(itemData, out string error))
            {
                Debug.LogWarning(
                    $"Could not open chest item Info: {error}",
                    this);
                return;
            }

            SetMenuVisible(false);
        }

        private void HandleInfoCloseRequested()
        {
            CloseAll();
        }

        private void BindViews()
        {
            if (_viewsBound)
                return;

            BindButton(_equipButton, HandleEquipClicked);
            BindButton(_takeButton, HandleTakeClicked);
            BindButton(_infoButton, HandleInfoClicked);
            _infoPanel.CloseRequested -= HandleInfoCloseRequested;
            _infoPanel.CloseRequested += HandleInfoCloseRequested;
            _viewsBound = true;
        }

        private void UnbindViews()
        {
            if (!_viewsBound)
                return;

            UnbindButton(_equipButton, HandleEquipClicked);
            UnbindButton(_takeButton, HandleTakeClicked);
            UnbindButton(_infoButton, HandleInfoClicked);

            if (_infoPanel != null)
                _infoPanel.CloseRequested -= HandleInfoCloseRequested;

            _viewsBound = false;
        }

        private static void BindButton(Button button, UnityAction handler)
        {
            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
        }

        private static void UnbindButton(Button button, UnityAction handler)
        {
            if (button != null)
                button.onClick.RemoveListener(handler);
        }

        private bool TryValidateSetup(out string error)
        {
            if (_chest == null ||
                _presentation == null ||
                !_presentation.IsInitialized ||
                _transferService == null ||
                _playerCamera == null)
            {
                error =
                    "Chest, initialized presentation, transfer service and " +
                    "player camera are required.";
                return false;
            }

            if (_menuRoot == null ||
                _equipButton == null ||
                _takeButton == null ||
                _infoButton == null ||
                _infoPanel == null)
            {
                error =
                    "Menu root, Equip, Take, Info buttons and Info panel are " +
                    "required.";
                return false;
            }

            if (_menuRoot.gameObject == gameObject)
            {
                error =
                    "Chest context-menu controller must be placed outside the " +
                    "Menu Root so it remains active while the popup is hidden.";
                return false;
            }

            if (_inventoryLayerMask == 0)
            {
                error =
                    $"Unity layer '{_inventoryLayerName}' does not exist.";
                return false;
            }

            if (_maximumRayDistance <= 0f)
            {
                error = "Chest item ray distance must be greater than zero.";
                return false;
            }

            error = null;
            return true;
        }

        private void SetMenuVisible(bool visible)
        {
            if (_menuRoot != null &&
                _menuRoot.gameObject.activeSelf != visible)
            {
                _menuRoot.gameObject.SetActive(visible);
            }
        }

        private void OnDisable()
        {
            _interactionActive = false;
            CloseAll();
        }

        private void OnDestroy()
        {
            UnbindViews();
        }
    }
}
