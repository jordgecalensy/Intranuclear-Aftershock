using System;

namespace Failsafe.Inventory.Core
{
    public sealed class InventoryItemModel
    {
        public string InstanceId { get; }
        public InventoryItemDefinition Definition { get; }
        public string DefinitionId => Definition.DefinitionId;
        public InventoryGridSize BaseFootprint => Definition.Footprint;
        public int MaxStack => Definition.MaxStack;
        public bool CanRotate => Definition.CanRotate;
        public bool CanAssignQuickSlot => Definition.CanAssignQuickSlot;

        public int Quantity { get; private set; }
        public InventoryItemRotation Rotation { get; private set; }
        public InventoryGridSize Footprint => GetFootprint(Rotation);

        public InventoryItemModel(
            string instanceId,
            string definitionId,
            InventoryGridSize baseFootprint,
            int quantity = 1,
            int maxStack = 1,
            bool canRotate = true,
            bool canAssignQuickSlot = true)
            : this(
                instanceId,
                new InventoryItemDefinition(
                    definitionId,
                    baseFootprint,
                    maxStack,
                    canRotate,
                    canAssignQuickSlot),
                quantity)
        {
        }

        public InventoryItemModel(
            string instanceId,
            InventoryItemDefinition definition,
            int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("Instance ID cannot be empty.", nameof(instanceId));

            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (quantity <= 0 || quantity > definition.MaxStack)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Quantity must be greater than zero and cannot exceed max stack.");
            }

            InstanceId = instanceId;
            Definition = definition;
            Quantity = quantity;
            Rotation = InventoryItemRotation.Default;
        }

        public InventoryGridSize GetFootprint(InventoryItemRotation rotation)
        {
            return rotation == InventoryItemRotation.Clockwise90
                ? BaseFootprint.Rotated()
                : BaseFootprint;
        }

        internal void SetRotation(InventoryItemRotation rotation)
        {
            Rotation = rotation;
        }

        internal void AddQuantity(int amount)
        {
            if (amount <= 0 || Quantity + amount > MaxStack)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Quantity += amount;
        }

        internal void RemoveQuantity(int amount)
        {
            if (amount <= 0 || amount > Quantity)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Quantity -= amount;
        }
    }
}
