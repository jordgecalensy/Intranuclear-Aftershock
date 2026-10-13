using System;

namespace Failsafe.Inventory.Core
{
    public sealed class InventoryQuickSlots : IDisposable
    {
        public const int DefaultSlotCount = 5;

        public int SlotCount => _assignedInstanceIds.Length;

        public event Action<int, string> SlotChanged;

        private readonly InventoryGridModel _grid;
        private readonly string[] _assignedInstanceIds;
        private bool _isDisposed;

        public InventoryQuickSlots(
            InventoryGridModel grid,
            int slotCount = DefaultSlotCount)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            _assignedInstanceIds = new string[slotCount];
            _grid.ItemRemoved += HandleItemRemoved;
        }

        public InventoryOperationResult Assign(
            int slotIndex,
            string instanceId)
        {
            if (!IsValidSlot(slotIndex))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.QuickSlotOutOfRange);
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidInstanceId);
            }

            if (!_grid.TryGetItem(instanceId, out InventoryItemModel item))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);
            }

            if (!item.CanAssignQuickSlot)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.QuickSlotNotAllowed,
                    item.Quantity);
            }

            for (int index = 0; index < _assignedInstanceIds.Length; index++)
            {
                if (index == slotIndex)
                    continue;

                if (string.Equals(
                        _assignedInstanceIds[index],
                        instanceId,
                        StringComparison.Ordinal))
                {
                    _assignedInstanceIds[index] = null;
                    SlotChanged?.Invoke(index, null);
                }
            }

            if (!string.Equals(
                    _assignedInstanceIds[slotIndex],
                    instanceId,
                    StringComparison.Ordinal))
            {
                _assignedInstanceIds[slotIndex] = instanceId;
                SlotChanged?.Invoke(slotIndex, instanceId);
            }

            return InventoryOperationResult.Success(remainingQuantity: item.Quantity);
        }

        public InventoryOperationResult Clear(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.QuickSlotOutOfRange);
            }

            if (_assignedInstanceIds[slotIndex] != null)
            {
                _assignedInstanceIds[slotIndex] = null;
                SlotChanged?.Invoke(slotIndex, null);
            }

            return InventoryOperationResult.Success();
        }

        public string GetAssignedInstanceId(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            return _assignedInstanceIds[slotIndex];
        }

        public bool TryGetAssignedItem(
            int slotIndex,
            out InventoryItemModel item)
        {
            item = null;

            if (!IsValidSlot(slotIndex))
                return false;

            string instanceId = _assignedInstanceIds[slotIndex];

            if (instanceId == null)
                return false;

            if (_grid.TryGetItem(instanceId, out item))
                return true;

            _assignedInstanceIds[slotIndex] = null;
            SlotChanged?.Invoke(slotIndex, null);

            return false;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _grid.ItemRemoved -= HandleItemRemoved;
            _isDisposed = true;
        }

        private bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _assignedInstanceIds.Length;
        }

        private void HandleItemRemoved(string instanceId)
        {
            for (int index = 0; index < _assignedInstanceIds.Length; index++)
            {
                if (!string.Equals(
                        _assignedInstanceIds[index],
                        instanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                _assignedInstanceIds[index] = null;
                SlotChanged?.Invoke(index, null);
            }
        }
    }
}
