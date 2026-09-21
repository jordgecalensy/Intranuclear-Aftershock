using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;

namespace Failsafe.Chests
{
    public readonly struct ChestGridPlacement
    {
        public int SelectionIndex { get; }
        public ChestLootEntry Entry { get; }
        public InventoryGridPosition Origin { get; }
        public InventoryItemRotation Rotation { get; }

        public ChestGridPlacement(
            int selectionIndex,
            ChestLootEntry entry,
            InventoryGridPosition origin,
            InventoryItemRotation rotation)
        {
            SelectionIndex = selectionIndex;
            Entry = entry;
            Origin = origin;
            Rotation = rotation;
        }
    }

    public static class ChestGridPackingSolver
    {
        private const int StandaloneSearchOperations = 100000;

        public static bool TryPack(
            IReadOnlyList<ChestLootEntry> selection,
            int columns,
            int rows,
            out List<ChestGridPlacement> placements,
            out string error)
        {
            int remainingSearchOperations = StandaloneSearchOperations;

            return TryPack(
                selection,
                columns,
                rows,
                ref remainingSearchOperations,
                out placements,
                out error);
        }

        internal static bool TryPack(
            IReadOnlyList<ChestLootEntry> selection,
            int columns,
            int rows,
            ref int remainingSearchOperations,
            out List<ChestGridPlacement> placements,
            out string error)
        {
            placements = null;

            if (selection == null)
            {
                error = "Chest loot selection is null.";
                return false;
            }

            if (columns <= 0 || rows <= 0)
            {
                error = "Chest grid dimensions must be greater than zero.";
                return false;
            }

            var order = new List<int>(selection.Count);
            long occupiedArea = 0;

            for (int index = 0; index < selection.Count; index++)
            {
                ChestLootEntry entry = selection[index];

                if (entry?.ItemData == null)
                {
                    error = $"Chest loot selection entry {index} has no ItemData.";
                    return false;
                }

                int width = entry.ItemData.InventoryWidth;
                int height = entry.ItemData.InventoryHeight;

                if (width <= 0 || height <= 0)
                {
                    error =
                        $"Chest loot entry '{entry.Id}' has invalid inventory " +
                        $"dimensions {width}x{height}.";
                    return false;
                }

                occupiedArea += (long)width * height;
                order.Add(index);
            }

            long gridArea = (long)columns * rows;

            if (occupiedArea > gridArea)
            {
                error =
                    $"Chest loot needs {occupiedArea} cells, but the grid only has " +
                    $"{gridArea}.";
                return false;
            }

            order.Sort((left, right) =>
            {
                ItemData leftItem = selection[left].ItemData;
                ItemData rightItem = selection[right].ItemData;
                long leftArea =
                    (long)leftItem.InventoryWidth * leftItem.InventoryHeight;
                long rightArea =
                    (long)rightItem.InventoryWidth * rightItem.InventoryHeight;
                int areaComparison = rightArea.CompareTo(leftArea);

                if (areaComparison != 0)
                    return areaComparison;

                int leftLongestSide = Math.Max(
                    leftItem.InventoryWidth,
                    leftItem.InventoryHeight);
                int rightLongestSide = Math.Max(
                    rightItem.InventoryWidth,
                    rightItem.InventoryHeight);

                int longestSideComparison =
                    rightLongestSide.CompareTo(leftLongestSide);

                if (longestSideComparison != 0)
                    return longestSideComparison;

                int widthComparison =
                    rightItem.InventoryWidth.CompareTo(leftItem.InventoryWidth);

                if (widthComparison != 0)
                    return widthComparison;

                int heightComparison =
                    rightItem.InventoryHeight.CompareTo(leftItem.InventoryHeight);

                if (heightComparison != 0)
                    return heightComparison;

                int rotationComparison =
                    rightItem.CanRotateInInventory.CompareTo(
                        leftItem.CanRotateInInventory);

                return rotationComparison != 0
                    ? rotationComparison
                    : left.CompareTo(right);
            });

            var occupancy = new bool[columns, rows];
            var placementsBySelectionIndex =
                new ChestGridPlacement[selection.Count];
            bool searchLimitReached = false;

            if (!TryPlaceNext(
                    selection,
                    order,
                    placementIndex: 0,
                    columns,
                    rows,
                    occupancy,
                    placementsBySelectionIndex,
                    ref remainingSearchOperations,
                    ref searchLimitReached))
            {
                error = searchLimitReached
                    ? $"Chest layout search exhausted its shared operation " +
                      $"budget for a {columns}x{rows} grid."
                    : $"The selected chest loot cannot fit inside a " +
                      $"{columns}x{rows} grid.";
                return false;
            }

            placements = new List<ChestGridPlacement>(
                placementsBySelectionIndex);
            error = null;
            return true;
        }

