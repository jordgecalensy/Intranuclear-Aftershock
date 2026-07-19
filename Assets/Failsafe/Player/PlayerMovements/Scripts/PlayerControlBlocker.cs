using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.PlayerMovements
{
    [Flags]
    public enum PlayerControlBlock
    {
        None = 0,

        Movement = 1 << 0,
        Look = 1 << 1,
        Jump = 1 << 2,
        Crouch = 1 << 3,
        Sprint = 1 << 4,
        Interaction = 1 << 5,
        Shooting = 1 << 6,
        Inventory = 1 << 7,
        ItemUse = 1 << 8,
        Visor = 1 << 9,

        All = ~0
    }

    public static class PlayerControlLockIds
    {
        public const int Stasis = 1001;
        public const int Death = 1002;
        public const int InventoryOpened = 1003;
        public const int Cutscene = 1004;
        public const int Dialogue = 1005;
        public const int PauseMenu = 1006;
    }

    public class PlayerControlBlocker : MonoBehaviour
    {
        private readonly Dictionary<int, PlayerControlBlock> _locks = new();

        public PlayerControlBlock CurrentBlocks
        {
            get
            {
                PlayerControlBlock result = PlayerControlBlock.None;

                foreach (PlayerControlBlock block in _locks.Values)
                    result |= block;

                return result;
            }
        }

        public bool HasAnyLock => _locks.Count > 0;

        public void AddLock(int lockId, PlayerControlBlock blocks)
        {
            if (blocks == PlayerControlBlock.None)
                return;

            _locks[lockId] = blocks;

            Debug.Log($"[PlayerControlBlocker] AddLock id={lockId}, blocks={blocks}, current={CurrentBlocks}", this);
        }

        public void RemoveLock(int lockId)
        {
            if (!_locks.Remove(lockId))
                return;

            Debug.Log($"[PlayerControlBlocker] RemoveLock id={lockId}, current={CurrentBlocks}", this);
        }

        public void ClearAllLocks()
        {
            _locks.Clear();

            Debug.Log("[PlayerControlBlocker] ClearAllLocks", this);
        }

        public bool IsBlocked(PlayerControlBlock block)
        {
            return (CurrentBlocks & block) != 0;
        }

        public bool IsAnyBlocked(PlayerControlBlock blocks)
        {
            return (CurrentBlocks & blocks) != 0;
        }

        public bool IsLockedBy(int lockId)
        {
            return _locks.ContainsKey(lockId);
        }

        public PlayerControlBlock GetLock(int lockId)
        {
            return _locks.TryGetValue(lockId, out PlayerControlBlock block)
                ? block
                : PlayerControlBlock.None;
        }
    }
}