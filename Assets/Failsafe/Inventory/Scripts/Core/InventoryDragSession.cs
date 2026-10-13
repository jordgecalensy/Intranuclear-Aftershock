using System;

namespace Failsafe.Inventory.Core
{
    public sealed class InventoryDragSession
    {
        public string InstanceId { get; }
        public InventoryGridPosition InitialOrigin { get; }
        public InventoryItemRotation InitialRotation { get; }
        public InventoryGridSize BaseFootprint { get; }
        public bool CanRotate { get; }

        public InventoryGridPosition PointerCell { get; private set; }
        public InventoryGridPosition GrabOffset { get; private set; }
        public InventoryGridPosition TargetOrigin { get; private set; }
        public InventoryItemRotation TargetRotation { get; private set; }

        public InventoryGridSize TargetFootprint =>
            TargetRotation == InventoryItemRotation.Clockwise90
                ? BaseFootprint.Rotated()
                : BaseFootprint;

        private readonly InventoryGridPosition _baseGrabOffset;

        public InventoryDragSession(
            InventoryPlacement placement,
            InventoryGridPosition grabbedCell)
        {
            if (placement == null)
                throw new ArgumentNullException(nameof(placement));

            if (!placement.Contains(grabbedCell))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(grabbedCell),
                    "The grabbed cell must be occupied by the dragged item.");
            }

            InventoryItemModel item = placement.Item;
            InstanceId = item.InstanceId;
            InitialOrigin = placement.Origin;
            InitialRotation = item.Rotation;
            BaseFootprint = item.BaseFootprint;
            CanRotate = item.CanRotate;

            InventoryGridPosition currentOffset = new InventoryGridPosition(
                grabbedCell.Column - placement.Origin.Column,
                grabbedCell.Row - placement.Origin.Row);

            _baseGrabOffset = ConvertToBaseOffset(
                currentOffset,
                item.Rotation,
                BaseFootprint);

            TargetRotation = item.Rotation;
            PointerCell = grabbedCell;
            RecalculateTarget();
        }

        public void UpdatePointer(InventoryGridPosition pointerCell)
        {
            PointerCell = pointerCell;
            RecalculateTarget();
        }

        public bool TryToggleRotation()
        {
            if (!CanRotate)
                return false;

            TargetRotation =
                TargetRotation == InventoryItemRotation.Default
                    ? InventoryItemRotation.Clockwise90
                    : InventoryItemRotation.Default;

            RecalculateTarget();
            return true;
        }

        private void RecalculateTarget()
        {
            GrabOffset = ConvertFromBaseOffset(
                _baseGrabOffset,
                TargetRotation,
                BaseFootprint);

            TargetOrigin = new InventoryGridPosition(
                PointerCell.Column - GrabOffset.Column,
                PointerCell.Row - GrabOffset.Row);
        }

        private static InventoryGridPosition ConvertToBaseOffset(
            InventoryGridPosition currentOffset,
            InventoryItemRotation rotation,
            InventoryGridSize baseFootprint)
        {
            switch (rotation)
            {
                case InventoryItemRotation.Default:
                    return currentOffset;

                case InventoryItemRotation.Clockwise90:
                    return new InventoryGridPosition(
                        currentOffset.Row,
                        baseFootprint.Height - 1 - currentOffset.Column);

                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }

        private static InventoryGridPosition ConvertFromBaseOffset(
            InventoryGridPosition baseOffset,
            InventoryItemRotation rotation,
            InventoryGridSize baseFootprint)
        {
            switch (rotation)
            {
                case InventoryItemRotation.Default:
                    return baseOffset;

                case InventoryItemRotation.Clockwise90:
                    return new InventoryGridPosition(
                        baseFootprint.Height - 1 - baseOffset.Row,
                        baseOffset.Column);

                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }
    }
}
