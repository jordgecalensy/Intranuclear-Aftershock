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

        [Header("Screen Menu")]
        [SerializeField, Min(40f)] private float _menuScreenWidth = 260f;
        [SerializeField, Min(0f)] private float _menuCursorOffset = 16f;

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
        private Canvas _screenMenuCanvas;
        private RectTransformState _originalMenuTransform;
        private Vector2 _menuDesignSize;

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
                if (IsPointerInsideMenu(pointerPosition))
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
            EnsureScreenMenu();
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

            EnsureScreenMenu();
            Canvas.ForceUpdateCanvases();
            RectTransform clampRoot = (RectTransform)_screenMenuCanvas.transform;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    clampRoot,
                    pointerPosition,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            Rect bounds = clampRoot.rect;
            float margin = Mathf.Min(8f, Mathf.Min(bounds.width, bounds.height) * 0.1f);
            float scale = Mathf.Min(
                Mathf.Max(40f, _menuScreenWidth) / _menuDesignSize.x,
                (bounds.width - margin * 2f) / _menuDesignSize.x,
                (bounds.height - margin * 2f) / _menuDesignSize.y);
            _menuRoot.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            Vector2 size = _menuDesignSize * _menuRoot.localScale.x;
            float gap = Mathf.Max(0f, _menuCursorOffset);
            float x = localPoint.x + gap;
            float y = localPoint.y - gap;
            if (x + size.x > bounds.xMax - margin)
                x = localPoint.x - gap - size.x;
            if (y - size.y < bounds.yMin + margin)
                y = localPoint.y + gap + size.y;

            // The menu pivot is its upper-left corner; local coordinates belong
            // to the screen canvas, not the rotated world-space chest panel.
            _menuRoot.localPosition = new Vector3(
                Mathf.Clamp(x, bounds.xMin + margin, bounds.xMax - margin - size.x),
                Mathf.Clamp(y, bounds.yMin + margin + size.y, bounds.yMax - margin), 0f);
        }

        private void EnsureScreenMenu()
        {
            if (_screenMenuCanvas != null)
                return;

            _originalMenuTransform = new RectTransformState(_menuRoot);
            _menuDesignSize = _menuRoot.rect.size;
            GameObject root = new GameObject("Chest Context Menu Screen Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");
            _screenMenuCanvas = root.GetComponent<Canvas>();
            _screenMenuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _screenMenuCanvas.sortingOrder = 1000;
            _screenMenuCanvas.targetDisplay = _playerCamera != null ? _playerCamera.targetDisplay : 0;
            _menuRoot.SetParent(root.transform, false);
            _menuRoot.anchorMin = _menuRoot.anchorMax = Vector2.one * 0.5f;
            _menuRoot.pivot = new Vector2(0f, 1f);
            _menuRoot.sizeDelta = _menuDesignSize;
            _menuRoot.localRotation = Quaternion.identity;
            _menuRoot.localPosition = Vector3.zero;
            _menuRoot.localScale = Vector3.one;
            SetMenuVisible(false);
        }

        private bool IsPointerInsideMenu(Vector2 pointerPosition)
        {
            return IsMenuOpen && RectTransformUtility.RectangleContainsScreenPoint(
                _menuRoot, pointerPosition, _screenMenuCanvas != null ? null : _playerCamera);
        }

        private void ReleaseScreenMenu()
        {
            if (_screenMenuCanvas == null)
                return;

            SetMenuVisible(false);
            if (_menuRoot != null && _originalMenuTransform.Parent != null)
                _originalMenuTransform.Restore(_menuRoot);

            GameObject root = _screenMenuCanvas.gameObject;
            _screenMenuCanvas = null;
            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
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

            if (_menuRoot.rect.width <= 0f || _menuRoot.rect.height <= 0f)
            {
                error = "Chest context menu must have a non-zero width and height.";
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
            ReleaseScreenMenu();
        }

        private readonly struct RectTransformState
        {
            public readonly Transform Parent;
            private readonly int _siblingIndex;
            private readonly Vector2 _anchorMin, _anchorMax, _pivot, _sizeDelta;
            private readonly Vector3 _anchoredPosition, _scale;
            private readonly Quaternion _rotation;

            public RectTransformState(RectTransform rect)
            {
                Parent = rect.parent;
                _siblingIndex = rect.GetSiblingIndex();
                _anchorMin = rect.anchorMin;
                _anchorMax = rect.anchorMax;
                _pivot = rect.pivot;
                _sizeDelta = rect.sizeDelta;
                _anchoredPosition = rect.anchoredPosition3D;
                _scale = rect.localScale;
                _rotation = rect.localRotation;
            }

            public void Restore(RectTransform rect)
            {
                rect.SetParent(Parent, false);
                rect.SetSiblingIndex(_siblingIndex);
                rect.anchorMin = _anchorMin;
                rect.anchorMax = _anchorMax;
                rect.pivot = _pivot;
                rect.sizeDelta = _sizeDelta;
                rect.anchoredPosition3D = _anchoredPosition;
                rect.localScale = _scale;
                rect.localRotation = _rotation;
            }
        }
    }
}
