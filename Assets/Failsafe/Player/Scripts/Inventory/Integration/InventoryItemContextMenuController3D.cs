using Failsafe.Inventory.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryItemContextMenuController3D : MonoBehaviour
    {
        [Header("Inventory References")]
        [SerializeField] private InventoryRuntimeController _inventory;
        [SerializeField] private InventoryDragController3D _dragController;
        [SerializeField] private InventoryInputController3D _inputController;
        [SerializeField] private Camera _playerCamera;

        [Header("Context Menu")]
        [SerializeField] private RectTransform _menuRoot;
        [SerializeField] private RectTransform _menuClampRoot;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _infoButton;

        [Header("Info")]
        [SerializeField] private InventoryItemInfoPanel3D _infoPanel;

        [Header("Raycast")]
        [SerializeField] private string _inventoryLayerName = "Inventory";
        [SerializeField, Min(0.01f)] private float _maximumRayDistance = 10f;

        public bool IsMenuOpen =>
            _menuRoot != null && _menuRoot.gameObject.activeSelf;
        public bool IsInfoOpen =>
            _infoPanel != null && _infoPanel.IsOpen;
        public bool IsModalOpen => IsMenuOpen || IsInfoOpen;
        public string SelectedInstanceId { get; private set; }

        private InventoryQuickSlotEquipService _equipService;
        private int _inventoryLayerMask;
        private bool _viewsBound;

        [Inject]
        public void Construct(
            InventoryQuickSlotEquipService equipService)
        {
            _equipService = equipService;
        }

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponentInParent<InventoryRuntimeController>();

            if (_dragController == null)
                _dragController = GetComponentInParent<InventoryDragController3D>();

            if (_inputController == null)
                _inputController = GetComponentInParent<InventoryInputController3D>();

            if (_playerCamera == null)
                _playerCamera = Camera.main;

            int inventoryLayer = LayerMask.NameToLayer(_inventoryLayerName);
            _inventoryLayerMask = inventoryLayer >= 0
                ? 1 << inventoryLayer
                : 0;

            SetMenuVisible(false);
            _infoPanel?.Hide();
        }

        private void OnEnable()
        {
            BindViews();

            if (_inputController != null)
            {
                _inputController.OpenStateChanged -=
                    HandleInventoryOpenStateChanged;
                _inputController.OpenStateChanged +=
                    HandleInventoryOpenStateChanged;
            }
        }

        private void Start()
        {
            if (!TryValidateSetup(out string error))
            {
                Debug.LogError(
                    $"Inventory context menu is not configured: {error}",
                    this);

                enabled = false;
            }
        }

        private void Update()
        {
            if (_inputController == null ||
                !_inputController.IsOpen ||
                _inputController.IsTransitioning)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame &&
                IsModalOpen)
            {
                CloseAll();
                return;
            }

            if (IsInfoOpen)
                return;

            Mouse mouse = Mouse.current;

            if (mouse == null)
                return;

            Vector2 pointerPosition = mouse.position.ReadValue();

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (_dragController != null &&
                    _dragController.IsDragging)
                {
                    return;
                }

                if (!TryOpenMenu(pointerPosition))
                    CloseAll();

                return;
            }

            if (IsMenuOpen &&
                mouse.leftButton.wasPressedThisFrame &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    _menuRoot,
                    pointerPosition,
                    _playerCamera))
            {
                CloseAll();
            }
        }

        public void CloseAll(bool restoreDragInteraction = true)
        {
            SelectedInstanceId = null;
            SetMenuVisible(false);
            _infoPanel?.Hide();

            if (restoreDragInteraction)
                RestoreDragInteraction();
        }

        private bool TryOpenMenu(Vector2 pointerPosition)
        {
            if (_inventory == null ||
                !_inventory.IsInitialized ||
                _playerCamera == null ||
                _inventoryLayerMask == 0 ||
                _maximumRayDistance <= 0f)
            {
                return false;
            }

            Ray pointerRay = _playerCamera.ScreenPointToRay(pointerPosition);

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
                hit.collider.GetComponentInParent<
                    InventoryItemHitTarget3D>();

            if (hitTarget == null ||
                !_inventory.Grid.TryGetItem(
                    hitTarget.InstanceId,
                    out _))
            {
                return false;
            }

            SelectedInstanceId = hitTarget.InstanceId;
            _infoPanel?.Hide();
            PositionMenu(pointerPosition);
            SetMenuVisible(true);
            _dragController?.SetInteractionEnabled(false);
            return true;
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

            float minimumX =
                bounds.xMin + menuRect.width * pivot.x;
            float maximumX =
                bounds.xMax - menuRect.width * (1f - pivot.x);
            float minimumY =
                bounds.yMin + menuRect.height * pivot.y;
            float maximumY =
                bounds.yMax - menuRect.height * (1f - pivot.y);

            _menuRoot.anchoredPosition = new Vector2(
                Mathf.Clamp(localPoint.x, minimumX, maximumX),
                Mathf.Clamp(localPoint.y, minimumY, maximumY));
        }

        private void HandleEquipClicked()
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId) ||
                _equipService == null)
            {
                return;
            }

            if (!_equipService.TryEquipItem(
                    SelectedInstanceId,
                    out string error))
            {
                Debug.LogWarning(
                    $"Could not equip inventory item " +
                    $"'{SelectedInstanceId}': {error}",
                    this);

                return;
            }

            CloseAll();
        }

        private void HandleDropClicked()
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId) ||
                _dragController == null)
            {
                return;
            }

            if (!_dragController.TryDropItemIntoWorld(
                    SelectedInstanceId,
                    out string error))
            {
                Debug.LogWarning(
                    $"Could not drop inventory item " +
                    $"'{SelectedInstanceId}': {error}",
                    this);

                return;
            }

            CloseAll();
        }

        private void HandleInfoClicked()
        {
            if (string.IsNullOrWhiteSpace(SelectedInstanceId) ||
                _inventory == null ||
                _infoPanel == null)
            {
                return;
            }

            if (!_inventory.TryGetItemDataForInstance(
                    SelectedInstanceId,
                    out ItemData itemData))
            {
                Debug.LogWarning(
                    $"Inventory item '{SelectedInstanceId}' has no " +
                    "registered ItemData for the Info panel.",
                    this);

                return;
            }

            if (!_infoPanel.TryShow(itemData, out string error))
            {
                Debug.LogWarning(
                    $"Could not open item Info: {error}",
                    this);

                return;
            }

            SetMenuVisible(false);
        }

        private void HandleInfoCloseRequested()
        {
            SelectedInstanceId = null;
            _infoPanel?.Hide();
            RestoreDragInteraction();
        }

        private void HandleInventoryOpenStateChanged(bool isOpen)
        {
            if (!isOpen)
                CloseAll(restoreDragInteraction: false);
        }

        private void RestoreDragInteraction()
        {
            if (_inputController != null &&
                _inputController.IsOpen &&
                !_inputController.IsTransitioning)
            {
                _dragController?.SetInteractionEnabled(true);
            }
        }

        private void BindViews()
        {
            if (_viewsBound)
                return;

            BindButton(_equipButton, HandleEquipClicked);
            BindButton(_dropButton, HandleDropClicked);
            BindButton(_infoButton, HandleInfoClicked);

            if (_infoPanel != null)
            {
                _infoPanel.Initialize(_playerCamera);
                _infoPanel.CloseRequested +=
                    HandleInfoCloseRequested;
            }

            _viewsBound = true;
        }

        private void UnbindViews()
        {
            if (!_viewsBound)
                return;

            UnbindButton(_equipButton, HandleEquipClicked);
            UnbindButton(_dropButton, HandleDropClicked);
            UnbindButton(_infoButton, HandleInfoClicked);

            if (_infoPanel != null)
            {
                _infoPanel.CloseRequested -=
                    HandleInfoCloseRequested;
            }

            _viewsBound = false;
        }

        private void BindButton(
            Button button,
            UnityAction handler)
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
        }

        private static void UnbindButton(
            Button button,
            UnityAction handler)
        {
            if (button != null)
                button.onClick.RemoveListener(handler);
        }

        private bool TryValidateSetup(out string error)
        {
            if (_inventory == null ||
                _dragController == null ||
                _inputController == null ||
                _playerCamera == null)
            {
                error =
                    "Inventory, Drag Controller, Input Controller and " +
                    "Player Camera references are required.";

                return false;
            }

            if (_menuRoot == null ||
                _equipButton == null ||
                _dropButton == null ||
                _infoButton == null)
            {
                error =
                    "Menu Root and all three menu buttons are required.";

                return false;
            }

            if (_infoPanel == null)
            {
                error = "Info Panel is required.";
                return false;
            }

            if (_equipService == null)
            {
                error = "Quick-slot equip service was not injected.";
                return false;
            }

            if (_inventoryLayerMask == 0)
            {
                error =
                    $"Unity layer '{_inventoryLayerName}' does not exist.";

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
            if (_inputController != null)
            {
                _inputController.OpenStateChanged -=
                    HandleInventoryOpenStateChanged;
            }

            UnbindViews();
            CloseAll(restoreDragInteraction: false);
        }
    }
}