        private static bool TryPlaceNext(
            IReadOnlyList<ChestLootEntry> selection,
            IReadOnlyList<int> order,
            int placementIndex,
            int columns,
            int rows,
            bool[,] occupancy,
            ChestGridPlacement[] placements,
            ref int remainingSearchOperations,
            ref bool searchLimitReached)
        {
            if (!TryConsumeSearchOperation(
                    ref remainingSearchOperations,
                    ref searchLimitReached))
                return false;

            if (placementIndex >= order.Count)
                return true;

            int selectionIndex = order[placementIndex];
            ChestLootEntry entry = selection[selectionIndex];
            ItemData itemData = entry.ItemData;
            int minimumEquivalentPlacementKey = -1;

            if (placementIndex > 0)
            {
                int previousSelectionIndex = order[placementIndex - 1];
                ItemData previousItemData =
                    selection[previousSelectionIndex].ItemData;

                if (HaveEquivalentFootprints(itemData, previousItemData))
                {
                    ChestGridPlacement previousPlacement =
                        placements[previousSelectionIndex];

                    minimumEquivalentPlacementKey = GetPlacementKey(
                        previousPlacement.Origin,
                        previousPlacement.Rotation,
                        columns);
                }
            }

            if (TryPlaceOrientation(
                    selection,
                    order,
                    placementIndex,
                    selectionIndex,
                    entry,
                    itemData.InventoryWidth,
                    itemData.InventoryHeight,
                    InventoryItemRotation.Default,
                    columns,
                    rows,
                    occupancy,
                    placements,
                    minimumEquivalentPlacementKey,
                    ref remainingSearchOperations,
                    ref searchLimitReached))
            {
                return true;
            }

            if (searchLimitReached)
                return false;

            return itemData.CanRotateInInventory &&
                   itemData.InventoryWidth != itemData.InventoryHeight &&
                   TryPlaceOrientation(
                       selection,
                       order,
                       placementIndex,
                       selectionIndex,
                       entry,
                       itemData.InventoryHeight,
                       itemData.InventoryWidth,
                       InventoryItemRotation.Clockwise90,
                       columns,
                       rows,
                       occupancy,
                       placements,
                       minimumEquivalentPlacementKey,
                       ref remainingSearchOperations,
                       ref searchLimitReached);
        }

        private static bool TryPlaceOrientation(
            IReadOnlyList<ChestLootEntry> selection,
            IReadOnlyList<int> order,
            int placementIndex,
            int selectionIndex,
            ChestLootEntry entry,
            int width,
            int height,
            InventoryItemRotation rotation,
            int columns,
            int rows,
            bool[,] occupancy,
            ChestGridPlacement[] placements,
            int minimumEquivalentPlacementKey,
            ref int remainingSearchOperations,
            ref bool searchLimitReached)
        {
            for (int row = 0; row <= rows - height; row++)
            {
                for (int column = 0; column <= columns - width; column++)
                {
                    if (!TryConsumeSearchOperation(
                            ref remainingSearchOperations,
                            ref searchLimitReached))
                        return false;

                    var origin = new InventoryGridPosition(column, row);

                    if (GetPlacementKey(origin, rotation, columns) <=
                        minimumEquivalentPlacementKey)
                    {
                        continue;
                    }

                    if (!CanOccupy(
                            occupancy,
                            column,
                            row,
                            width,
                            height))
                    {
                        continue;
                    }

                    SetOccupied(
                        occupancy,
                        column,
                        row,
                        width,
                        height,
                        true);

                    placements[selectionIndex] = new ChestGridPlacement(
                        selectionIndex,
                        entry,
                        origin,
                        rotation);

                    if (TryPlaceNext(
                            selection,
                            order,
                            placementIndex + 1,
                            columns,
                            rows,
                            occupancy,
                            placements,
                            ref remainingSearchOperations,
                            ref searchLimitReached))
                    {
                        return true;
                    }

                    SetOccupied(
                        occupancy,
                        column,
                        row,
                        width,
                        height,
                        false);

                    if (searchLimitReached)
                        return false;
                }
            }

            return false;
        }

        private static bool TryConsumeSearchOperation(
            ref int remainingSearchOperations,
            ref bool searchLimitReached)
        {
            if (remainingSearchOperations <= 0)
            {
                searchLimitReached = true;
                return false;
            }

            remainingSearchOperations--;
            return true;
        }

        private static bool HaveEquivalentFootprints(
            ItemData first,
            ItemData second)
        {
            return first != null &&
                   second != null &&
                   first.InventoryWidth == second.InventoryWidth &&
                   first.InventoryHeight == second.InventoryHeight &&
                   first.CanRotateInInventory == second.CanRotateInInventory;
        }

        private static int GetPlacementKey(
            InventoryGridPosition origin,
            InventoryItemRotation rotation,
            int columns)
        {
            return (((origin.Row * columns) + origin.Column) * 2) +
                   (int)rotation;
        }

        private static bool CanOccupy(
            bool[,] occupancy,
            int originColumn,
            int originRow,
            int width,
            int height)
        {
            for (int row = originRow; row < originRow + height; row++)
            {
                for (int column = originColumn;
                     column < originColumn + width;
                     column++)
                {
                    if (occupancy[column, row])
                        return false;
                }
            }

            return true;
        }

        private static void SetOccupied(
            bool[,] occupancy,
            int originColumn,
            int originRow,
            int width,
            int height,
            bool occupied)
        {
            for (int row = originRow; row < originRow + height; row++)
            {
                for (int column = originColumn;
                     column < originColumn + width;
                     column++)
                {
                    occupancy[column, row] = occupied;
                }
            }
        }
    }
}
