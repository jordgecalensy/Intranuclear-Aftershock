using System;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using Failsafe.PlayerMovements;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryInputController3D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryRuntimeController _inventory;
        [SerializeField] private InventoryDragController3D _dragController;
        [SerializeField] private InventoryRobotPresentationController
            _robotPresentation;
        [SerializeField] private InventoryQuickBarPresentationLayout3D
            _closedQuickBarLayout;
        [SerializeField] private InventoryItemContextMenuController3D
            _itemContextMenu;
        [SerializeField] private CursorLock _cursorLock;
        [SerializeField] private PlayerControlBlocker _controlBlocker;

        [Header("Input")]
        [SerializeField] private bool _inputEnabled = true;
        [SerializeField] private bool _startOpen;

        public bool IsOpen { get; private set; }
        public bool IsTransitioning =>
            _robotPresentation != null &&
            _robotPresentation.IsTransitioning;

        public bool CanClose =>
            IsOpen &&
            !IsTransitioning &&
            (_dragController == null || !_dragController.IsDragging);

        public event Action<bool> OpenStateChanged;

        private CursorLockMode _cursorLockModeBeforeOpen;
        private bool _cursorVisibleBeforeOpen;
        private bool _cursorWasLockedBeforeOpen;
        private bool _hasSavedCursorState;
        private InputHandler _inputHandler;
        private InventoryQuickSlotEquipService _quickSlotEquipService;
        private InventoryRobotPresentationLayout3D _robotLayout;

        [Inject]
        public void Construct(
            InputHandler inputHandler,
            InventoryQuickSlotEquipService quickSlotEquipService)
        {
            if (_quickSlotEquipService != null)
            {
                _quickSlotEquipService.ActiveSlotChanged -=
                    HandleActiveSlotChanged;
            }

            _inputHandler = inputHandler;
            _quickSlotEquipService = quickSlotEquipService;

            if (_quickSlotEquipService != null)
            {
                _quickSlotEquipService.ActiveSlotChanged +=
                    HandleActiveSlotChanged;
            }
        }

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponentInParent<InventoryRuntimeController>();

            if (_dragController == null)
                _dragController = GetComponentInParent<InventoryDragController3D>();

            if (_robotPresentation == null)
            {
                _robotPresentation = GetComponentInChildren<
                    InventoryRobotPresentationController>(true);
            }

            if (_robotPresentation != null)
            {
                _robotLayout = _robotPresentation.GetComponentInChildren<
                    InventoryRobotPresentationLayout3D>(true);
            }

            if (_closedQuickBarLayout == null)
            {
                _closedQuickBarLayout = GetComponentInChildren<
                    InventoryQuickBarPresentationLayout3D>(true);
            }

            if (_itemContextMenu == null)
            {
                _itemContextMenu = GetComponentInChildren<
                    InventoryItemContextMenuController3D>(true);
            }

            if (_cursorLock == null)
                _cursorLock = GetComponentInParent<CursorLock>();

            if (_controlBlocker == null)
                _controlBlocker = GetComponentInParent<PlayerControlBlocker>();
        }

        private void OnEnable()
        {
            SubscribeToRobotPresentation();
        }

        private void Start()
        {
            if (_inventory == null ||
                !_inventory.IsInitialized ||
                _dragController == null)
            {
                Debug.LogError(
                    "Inventory input controller requires initialized runtime and drag controllers.",
                    this);

                enabled = false;
                return;
            }

            _dragController.SetInteractionEnabled(false);

            if (_closedQuickBarLayout != null &&
                !_inventory.TryBindClosedQuickBarLayout(
                    _closedQuickBarLayout,
                    out string quickBarLayoutError))
            {
                Debug.LogWarning(
                    $"Closed quick-bar layout could not be bound: " +
                    $"{quickBarLayoutError}. Using the procedural fallback.",
                    this);
            }

            _inventory.SetPresentationVisible(false);
            _inventory.QuickBarPresenter?.SetActiveSlot(
                _quickSlotEquipService?.ActiveSlotIndex ??
                InventoryQuickBarPresenter3D.NoActiveSlot);

            if (_startOpen)
                Open();
        }

        private void Update()
        {
            if (!_inputEnabled)
                return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard != null &&
                (keyboard.tabKey.wasPressedThisFrame ||
                 keyboard.iKey.wasPressedThisFrame))
            {
                Toggle();
            }

            if (_inputHandler != null &&
                _inputHandler.TryConsumeQuickSlotSelection(
                    out int quickSlotIndex) &&
                !TryHandleQuickSlotInput(
                    quickSlotIndex,
                    out string error) &&
                !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning(
                    $"Quick-slot selection failed: {error}",
                    this);
            }
        }

        private bool TryHandleQuickSlotInput(
            int slotIndex,
            out string error)
        {
            if (!IsOpen)
                _closedQuickBarLayout?.RequestReveal();

            if (IsOpen)
            {
                InventoryDragSession session =
                    _dragController?.CurrentSession;

                if (session == null)
                {
                    error = null;
                    return true;
                }

                InventoryOperationResult result =
                    _inventory.AssignQuickSlot(
                        slotIndex,
                        session.InstanceId);

                error = result.IsSuccess
                    ? null
                    : $"Could not assign inventory item " +
                      $"'{session.InstanceId}' to quick slot " +
                      $"{slotIndex + 1}: {result.FailureReason}.";

                return result.IsSuccess;
            }

            if (_quickSlotEquipService == null)
            {
                error = "Quick-slot equip service was not injected.";
                return false;
            }

            return _quickSlotEquipService.TrySelectSlot(
                slotIndex,
                out error);
        }

        private void HandleActiveSlotChanged(int slotIndex)
        {
            _inventory?.QuickBarPresenter?.SetActiveSlot(slotIndex);
        }

        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
        }

        public bool Toggle()
        {
            if (IsTransitioning)
                return false;

            return IsOpen
                ? Close()
                : Open();
        }

        public bool Open()
        {
            if (IsTransitioning)
                return false;

            if (IsOpen)
                return true;

            if (_inventory == null ||
                !_inventory.IsInitialized ||
                _dragController == null)
            {
                return false;
            }

            SaveCursorState();
            AddInventoryControlLock();
            _dragController.SetInteractionEnabled(false);

            if (_robotPresentation != null)
            {
                if (_robotPresentation.RequestOpen())
                    return true;

                ReleaseInventoryControlLock();
                RestoreCursorState();
                return false;
            }

            return CompleteOpen();
        }

        public bool Close()
        {
            if (IsTransitioning)
                return false;

            if (!IsOpen)
                return true;

            if (_dragController != null && _dragController.IsDragging)
                return false;

            _itemContextMenu?.CloseAll(
                restoreDragInteraction: false);

            if (_inventory == null ||
                !_inventory.SetPresentationVisible(false))
            {
                return false;
            }

            _dragController?.SetInteractionEnabled(false);

            if (_robotPresentation != null)
            {
                if (_robotPresentation.RequestClose())
                    return true;

                _inventory.SetPresentationVisible(true);
                _dragController?.SetInteractionEnabled(true);
                return false;
            }

            CompleteClose();
            return true;
        }

        private bool CompleteOpen()
        {
            if (_inventory != null &&
                _robotLayout != null &&
                !_inventory.TryBindRobotPresentationLayout(
                    _robotLayout,
                    out string layoutError))
            {
                Debug.LogWarning(
                    $"Robot inventory layout could not be bound: " +
                    $"{layoutError}. Using the procedural presentation.",
                    this);
            }

            if (_inventory == null ||
                !_inventory.SetPresentationVisible(true))
            {
                Debug.LogError(
                    "Inventory robot opened, but inventory presentation " +
                    "could not be shown.",
                    this);

                _robotPresentation?.RequestClose();
                CompleteClose();
                return false;
            }

            SetCursorLocked(false);
            _dragController?.SetInteractionEnabled(true);

            if (IsOpen)
                return true;

            IsOpen = true;
            OpenStateChanged?.Invoke(true);
            return true;
        }

        private void CompleteClose()
        {
            _itemContextMenu?.CloseAll(
                restoreDragInteraction: false);
            _dragController?.SetInteractionEnabled(false);
            ReleaseInventoryControlLock();
            RestoreCursorState();

            if (!IsOpen)
                return;

            IsOpen = false;
            OpenStateChanged?.Invoke(false);
        }

        private void HandleRobotOpenCompleted()
        {
            CompleteOpen();
        }

        private void HandleRobotCloseCompleted()
        {
            CompleteClose();
        }

        private void OnDisable()
        {
            UnsubscribeFromRobotPresentation();
            _itemContextMenu?.CloseAll(
                restoreDragInteraction: false);

            if (_dragController != null)
            {
                _dragController.CancelDrag();
                _dragController.SetInteractionEnabled(false);
            }

            if (_inventory != null && _inventory.IsInitialized)
                _inventory.SetPresentationVisible(false);

            _robotPresentation?.ForceHidden();
            ReleaseInventoryControlLock();
            RestoreCursorState();

            if (!IsOpen)
                return;

            IsOpen = false;
            OpenStateChanged?.Invoke(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromRobotPresentation();

            if (_quickSlotEquipService != null)
            {
                _quickSlotEquipService.ActiveSlotChanged -=
                    HandleActiveSlotChanged;
            }
        }

        private void SubscribeToRobotPresentation()
        {
            if (_robotPresentation == null)
                return;

            _robotPresentation.OpenCompleted -=
                HandleRobotOpenCompleted;
            _robotPresentation.CloseCompleted -=
                HandleRobotCloseCompleted;

            _robotPresentation.OpenCompleted +=
                HandleRobotOpenCompleted;
            _robotPresentation.CloseCompleted +=
                HandleRobotCloseCompleted;
        }

        private void UnsubscribeFromRobotPresentation()
        {
            if (_robotPresentation == null)
                return;

            _robotPresentation.OpenCompleted -=
                HandleRobotOpenCompleted;
            _robotPresentation.CloseCompleted -=
                HandleRobotCloseCompleted;
        }

        private void AddInventoryControlLock()
        {
            _controlBlocker?.AddLock(
                PlayerControlLockIds.InventoryOpened,
                PlayerControlBlock.Look |
                PlayerControlBlock.Interaction |
                PlayerControlBlock.Shooting |
                PlayerControlBlock.ItemUse |
                PlayerControlBlock.Visor);
        }

        private void ReleaseInventoryControlLock()
        {
            _controlBlocker?.RemoveLock(
                PlayerControlLockIds.InventoryOpened);
        }

        private void SaveCursorState()
        {
            if (_hasSavedCursorState)
                return;

            _cursorLockModeBeforeOpen = Cursor.lockState;
            _cursorVisibleBeforeOpen = Cursor.visible;
            _cursorWasLockedBeforeOpen =
                _cursorLock != null && _cursorLock.IsCursorLocked;
            _hasSavedCursorState = true;
        }

        private void RestoreCursorState()
        {
            if (!_hasSavedCursorState)
                return;

            if (_cursorLock != null)
            {
                _cursorLock.SetCursorLocked(
                    _cursorWasLockedBeforeOpen);
            }
            else
            {
                Cursor.lockState = _cursorLockModeBeforeOpen;
                Cursor.visible = _cursorVisibleBeforeOpen;
            }

            _hasSavedCursorState = false;
        }

        private void SetCursorLocked(bool locked)
        {
            if (_cursorLock != null)
            {
                _cursorLock.SetCursorLocked(locked);
                return;
            }

            Cursor.lockState = locked
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = !locked;
        }
    }
}
