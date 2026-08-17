using System;
using Failsafe.PlayerMovements;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryInputController3D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryRuntimeController _inventory;
        [SerializeField] private InventoryDragController3D _dragController;
        [SerializeField] private CursorLock _cursorLock;
        [SerializeField] private PlayerControlBlocker _controlBlocker;

        [Header("Input")]
        [SerializeField] private bool _inputEnabled = true;
        [SerializeField] private bool _startOpen;

        public bool IsOpen { get; private set; }
        public bool CanClose =>
            IsOpen &&
            (_dragController == null || !_dragController.IsDragging);

        public event Action<bool> OpenStateChanged;

        private CursorLockMode _cursorLockModeBeforeOpen;
        private bool _cursorVisibleBeforeOpen;
        private bool _cursorWasLockedBeforeOpen;
        private bool _hasSavedCursorState;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponentInParent<InventoryRuntimeController>();

            if (_dragController == null)
                _dragController = GetComponentInParent<InventoryDragController3D>();

            if (_cursorLock == null)
                _cursorLock = GetComponentInParent<CursorLock>();

            if (_controlBlocker == null)
                _controlBlocker = GetComponentInParent<PlayerControlBlocker>();
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
            _inventory.SetPresentationVisible(false);

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
        }

        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
        }

        public bool Toggle()
        {
            return IsOpen
                ? Close()
                : Open();
        }

        public bool Open()
        {
            if (IsOpen)
                return true;

            if (_inventory == null ||
                !_inventory.IsInitialized ||
                _dragController == null ||
                !_inventory.SetPresentationVisible(true))
            {
                return false;
            }

            SaveCursorState();
            SetCursorLocked(false);

            _controlBlocker?.AddLock(
                PlayerControlLockIds.InventoryOpened,
                PlayerControlBlock.Look |
                PlayerControlBlock.Interaction |
                PlayerControlBlock.Shooting |
                PlayerControlBlock.ItemUse |
                PlayerControlBlock.Visor);

            _dragController.SetInteractionEnabled(true);
            IsOpen = true;
            OpenStateChanged?.Invoke(true);
            return true;
        }

        public bool Close()
        {
            if (!IsOpen)
                return true;

            if (_dragController != null && _dragController.IsDragging)
                return false;

            if (_inventory == null ||
                !_inventory.SetPresentationVisible(false))
            {
                return false;
            }

            _dragController?.SetInteractionEnabled(false);
            _controlBlocker?.RemoveLock(
                PlayerControlLockIds.InventoryOpened);
            RestoreCursorState();

            IsOpen = false;
            OpenStateChanged?.Invoke(false);
            return true;
        }

        private void OnDisable()
        {
            if (_dragController != null)
            {
                _dragController.CancelDrag();
                _dragController.SetInteractionEnabled(false);
            }

            if (_inventory != null && _inventory.IsInitialized)
                _inventory.SetPresentationVisible(false);

            _controlBlocker?.RemoveLock(
                PlayerControlLockIds.InventoryOpened);
            RestoreCursorState();

            if (!IsOpen)
                return;

            IsOpen = false;
            OpenStateChanged?.Invoke(false);
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
