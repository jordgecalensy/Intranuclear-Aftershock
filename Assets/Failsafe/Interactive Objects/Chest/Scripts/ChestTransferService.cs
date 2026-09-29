using System;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Integration;
using Failsafe.Items;

namespace Failsafe.Chests
{
    public sealed class ChestTransferService
    {
        private readonly InventoryRuntimeController _playerInventory;
        private readonly InventoryQuickSlotEquipService _equipService;
        private readonly PlayerHandsContainer _hands;

        public ChestTransferService(
            InventoryRuntimeController playerInventory,
            InventoryQuickSlotEquipService equipService,
            PlayerHandsContainer hands)
        {
            _playerInventory = playerInventory ??
                throw new ArgumentNullException(nameof(playerInventory));
            _equipService = equipService ??
                throw new ArgumentNullException(nameof(equipService));
            _hands = hands ??
                throw new ArgumentNullException(nameof(hands));
        }

        public bool TryTake(
            ChestInventoryController chest,
            string chestInstanceId,
            out string playerInstanceId,
            out string error)
        {
            playerInstanceId = null;

            if (!TryDetachChestItem(
                    chest,
                    chestInstanceId,
                    out ChestDetachedItem detachedItem,
                    out error))
            {
                return false;
            }

            InventoryOperationResult addResult =
                _playerInventory.CreateAndStoreRuntimeItem(
                    detachedItem.ItemData,
                    out playerInstanceId,
                    out string addError);

            if (addResult.IsSuccess)
            {
                if (chest.TryCommitDetached(
                        detachedItem,
                        out string commitError))
                {
                    error = null;
                    return true;
                }

                error =
                    $"The item was added to the player inventory, but the " +
                    $"chest transfer could not be committed: {commitError}";
                return false;
            }

            playerInstanceId = null;
            RestoreChestAfterFailure(
                chest,
                detachedItem,
                addError ??
                $"Player inventory rejected the item: {addResult.FailureReason}.",
                out error);
            return false;
        }

        public bool TryEquip(
            ChestInventoryController chest,
            string chestInstanceId,
            out string playerInstanceId,
            out int quickSlotIndex,
            out string error)
        {
            playerInstanceId = null;
            quickSlotIndex = InventoryQuickSlotEquipService.NoActiveSlot;

            if (chest == null)
            {
                error = "Chest inventory is not assigned.";
                return false;
            }

            if (!chest.TryGetStoredItem(
                    chestInstanceId,
                    out ChestStoredItem storedItem))
            {
                error = $"Chest item '{chestInstanceId}' was not found.";
                return false;
            }

            if (!storedItem.ItemData.CanAssignQuickSlot)
            {
                error =
                    $"Chest item '{chestInstanceId}' cannot be assigned to " +
                    "a quick slot.";
                return false;
            }

            if (!TryDetachChestItem(
                    chest,
                    chestInstanceId,
                    out ChestDetachedItem detachedItem,
                    out error))
            {
                return false;
            }

            InventoryOperationResult addResult =
                _playerInventory.CreateAndStoreRuntimeItem(
                    detachedItem.ItemData,
                    out playerInstanceId,
                    out string addError);

            if (!addResult.IsSuccess)
            {
                playerInstanceId = null;
                RestoreChestAfterFailure(
                    chest,
                    detachedItem,
                    addError ??
                    $"Player inventory rejected the item: " +
                    $"{addResult.FailureReason}.",
                    out error);
                return false;
            }

            string equipError = null;
            bool mergedIntoHeldItem = IsRegisteredItemInHands(
                playerInstanceId);
            bool wasAlreadyActive = TryGetActiveQuickSlot(
                playerInstanceId,
                out int activeQuickSlot);

            if (wasAlreadyActive ||
                _equipService.TryEquipItem(
                    playerInstanceId,
                    out equipError))
            {
                quickSlotIndex = wasAlreadyActive
                    ? activeQuickSlot
                    : _equipService.ActiveSlotIndex;

                if (chest.TryCommitDetached(
                        detachedItem,
                        out string commitError))
                {
                    error = null;
                    return true;
                }

                error =
                    $"The item was equipped, but the chest transfer could not " +
                    $"be committed: {commitError}";
                return false;
            }

            string playerRollbackError;
            bool playerRolledBack = mergedIntoHeldItem
                ? TryRollbackMergedHeldQuantity(
                    playerInstanceId,
                    out playerRollbackError)
                : TryRollbackPlayerItem(
                    playerInstanceId,
                    out playerRollbackError);

            string failure = string.IsNullOrWhiteSpace(equipError)
                ? "The item could not be equipped."
                : equipError;

            if (!playerRolledBack)
            {
                quickSlotIndex = InventoryQuickSlotEquipService.NoActiveSlot;

                bool chestCommitted = chest.TryCommitDetached(
                    detachedItem,
                    out string commitError);

                error = failure +
                    $" Player inventory rollback failed: " +
                    $"{playerRollbackError} The item was left in the player " +
                    "inventory and was not restored to the chest to prevent duplication." +
                    (chestCommitted
                        ? string.Empty
                        : $" Chest transfer commit also failed: {commitError}");
                return false;
            }

            if (!chest.TryRestoreDetached(
                    detachedItem,
                    out string chestRestoreError))
            {
                failure +=
                    $" Chest rollback failed: {chestRestoreError}";
            }

            playerInstanceId = null;
            quickSlotIndex = InventoryQuickSlotEquipService.NoActiveSlot;
            error = failure;
            return false;
        }

