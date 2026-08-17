using NUnit.Framework;

namespace Failsafe.Inventory.Core.Tests
{
    [TestFixture]
    public sealed class InventoryGridModelTests
    {
        [Test]
        public void Constructor_UsesSixByFiveGridByDefault()
        {
            InventoryGridModel grid = new InventoryGridModel();

            Assert.That(grid.Columns, Is.EqualTo(6));
            Assert.That(grid.Rows, Is.EqualTo(5));
        }

        [Test]
        public void TryPlace_AllowsMultiCellItemInsideGrid()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 3);

            InventoryOperationResult result = grid.TryPlace(
                item,
                new InventoryGridPosition(4, 2));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(4, 2)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(5, 4)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(3, 2)), Is.False);
        }

        [Test]
        public void TryPlace_RejectsOutOfBoundsItemWithoutChangingGrid()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 2);

            InventoryOperationResult result = grid.TryPlace(
                item,
                new InventoryGridPosition(5, 4));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.OutOfBounds));
            Assert.That(grid.Placements, Is.Empty);
        }

        [Test]
        public void TryPlace_RejectsOverlap()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel first = CreateItem("first", width: 2, height: 2);
            InventoryItemModel second = CreateItem("second");

            grid.TryPlace(first, new InventoryGridPosition(0, 0));

            InventoryOperationResult result = grid.TryPlace(
                second,
                new InventoryGridPosition(1, 1));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.Overlap));
            Assert.That(grid.Placements.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryRotate_ChangesFootprintAndKeepsOrigin()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 1);

            grid.TryPlace(item, new InventoryGridPosition(1, 1));

            InventoryOperationResult result = grid.TryRotate(item.InstanceId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Clockwise90));
            Assert.That(item.Footprint, Is.EqualTo(new InventoryGridSize(1, 2)));
            Assert.That(grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement), Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(1, 1)));
            Assert.That(grid.IsOccupied(new InventoryGridPosition(1, 2)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(2, 1)), Is.False);
        }

        [Test]
        public void TryRotate_WhenOutOfBounds_LeavesOriginalPlacementUntouched()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 1);

            grid.TryPlace(item, new InventoryGridPosition(4, 4));

            InventoryOperationResult result = grid.TryRotate(item.InstanceId);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.OutOfBounds));
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Default));
            Assert.That(grid.IsOccupied(new InventoryGridPosition(4, 4)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(5, 4)), Is.True);
        }

        [Test]
        public void TryMove_WhenTargetIsInvalid_IsAtomic()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 2);

            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            InventoryOperationResult result = grid.TryMove(
                item.InstanceId,
                new InventoryGridPosition(5, 4));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement), Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(0, 0)));
            Assert.That(grid.IsOccupied(new InventoryGridPosition(0, 0)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(1, 1)), Is.True);
        }

        [Test]
        public void TryRelocate_MovesAndRotatesWithSingleChangeEvent()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 1);
            int changeEventCount = 0;

            grid.TryPlace(item, new InventoryGridPosition(0, 0));
            grid.PlacementChanged += _ => changeEventCount++;

            InventoryOperationResult result = grid.TryRelocate(
                item.InstanceId,
                new InventoryGridPosition(3, 1),
                InventoryItemRotation.Clockwise90);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(changeEventCount, Is.EqualTo(1));
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Clockwise90));
            Assert.That(
                grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement),
                Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(3, 1)));
            Assert.That(placement.Footprint, Is.EqualTo(new InventoryGridSize(1, 2)));
            Assert.That(grid.IsOccupied(new InventoryGridPosition(3, 1)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(3, 2)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(0, 0)), Is.False);
        }

        [Test]
        public void TryRelocate_WhenTargetOverlaps_RestoresOriginRotationAndOccupancy()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 1);
            InventoryItemModel blocker = CreateItem("blocker");

            grid.TryPlace(item, new InventoryGridPosition(0, 0));
            grid.TryPlace(blocker, new InventoryGridPosition(3, 2));

            InventoryOperationResult result = grid.TryRelocate(
                item.InstanceId,
                new InventoryGridPosition(3, 1),
                InventoryItemRotation.Clockwise90);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.Overlap));
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Default));
            Assert.That(
                grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement),
                Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(0, 0)));
            Assert.That(grid.IsOccupied(new InventoryGridPosition(0, 0)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(1, 0)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(3, 1)), Is.False);
            Assert.That(
                grid.TryGetItemAt(
                    new InventoryGridPosition(3, 2),
                    out InventoryItemModel occupant),
                Is.True);
            Assert.That(occupant, Is.SameAs(blocker));
        }

        [Test]
        public void TryRelocate_WhenRotationIsNotAllowed_LeavesPlacementUntouched()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem(
                "item",
                width: 2,
                height: 1,
                canRotate: false);

            grid.TryPlace(item, new InventoryGridPosition(1, 1));

            InventoryOperationResult result = grid.TryRelocate(
                item.InstanceId,
                new InventoryGridPosition(2, 2),
                InventoryItemRotation.Clockwise90);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(InventoryFailureReason.RotationNotAllowed));
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Default));
            Assert.That(
                grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement),
                Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(1, 1)));
            Assert.That(grid.IsOccupied(new InventoryGridPosition(1, 1)), Is.True);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(2, 1)), Is.True);
        }

        [Test]
        public void TryMerge_TransfersOnlyAvailableCapacityAndKeepsRemainder()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel source = CreateItem(
                "source",
                definitionId: "grenade",
                quantity: 4,
                maxStack: 5);
            InventoryItemModel target = CreateItem(
                "target",
                definitionId: "grenade",
                quantity: 3,
                maxStack: 5);

            grid.TryPlace(source, new InventoryGridPosition(0, 0));
            grid.TryPlace(target, new InventoryGridPosition(1, 0));

            InventoryOperationResult result = grid.TryMerge(
                source.InstanceId,
                target.InstanceId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.TransferredQuantity, Is.EqualTo(2));
            Assert.That(result.RemainingQuantity, Is.EqualTo(2));
            Assert.That(source.Quantity, Is.EqualTo(2));
            Assert.That(target.Quantity, Is.EqualTo(5));
            Assert.That(grid.TryGetPlacement(source.InstanceId, out _), Is.True);
        }

        [Test]
        public void TryMerge_WhenSourceBecomesEmpty_RemovesSourcePlacement()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel source = CreateItem(
                "source",
                definitionId: "grenade",
                quantity: 2,
                maxStack: 5);
            InventoryItemModel target = CreateItem(
                "target",
                definitionId: "grenade",
                quantity: 3,
                maxStack: 5);

            grid.TryPlace(source, new InventoryGridPosition(0, 0));
            grid.TryPlace(target, new InventoryGridPosition(1, 0));

            InventoryOperationResult result = grid.TryMerge(
                source.InstanceId,
                target.InstanceId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.RemainingQuantity, Is.Zero);
            Assert.That(target.Quantity, Is.EqualTo(5));
            Assert.That(grid.TryGetPlacement(source.InstanceId, out _), Is.False);
            Assert.That(grid.IsOccupied(new InventoryGridPosition(0, 0)), Is.False);
        }

        [Test]
        public void TryMerge_RejectsDifferentDefinitions()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel source = CreateItem(
                "source",
                definitionId: "frag",
                quantity: 1,
                maxStack: 5);
            InventoryItemModel target = CreateItem(
                "target",
                definitionId: "stasis",
                quantity: 1,
                maxStack: 5);

            grid.TryPlace(source, new InventoryGridPosition(0, 0));
            grid.TryPlace(target, new InventoryGridPosition(1, 0));

            InventoryOperationResult result = grid.TryMerge(
                source.InstanceId,
                target.InstanceId);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.IncompatibleItems));
            Assert.That(source.Quantity, Is.EqualTo(1));
            Assert.That(target.Quantity, Is.EqualTo(1));
        }

        [Test]
        public void TryRemoveQuantity_RemovesOnlyRequestedAmount()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem(
                "stack",
                quantity: 5,
                maxStack: 5);

            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            InventoryOperationResult result = grid.TryRemoveQuantity(item.InstanceId, 2);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.TransferredQuantity, Is.EqualTo(2));
            Assert.That(result.RemainingQuantity, Is.EqualTo(3));
            Assert.That(item.Quantity, Is.EqualTo(3));
            Assert.That(grid.TryGetPlacement(item.InstanceId, out _), Is.True);
        }

        [Test]
        public void TryFindFirstFit_ScansRowsFromLeftToRight()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel blocker = CreateItem("blocker", width: 2);
            InventoryItemModel item = CreateItem("item");

            grid.TryPlace(blocker, new InventoryGridPosition(0, 0));

            bool found = grid.TryFindFirstFit(item, out InventoryGridPosition origin);

            Assert.That(found, Is.True);
            Assert.That(origin, Is.EqualTo(new InventoryGridPosition(2, 0)));
        }

        private static InventoryItemModel CreateItem(
            string instanceId,
            string definitionId = "definition",
            int width = 1,
            int height = 1,
            int quantity = 1,
            int maxStack = 1,
            bool canRotate = true,
            bool canAssignQuickSlot = true)
        {
            return new InventoryItemModel(
                instanceId,
                definitionId,
                new InventoryGridSize(width, height),
                quantity,
                maxStack,
                canRotate,
                canAssignQuickSlot);
        }
    }
}
