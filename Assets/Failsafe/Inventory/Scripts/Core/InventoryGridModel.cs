using System;
using System.Collections.Generic;

namespace Failsafe.Inventory.Core
{
    public sealed class InventoryGridModel
    {
        public const int DefaultColumns = 6;
        public const int DefaultRows = 5;

        public int Columns { get; }
        public int Rows { get; }
        public IReadOnlyCollection<InventoryPlacement> Placements => _placements.Values;

        public event Action<InventoryPlacement> ItemPlaced;
        public event Action<InventoryPlacement> PlacementChanged;
        public event Action<InventoryItemModel> QuantityChanged;
        public event Action<string> ItemRemoved;

        private readonly InventoryPlacement[,] _occupancy;
        private readonly Dictionary<string, InventoryPlacement> _placements;

        public InventoryGridModel(
            int columns = DefaultColumns,
            int rows = DefaultRows)
        {
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));

            if (rows <= 0)
                throw new ArgumentOutOfRangeException(nameof(rows));

            Columns = columns;
            Rows = rows;
            _occupancy = new InventoryPlacement[columns, rows];
            _placements = new Dictionary<string, InventoryPlacement>(StringComparer.Ordinal);
        }

        public InventoryOperationResult TryPlace(
            InventoryItemModel item,
            InventoryGridPosition origin)
        {
            return TryPlace(
                item,
                origin,
                item?.Rotation ?? InventoryItemRotation.Default);
        }

        public InventoryOperationResult TryPlace(
            InventoryItemModel item,
            InventoryGridPosition origin,
            InventoryItemRotation targetRotation)
        {
            if (item == null)
                return InventoryOperationResult.Failure(InventoryFailureReason.InvalidItem);

            if (targetRotation != InventoryItemRotation.Default &&
                targetRotation != InventoryItemRotation.Clockwise90)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    item.Quantity);
            }

            if (targetRotation != InventoryItemRotation.Default &&
                !item.CanRotate)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.RotationNotAllowed,
                    item.Quantity);
            }

            if (_placements.ContainsKey(item.InstanceId))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.DuplicateInstanceId,
                    item.Quantity);
            }

            InventoryGridSize targetFootprint =
                item.GetFootprint(targetRotation);

            if (!CanOccupy(
                    targetFootprint,
                    origin,
                    null,
                    out InventoryFailureReason failureReason))
            {
                return InventoryOperationResult.Failure(failureReason, item.Quantity);
            }

            item.SetRotation(targetRotation);

            InventoryPlacement placement = new InventoryPlacement(item, origin);

            _placements.Add(item.InstanceId, placement);
            Occupy(placement);
            ItemPlaced?.Invoke(placement);

            return InventoryOperationResult.Success(remainingQuantity: item.Quantity);
        }

        public InventoryOperationResult TryPlaceFirstAvailable(
            InventoryItemModel item,
            out InventoryGridPosition origin)
        {
            if (!TryFindFirstFit(item, out origin))
            {
                return InventoryOperationResult.Failure(
                    item == null
                        ? InventoryFailureReason.InvalidItem
                        : InventoryFailureReason.OutOfBounds,
                    item?.Quantity ?? 0);
            }

            return TryPlace(item, origin);
        }

        public bool TryFindFirstFit(
            InventoryItemModel item,
            out InventoryGridPosition origin)
        {
            origin = default;

            if (item == null || _placements.ContainsKey(item.InstanceId))
                return false;

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    InventoryGridPosition candidate = new InventoryGridPosition(column, row);

                    if (CanOccupy(item.Footprint, candidate, null, out _))
                    {
                        origin = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        public InventoryOperationResult TryMove(
            string instanceId,
            InventoryGridPosition targetOrigin)
        {
            if (!TryGetPlacementInternal(instanceId, out InventoryPlacement placement, out InventoryOperationResult failure))
                return failure;

            if (!CanOccupy(
                    placement.Item.Footprint,
                    targetOrigin,
                    placement,
                    out InventoryFailureReason failureReason))
            {
                return InventoryOperationResult.Failure(
                    failureReason,
                    placement.Item.Quantity);
            }

            ClearOccupancy(placement);
            placement.Origin = targetOrigin;
            Occupy(placement);
            PlacementChanged?.Invoke(placement);

            return InventoryOperationResult.Success(remainingQuantity: placement.Item.Quantity);
        }

        public InventoryOperationResult TryRotate(string instanceId)
        {
            if (!TryGetPlacementInternal(instanceId, out InventoryPlacement placement, out InventoryOperationResult failure))
                return failure;

            if (!placement.Item.CanRotate)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.RotationNotAllowed,
                    placement.Item.Quantity);
            }

            InventoryItemRotation targetRotation =
                placement.Item.Rotation == InventoryItemRotation.Default
                    ? InventoryItemRotation.Clockwise90
                    : InventoryItemRotation.Default;

            InventoryGridSize targetFootprint = placement.Item.GetFootprint(targetRotation);

            if (!CanOccupy(
                    targetFootprint,
                    placement.Origin,
                    placement,
                    out InventoryFailureReason failureReason))
            {
                return InventoryOperationResult.Failure(
                    failureReason,
                    placement.Item.Quantity);
            }

            ClearOccupancy(placement);
            placement.Item.SetRotation(targetRotation);
            Occupy(placement);
            PlacementChanged?.Invoke(placement);

            return InventoryOperationResult.Success(remainingQuantity: placement.Item.Quantity);
        }

        public InventoryOperationResult TryRelocate(
            string instanceId,
            InventoryGridPosition targetOrigin,
            InventoryItemRotation targetRotation)
        {
            if (!TryGetPlacementInternal(
                    instanceId,
                    out InventoryPlacement placement,
                    out InventoryOperationResult failure))
            {
                return failure;
            }

            InventoryOperationResult validation = ValidateRelocation(
                placement,
                targetOrigin,
                targetRotation);

            if (!validation.IsSuccess)
                return validation;

            if (placement.Origin.Equals(targetOrigin) &&
                placement.Item.Rotation == targetRotation)
            {
                return validation;
            }

            ClearOccupancy(placement);
            placement.Origin = targetOrigin;
            placement.Item.SetRotation(targetRotation);
            Occupy(placement);
            PlacementChanged?.Invoke(placement);

            return InventoryOperationResult.Success(
                remainingQuantity: placement.Item.Quantity);
        }

        public InventoryOperationResult ValidateRelocation(
            string instanceId,
            InventoryGridPosition targetOrigin,
            InventoryItemRotation targetRotation)
        {
            if (!TryGetPlacementInternal(
                    instanceId,
                    out InventoryPlacement placement,
                    out InventoryOperationResult failure))
            {
                return failure;
            }

            return ValidateRelocation(
                placement,
                targetOrigin,
                targetRotation);
        }

        private InventoryOperationResult ValidateRelocation(
            InventoryPlacement placement,
            InventoryGridPosition targetOrigin,
            InventoryItemRotation targetRotation)
        {
            if (targetRotation != InventoryItemRotation.Default &&
                targetRotation != InventoryItemRotation.Clockwise90)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    placement.Item.Quantity);
            }

            if (targetRotation != placement.Item.Rotation &&
                !placement.Item.CanRotate)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.RotationNotAllowed,
                    placement.Item.Quantity);
            }

            InventoryGridSize targetFootprint =
                placement.Item.GetFootprint(targetRotation);

            if (!CanOccupy(
                    targetFootprint,
                    targetOrigin,
                    placement,
                    out InventoryFailureReason failureReason))
            {
                return InventoryOperationResult.Failure(
                    failureReason,
                    placement.Item.Quantity);
            }

            return InventoryOperationResult.Success(
                remainingQuantity: placement.Item.Quantity);
        }

        public InventoryOperationResult TryMerge(
            string sourceInstanceId,
            string targetInstanceId)
        {
            if (!TryGetPlacementInternal(sourceInstanceId, out InventoryPlacement source, out InventoryOperationResult sourceFailure))
                return sourceFailure;

            if (!TryGetPlacementInternal(targetInstanceId, out InventoryPlacement target, out InventoryOperationResult targetFailure))
                return targetFailure;

            InventoryOperationResult validation = ValidateMerge(
                source,
                target);

            if (!validation.IsSuccess)
                return validation;

            int capacity = target.Item.MaxStack - target.Item.Quantity;

            int transferred = Math.Min(capacity, source.Item.Quantity);

            target.Item.AddQuantity(transferred);
            source.Item.RemoveQuantity(transferred);

            QuantityChanged?.Invoke(target.Item);

            int remaining = source.Item.Quantity;

            if (remaining == 0)
                RemovePlacement(source);
            else
                QuantityChanged?.Invoke(source.Item);

            return InventoryOperationResult.Success(transferred, remaining);
        }

        public InventoryOperationResult ValidateMerge(
            string sourceInstanceId,
            string targetInstanceId)
        {
            if (!TryGetPlacementInternal(
                    sourceInstanceId,
                    out InventoryPlacement source,
                    out InventoryOperationResult sourceFailure))
            {
                return sourceFailure;
            }

            if (!TryGetPlacementInternal(
                    targetInstanceId,
                    out InventoryPlacement target,
                    out InventoryOperationResult targetFailure))
            {
                return targetFailure;
            }

            return ValidateMerge(source, target);
        }

        public InventoryOperationResult TryAddQuantity(
            string instanceId,
            int amount)
        {
            if (!TryGetPlacementInternal(
                    instanceId,
                    out InventoryPlacement placement,
                    out InventoryOperationResult failure))
            {
                return failure;
            }

            if (amount <= 0)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidQuantity,
                    placement.Item.Quantity);
            }

            if (placement.Item.MaxStack <= 1)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.StackingNotAllowed,
                    placement.Item.Quantity);
            }

            int capacity = placement.Item.MaxStack - placement.Item.Quantity;

            if (capacity <= 0)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.StackFull,
                    placement.Item.Quantity);
            }

            if (amount > capacity)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.StackFull,
                    placement.Item.Quantity);
            }

            placement.Item.AddQuantity(amount);
            QuantityChanged?.Invoke(placement.Item);

            return InventoryOperationResult.Success(
                amount,
                placement.Item.Quantity);
        }

        public InventoryOperationResult TryRemoveQuantity(
            string instanceId,
            int amount)
        {
            if (!TryGetPlacementInternal(instanceId, out InventoryPlacement placement, out InventoryOperationResult failure))
                return failure;

            if (amount <= 0 || amount > placement.Item.Quantity)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidQuantity,
                    placement.Item.Quantity);
            }

            placement.Item.RemoveQuantity(amount);
            int remaining = placement.Item.Quantity;

            if (remaining == 0)
                RemovePlacement(placement);
            else
                QuantityChanged?.Invoke(placement.Item);

            return InventoryOperationResult.Success(amount, remaining);
        }

        public InventoryOperationResult TryRemove(
            string instanceId,
            out InventoryItemModel removedItem)
        {
            removedItem = null;

            if (!TryGetPlacementInternal(instanceId, out InventoryPlacement placement, out InventoryOperationResult failure))
                return failure;

            removedItem = placement.Item;
            int removedQuantity = removedItem.Quantity;

            RemovePlacement(placement);

            return InventoryOperationResult.Success(removedQuantity, 0);
        }

        public InventoryOperationResult TryRemove(string instanceId)
        {
            return TryRemove(instanceId, out _);
        }

        public bool TryGetPlacement(
            string instanceId,
            out InventoryPlacement placement)
        {
            placement = null;

            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _placements.TryGetValue(instanceId, out placement);
        }

        public bool TryGetItem(
            string instanceId,
            out InventoryItemModel item)
        {
            item = null;

            if (!TryGetPlacement(instanceId, out InventoryPlacement placement))
                return false;

            item = placement.Item;
            return true;
        }

        public bool TryGetItemAt(
            InventoryGridPosition position,
            out InventoryItemModel item)
        {
            item = null;

            if (!IsInsideGrid(position))
                return false;

            InventoryPlacement placement = _occupancy[position.Column, position.Row];

            if (placement == null)
                return false;

            item = placement.Item;
            return true;
        }

        public bool IsOccupied(InventoryGridPosition position)
        {
            return IsInsideGrid(position) &&
                   _occupancy[position.Column, position.Row] != null;
        }

        private bool TryGetPlacementInternal(
            string instanceId,
            out InventoryPlacement placement,
            out InventoryOperationResult failure)
        {
            placement = null;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                failure = InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidInstanceId);

                return false;
            }

            if (!_placements.TryGetValue(instanceId, out placement))
            {
                failure = InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);

                return false;
            }

            failure = InventoryOperationResult.Success();
            return true;
        }

        private static InventoryOperationResult ValidateMerge(
            InventoryPlacement source,
            InventoryPlacement target)
        {
            if (source == target || string.Equals(
                    source.Item.InstanceId,
                    target.Item.InstanceId,
                    StringComparison.Ordinal))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.IncompatibleItems,
                    source.Item.Quantity);
            }

            if (source.Item.MaxStack <= 1 || target.Item.MaxStack <= 1)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.StackingNotAllowed,
                    source.Item.Quantity);
            }

            if (!string.Equals(
                    source.Item.DefinitionId,
                    target.Item.DefinitionId,
                    StringComparison.Ordinal) ||
                source.Item.MaxStack != target.Item.MaxStack)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.IncompatibleItems,
                    source.Item.Quantity);
            }

            if (target.Item.Quantity >= target.Item.MaxStack)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.StackFull,
                    source.Item.Quantity);
            }

            return InventoryOperationResult.Success(
                remainingQuantity: source.Item.Quantity);
        }

        private bool CanOccupy(
            InventoryGridSize footprint,
            InventoryGridPosition origin,
            InventoryPlacement ignoredPlacement,
            out InventoryFailureReason failureReason)
        {
            if (origin.Column < 0 ||
                origin.Row < 0 ||
                origin.Column + footprint.Width > Columns ||
                origin.Row + footprint.Height > Rows)
            {
                failureReason = InventoryFailureReason.OutOfBounds;
                return false;
            }

            for (int column = origin.Column;
                 column < origin.Column + footprint.Width;
                 column++)
            {
                for (int row = origin.Row;
                     row < origin.Row + footprint.Height;
                     row++)
                {
                    InventoryPlacement occupant = _occupancy[column, row];

                    if (occupant != null && occupant != ignoredPlacement)
                    {
                        failureReason = InventoryFailureReason.Overlap;
                        return false;
                    }
                }
            }

            failureReason = InventoryFailureReason.None;
            return true;
        }

        private bool IsInsideGrid(InventoryGridPosition position)
        {
            return position.Column >= 0 &&
                   position.Row >= 0 &&
                   position.Column < Columns &&
                   position.Row < Rows;
        }

        private void Occupy(InventoryPlacement placement)
        {
            InventoryGridSize footprint = placement.Item.Footprint;

            for (int column = placement.Origin.Column;
                 column < placement.Origin.Column + footprint.Width;
                 column++)
            {
                for (int row = placement.Origin.Row;
                     row < placement.Origin.Row + footprint.Height;
                     row++)
                {
                    _occupancy[column, row] = placement;
                }
            }
        }

        private void ClearOccupancy(InventoryPlacement placement)
        {
            for (int column = 0; column < Columns; column++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    if (_occupancy[column, row] == placement)
                        _occupancy[column, row] = null;
                }
            }
        }

        private void RemovePlacement(InventoryPlacement placement)
        {
            ClearOccupancy(placement);
            _placements.Remove(placement.Item.InstanceId);
            ItemRemoved?.Invoke(placement.Item.InstanceId);
        }
    }
}
