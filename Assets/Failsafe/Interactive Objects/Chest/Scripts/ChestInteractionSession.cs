using System;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using UnityEngine;

namespace Failsafe.Chests
{
    public sealed class ChestInteractionSession : IDisposable
    {
        private const PlayerControlBlock ChestControlBlocks =
            PlayerControlBlock.Movement |
            PlayerControlBlock.Look |
            PlayerControlBlock.Jump |
            PlayerControlBlock.Crouch |
            PlayerControlBlock.Sprint |
            PlayerControlBlock.Interaction |
            PlayerControlBlock.Shooting |
            PlayerControlBlock.Inventory |
            PlayerControlBlock.ItemUse |
            PlayerControlBlock.Visor;

        private const PlayerControlBlock ConflictingBlocks =
            PlayerControlBlock.Movement |
            PlayerControlBlock.Look |
            PlayerControlBlock.Interaction |
            PlayerControlBlock.Inventory;

        private readonly PlayerControlBlocker _controlBlocker;
        private readonly PlayerCameraPoseOverride _cameraPoseOverride;
        private readonly CursorLock _cursorLock;

        private UnityEngine.Object _owner;
        private Transform _cameraAnchor;
        private bool _hasOwner;
        private bool _hasSavedCursorState;
        private bool _cursorWasLocked;
        private CursorLockMode _savedCursorLockMode;
        private bool _savedCursorVisible;

        public bool IsOpen
        {
            get
            {
                CloseIfOwnerWasDestroyed();
                return _hasOwner;
            }
        }

        public ChestInteractionSession(
            PlayerControlBlocker controlBlocker,
            PlayerCameraPoseOverride cameraPoseOverride,
            CursorLock cursorLock)
        {
            _controlBlocker = controlBlocker ??
                throw new ArgumentNullException(nameof(controlBlocker));
            _cameraPoseOverride = cameraPoseOverride ??
                throw new ArgumentNullException(nameof(cameraPoseOverride));
            _cursorLock = cursorLock ??
                throw new ArgumentNullException(nameof(cursorLock));
        }

        public bool TryOpen(
            UnityEngine.Object owner,
            Transform cameraAnchor,
            out string error)
        {
            CloseIfOwnerWasDestroyed();

            if (owner == null)
            {
                error = "Chest session owner is missing.";
                return false;
            }

            if (cameraAnchor == null)
            {
                error = "Chest camera anchor is not assigned.";
                return false;
            }

            if (_hasOwner)
            {
                if (ReferenceEquals(_owner, owner))
                {
                    error = null;
                    return true;
                }

                error = "Another chest interaction session is already open.";
                return false;
            }

            if (_controlBlocker.IsAnyBlocked(ConflictingBlocks))
            {
                error =
                    "Chest cannot open while another player-control mode is active.";
                return false;
            }

            if (!_cameraPoseOverride.TryAcquire(
                    owner,
                    cameraAnchor,
                    out error))
            {
                return false;
            }

            _owner = owner;
            _cameraAnchor = cameraAnchor;
            _hasOwner = true;

            try
            {
                SaveCursorState();
                _controlBlocker.AddLock(
                    PlayerControlLockIds.ChestOpened,
                    ChestControlBlocks);
                SetCursorUnlocked();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                CloseInternal();
                error =
                    $"Chest interaction session could not open: " +
                    $"{exception.Message}";
                return false;
            }
        }

        public bool TryClose(
            UnityEngine.Object owner,
            out string error)
        {
            CloseIfOwnerWasDestroyed();

            if (!_hasOwner)
            {
                error = null;
                return true;
            }

            if (!ReferenceEquals(_owner, owner))
            {
                error = "Only the chest that opened the session can close it.";
                return false;
            }

            CloseInternal();
            error = null;
            return true;
        }

        public bool IsOwnedBy(UnityEngine.Object owner)
        {
            CloseIfOwnerWasDestroyed();
            return _hasOwner && ReferenceEquals(_owner, owner);
        }

        public void Dispose()
        {
            CloseInternal();
        }

        private void SaveCursorState()
        {
            if (_hasSavedCursorState)
                return;

            _cursorWasLocked = _cursorLock.IsCursorLocked;
            _savedCursorLockMode = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            _hasSavedCursorState = true;
        }

        private void SetCursorUnlocked()
        {
            _cursorLock.SetCursorLocked(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursorState()
        {
            if (!_hasSavedCursorState)
                return;

            _cursorLock.SetCursorLocked(_cursorWasLocked);
            Cursor.lockState = _savedCursorLockMode;
            Cursor.visible = _savedCursorVisible;
            _hasSavedCursorState = false;
        }

        private void CloseIfOwnerWasDestroyed()
        {
            if (_hasOwner && (_owner == null || _cameraAnchor == null))
                CloseInternal();
        }

        private void CloseInternal()
        {
            if (!_hasOwner && !_hasSavedCursorState)
                return;

            UnityEngine.Object owner = _owner;
            _controlBlocker.RemoveLock(PlayerControlLockIds.ChestOpened);

            _cameraPoseOverride.Release(owner);

            RestoreCursorState();
            _owner = null;
            _cameraAnchor = null;
            _hasOwner = false;
        }
    }
}
