using System;

namespace Failsafe.Inventory.Core
{
    public enum InventoryItemRotation
    {
        Default = 0,
        Clockwise90 = 1
    }

    public enum InventoryFailureReason
    {
        None = 0,
        InvalidItem,
        InvalidInstanceId,
        InvalidQuantity,
        DuplicateInstanceId,
        ItemNotFound,
        OutOfBounds,
        Overlap,
        RotationNotAllowed,
        StackingNotAllowed,
        IncompatibleItems,
        StackFull,
        QuickSlotOutOfRange,
        QuickSlotNotAllowed
    }

    public readonly struct InventoryGridPosition : IEquatable<InventoryGridPosition>
    {
        public int Column { get; }
        public int Row { get; }

        public InventoryGridPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public bool Equals(InventoryGridPosition other)
        {
            return Column == other.Column && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryGridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Column * 397) ^ Row;
            }
        }

        public override string ToString()
        {
            return $"({Column}, {Row})";
        }
    }

    public readonly struct InventoryGridSize : IEquatable<InventoryGridSize>
    {
        public int Width { get; }
        public int Height { get; }

        public InventoryGridSize(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");

            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");

            Width = width;
            Height = height;
        }

        public InventoryGridSize Rotated()
        {
            return new InventoryGridSize(Height, Width);
        }

        public bool Equals(InventoryGridSize other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryGridSize other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }

    public readonly struct InventoryOperationResult
    {
        public bool IsSuccess { get; }
        public InventoryFailureReason FailureReason { get; }
        public int TransferredQuantity { get; }
        public int RemainingQuantity { get; }

        private InventoryOperationResult(
            bool isSuccess,
            InventoryFailureReason failureReason,
            int transferredQuantity,
            int remainingQuantity)
        {
            IsSuccess = isSuccess;
            FailureReason = failureReason;
            TransferredQuantity = transferredQuantity;
            RemainingQuantity = remainingQuantity;
        }

        public static InventoryOperationResult Success(
            int transferredQuantity = 0,
            int remainingQuantity = 0)
        {
            return new InventoryOperationResult(
                true,
                InventoryFailureReason.None,
                transferredQuantity,
                remainingQuantity);
        }

        public static InventoryOperationResult Failure(
            InventoryFailureReason reason,
            int remainingQuantity = 0)
        {
            if (reason == InventoryFailureReason.None)
                throw new ArgumentException("A failed operation must have a failure reason.", nameof(reason));

            return new InventoryOperationResult(
                false,
                reason,
                0,
                remainingQuantity);
        }
    }
}
