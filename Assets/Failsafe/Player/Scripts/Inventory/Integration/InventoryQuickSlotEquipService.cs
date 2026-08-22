using System;
using Failsafe.PlayerMovements;
using VContainer.Unity;

namespace Failsafe.Inventory.Integration
{
    public sealed class InventoryQuickSlotEquipService :
        IInitializable,
        IDisposable
    {
        public const int NoActiveSlot = -1;

        private readonly InventoryRuntimeController _inventory;
        private readonly PlayerHandsContainer _hands;
        private readonly PlayerControlBlocker _controlBlocker;

        private bool _isInitialized;

        public int ActiveSlotIndex { get; private set; } = NoActiveSlot;

        public event Action<int> ActiveSlotChanged;

        public InventoryQuickSlotEquipService(
            InventoryRuntimeController inventory,
            PlayerHandsContainer hands,
            PlayerControlBlocker controlBlocker)
        {
            _inventory = inventory ??
                throw new ArgumentNullException(nameof(inventory));
            _hands = hands ?? throw new ArgumentNullException(nameof(hands));
            _controlBlocker = controlBlocker ??
                throw new ArgumentNullException(nameof(controlBlocker));
        }

        public void Initialize()
        {
            TryEnsureInitialized(out _);
        }

        public void Dispose()
        {
            if (!_isInitialized)
                return;

            if (_inventory.QuickSlots != null)
            {
                _inventory.QuickSlots.SlotChanged -=
                    HandleSlotChanged;
            }

            _hands.OnItemDropped -= HandleItemLeftHand;
            _isInitialized = false;
        }

        public bool TrySelectSlot(int slotIndex, out string error)
        {
            if (!TryEnsureInitialized(out error))
                return false;

            if (_controlBlocker.IsBlocked(PlayerControlBlock.Inventory) ||
                _controlBlocker.IsLockedBy(
                    PlayerControlLockIds.InventoryOpened))
            {
                error = null;
                return true;
            }

            if (slotIndex < 0 ||
                slotIndex >= _inventory.QuickSlots.SlotCount)
            {
                error = $"Quick-slot index {slotIndex} is out of range.";
                return false;
            }

            string targetInstanceId =
                _inventory.QuickSlots.GetAssignedInstanceId(slotIndex);

            if (string.IsNullOrWhiteSpace(targetInstanceId))
            {
                error = null;
                return true;
            }

            if (!_inventory.TryGetWorldItem(
                    targetInstanceId,
                    out Item targetItem))
            {
                error =
                    $"Quick slot {slotIndex + 1} points to an item " +
                    $"without a registered world object.";

                return false;
            }

            Item currentItem = _hands.ItemInHand?.ItemObject;

            if (currentItem == targetItem)
            {
                return TryStowCurrentItem(
                    targetInstanceId,
                    currentItem,
                    out error);
            }

            Item previousItem = null;
            string previousInstanceId = null;
            int previousActiveSlotIndex = ActiveSlotIndex;

            if (currentItem != null)
            {
                if (!_inventory.TryGetWorldItemInstanceId(
                        currentItem,
                        out previousInstanceId))
                {
                    error =
                        $"The item currently held in hand is not registered " +
                        $"in the inventory and cannot be switched safely.";

                    return false;
                }

                previousItem = _hands.StowItemFromHand();

                if (previousItem != currentItem)
                {
                    error =
                        "The hand system returned a different item while " +
                        "stowing.";
                    TryRestoreItemInHand(
                        previousItem,
                        previousActiveSlotIndex);
                    return false;
                }

                if (!_inventory.TryMoveRegisteredWorldItemToStorage(
                        previousInstanceId,
                        out error))
                {
                    TryRestoreItemInHand(
                        previousItem,
                        previousActiveSlotIndex);
                    return false;
                }
            }

            if (!_hands.TryTakeItemInHand(targetItem))
            {
                bool previousRestored =
                    TryRestoreItemInHand(
                        previousItem,
                        previousActiveSlotIndex);

                error = previousRestored
                    ? $"Inventory item '{targetInstanceId}' was rejected " +
                      $"by the hand system."
                    : $"Inventory item '{targetInstanceId}' was rejected " +
                      $"by the hand system, and the previous item could not " +
                      $"be restored.";

                return false;
            }

            SetActiveSlot(slotIndex);
            error = null;
            return true;
        }

        private bool TryStowCurrentItem(
            string instanceId,
            Item currentItem,
            out string error)
        {
            int previousActiveSlotIndex = ActiveSlotIndex;
            Item stowedItem = _hands.StowItemFromHand();

            if (stowedItem != currentItem)
            {
                error = "The hand system returned a different item while stowing.";
                TryRestoreItemInHand(
                    stowedItem,
                    previousActiveSlotIndex);
                return false;
            }

            if (!_inventory.TryMoveRegisteredWorldItemToStorage(
                    instanceId,
                    out error))
            {
                TryRestoreItemInHand(
                    stowedItem,
                    previousActiveSlotIndex);
                return false;
            }

            SetActiveSlot(NoActiveSlot);
            return true;
        }

        private bool TryEnsureInitialized(out string error)
        {
            if (_isInitialized)
            {
                error = null;
                return true;
            }

            if (!_inventory.IsInitialized &&
                !_inventory.TryInitialize(out error))
            {
                return false;
            }

            if (_inventory.QuickSlots == null)
            {
                error = "Inventory quick slots are not initialized.";
                return false;
            }

            _inventory.QuickSlots.SlotChanged += HandleSlotChanged;
            _hands.OnItemDropped += HandleItemLeftHand;
            _isInitialized = true;
            error = null;
            return true;
        }

        private bool TryRestoreItemInHand(
            Item item,
            int activeSlotIndex)
        {
            if (item == null)
                return true;

            if (!_hands.TryTakeItemInHand(item))
                return false;

            SetActiveSlot(activeSlotIndex);
            return true;
        }

        private void HandleSlotChanged(int slotIndex, string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                if (ActiveSlotIndex == slotIndex)
                    SetActiveSlot(NoActiveSlot);

                return;
            }

            Item itemInHand = _hands.ItemInHand?.ItemObject;

            if (itemInHand != null &&
                _inventory.TryGetWorldItem(
                    instanceId,
                    out Item assignedItem) &&
                itemInHand == assignedItem)
            {
                SetActiveSlot(slotIndex);
                return;
            }

            if (ActiveSlotIndex == slotIndex)
                SetActiveSlot(NoActiveSlot);
        }

        private void HandleItemLeftHand()
        {
            SetActiveSlot(NoActiveSlot);
        }

        private void SetActiveSlot(int slotIndex)
        {
            if (ActiveSlotIndex == slotIndex)
                return;

            ActiveSlotIndex = slotIndex;
            ActiveSlotChanged?.Invoke(slotIndex);
        }
    }
}