        private bool IsRegisteredItemInHands(string playerInstanceId)
        {
            return _hands.State == PlayerHandsContainer.HandState.ItemInHand &&
                   _hands.ItemInHand?.ItemObject != null &&
                   _playerInventory.TryGetWorldItem(
                       playerInstanceId,
                       out Item registeredItem) &&
                   ReferenceEquals(
                       _hands.ItemInHand.ItemObject,
                       registeredItem);
        }

        private bool TryRollbackMergedHeldQuantity(
            string playerInstanceId,
            out string error)
        {
            if (!_playerInventory.Grid.TryGetItem(
                    playerInstanceId,
                    out InventoryItemModel item) ||
                item.Quantity <= 1)
            {
                error =
                    $"Held inventory stack '{playerInstanceId}' does not " +
                    "contain the unit added from the chest.";
                return false;
            }

            InventoryOperationResult rollbackResult =
                _playerInventory.Grid.TryRemoveQuantity(
                    playerInstanceId,
                    amount: 1);

            if (rollbackResult.IsSuccess)
            {
                error = null;
                return true;
            }

            error =
                $"Held inventory stack rollback failed: " +
                $"{rollbackResult.FailureReason}.";
            return false;
        }

        private bool TryGetActiveQuickSlot(
            string playerInstanceId,
            out int quickSlotIndex)
        {
            quickSlotIndex = _equipService.ActiveSlotIndex;

            return quickSlotIndex >= 0 &&
                   _playerInventory.QuickSlots != null &&
                   quickSlotIndex < _playerInventory.QuickSlots.SlotCount &&
                   string.Equals(
                       _playerInventory.QuickSlots.GetAssignedInstanceId(
                           quickSlotIndex),
                       playerInstanceId,
                       StringComparison.Ordinal);
        }

        private static bool TryDetachChestItem(
            ChestInventoryController chest,
            string chestInstanceId,
            out ChestDetachedItem detachedItem,
            out string error)
        {
            detachedItem = default;

            if (chest == null)
            {
                error = "Chest inventory is not assigned.";
                return false;
            }

            if (!chest.IsGenerated &&
                !chest.TryEnsureGenerated(out error))
            {
                return false;
            }

            return chest.TryDetach(
                chestInstanceId,
                out detachedItem,
                out error);
        }

        private void RestoreChestAfterFailure(
            ChestInventoryController chest,
            ChestDetachedItem detachedItem,
            string operationError,
            out string error)
        {
            if (chest.TryRestoreDetached(
                    detachedItem,
                    out string restoreError))
            {
                error = operationError;
                return;
            }

            error =
                $"{operationError} Chest rollback also failed: " +
                $"{restoreError}";
        }

        private bool TryRollbackPlayerItem(
            string playerInstanceId,
            out string error)
        {
            InventoryOperationResult rollbackResult =
                _playerInventory.ConsumeRegisteredWorldItem(
                    playerInstanceId,
                    out error);

            if (rollbackResult.IsSuccess)
            {
                error = null;
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error =
                    $"Inventory removal failed: " +
                    $"{rollbackResult.FailureReason}.";
            }

            return false;
        }
    }
}
