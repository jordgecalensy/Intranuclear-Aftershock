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

        private ChestInventoryController _chest;
        private ChestInventoryPresenter3D _presentation;
        private ChestTransferService _transferService;
        private Camera _playerCamera;
        private int _inventoryLayerMask;
        private bool _viewsBound;
        private bool _interactionActive;

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

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 pointerPosition = mouse.position.ReadValue();

            if (IsMenuOpen &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    _menuRoot,
                    pointerPosition,
                    _playerCamera))
            {
                return;
            }

            if (!TryOpenMenu(pointerPosition))
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
            _presentation?.Presenter?.ClearSelectedItem();
            SelectedInstanceId = null;
            SetMenuVisible(false);
            _infoPanel?.Hide();
        }

        private bool TryOpenMenu(Vector2 pointerPosition)
        {
            if (_chest == null ||
                _presentation?.Presenter == null ||
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
                !_chest.Grid.TryGetItem(hitTarget.InstanceId, out _))
            {
                return false;
            }

            SelectedInstanceId = hitTarget.InstanceId;
            _presentation.Presenter.SetSelectedItem(SelectedInstanceId);
            _infoPanel.Hide();
            PositionMenu(pointerPosition);
            SetMenuVisible(true);
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
