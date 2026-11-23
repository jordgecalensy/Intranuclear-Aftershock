using System;

namespace Failsafe.Inventory.Core
{
    public sealed class InventoryItemDefinition
    {
        public string DefinitionId { get; }
        public InventoryGridSize Footprint { get; }
        public int MaxStack { get; }
        public bool CanRotate { get; }
        public bool CanAssignQuickSlot { get; }

        public InventoryItemDefinition(
            string definitionId,
            InventoryGridSize footprint,
            int maxStack = 1,
            bool canRotate = true,
            bool canAssignQuickSlot = true)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException(
                    "Definition ID cannot be empty.",
                    nameof(definitionId));
            }

            if (maxStack <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxStack),
                    "Max stack must be greater than zero.");
            }

            DefinitionId = definitionId;
            Footprint = footprint;
            MaxStack = maxStack;
            CanRotate = canRotate;
            CanAssignQuickSlot = canAssignQuickSlot;
        }
    }
}
