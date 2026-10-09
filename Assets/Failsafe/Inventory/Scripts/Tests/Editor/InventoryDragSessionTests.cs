using System;
using NUnit.Framework;

namespace Failsafe.Inventory.Core.Tests
{
    [TestFixture]
    public sealed class InventoryDragSessionTests
    {
        [Test]
        public void Constructor_PreservesGrabbedCellAndOriginalPlacement()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 3, height: 2);
            InventoryGridPosition origin = new InventoryGridPosition(1, 1);
            InventoryGridPosition grabbedCell = new InventoryGridPosition(3, 2);

            grid.TryPlace(item, origin);
            grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement);

            InventoryDragSession session = new InventoryDragSession(
                placement,
                grabbedCell);

            Assert.That(session.InstanceId, Is.EqualTo(item.InstanceId));
            Assert.That(session.TargetOrigin, Is.EqualTo(origin));
            Assert.That(session.TargetRotation, Is.EqualTo(InventoryItemRotation.Default));
            Assert.That(session.GrabOffset, Is.EqualTo(new InventoryGridPosition(2, 1)));
            AssertGrabbedCellStaysUnderPointer(session);
        }

        [Test]
        public void UpdatePointer_KeepsOriginalGrabOffset()
        {
            InventoryDragSession session = CreateSession(
                width: 3,
                height: 1,
                origin: new InventoryGridPosition(1, 1),
                grabbedCell: new InventoryGridPosition(3, 1));

            session.UpdatePointer(new InventoryGridPosition(4, 3));

            Assert.That(
                session.TargetOrigin,
                Is.EqualTo(new InventoryGridPosition(2, 3)));
            Assert.That(session.GrabOffset, Is.EqualTo(new InventoryGridPosition(2, 0)));
            AssertGrabbedCellStaysUnderPointer(session);
        }

        [Test]
        public void TryToggleRotation_RotatesGrabOffsetWithoutPointerJump()
        {
            InventoryDragSession session = CreateSession(
                width: 3,
                height: 2,
                origin: new InventoryGridPosition(1, 1),
                grabbedCell: new InventoryGridPosition(3, 1));

            bool rotated = session.TryToggleRotation();

            Assert.That(rotated, Is.True);
            Assert.That(
                session.TargetRotation,
                Is.EqualTo(InventoryItemRotation.Clockwise90));
            Assert.That(session.TargetFootprint, Is.EqualTo(new InventoryGridSize(2, 3)));
            Assert.That(session.GrabOffset, Is.EqualTo(new InventoryGridPosition(1, 2)));
            Assert.That(
                session.TargetOrigin,
                Is.EqualTo(new InventoryGridPosition(2, -1)));
            AssertGrabbedCellStaysUnderPointer(session);
        }

        [Test]
        public void Constructor_WhenItemStartsRotated_ConvertsGrabOffsetBackToBasePose()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("rotated", width: 3, height: 2);
            InventoryGridPosition origin = new InventoryGridPosition(1, 1);

            grid.TryPlace(item, origin);
            grid.TryRotate(item.InstanceId);
            grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement);

            InventoryDragSession session = new InventoryDragSession(
                placement,
                new InventoryGridPosition(1, 3));

            Assert.That(
                session.TargetRotation,
                Is.EqualTo(InventoryItemRotation.Clockwise90));
            Assert.That(session.GrabOffset, Is.EqualTo(new InventoryGridPosition(0, 2)));
            AssertGrabbedCellStaysUnderPointer(session);

            Assert.That(session.TryToggleRotation(), Is.True);
            Assert.That(session.TargetRotation, Is.EqualTo(InventoryItemRotation.Default));
            Assert.That(session.GrabOffset, Is.EqualTo(new InventoryGridPosition(2, 1)));
            AssertGrabbedCellStaysUnderPointer(session);
        }

        [Test]
        public void TryToggleRotation_WhenRotationIsForbidden_DoesNotChangeSession()
        {
            InventoryDragSession session = CreateSession(
                width: 2,
                height: 1,
                origin: new InventoryGridPosition(1, 1),
                grabbedCell: new InventoryGridPosition(2, 1),
                canRotate: false);

            bool rotated = session.TryToggleRotation();

            Assert.That(rotated, Is.False);
            Assert.That(session.TargetRotation, Is.EqualTo(InventoryItemRotation.Default));
            Assert.That(session.TargetOrigin, Is.EqualTo(new InventoryGridPosition(1, 1)));
            Assert.That(session.GrabOffset, Is.EqualTo(new InventoryGridPosition(1, 0)));
        }

        [Test]
        public void Constructor_WhenGrabbedCellIsOutsideItem_Throws()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item", width: 2, height: 1);

            grid.TryPlace(item, new InventoryGridPosition(1, 1));
            grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new InventoryDragSession(
                    placement,
                    new InventoryGridPosition(3, 1)));
        }

        private static InventoryDragSession CreateSession(
            int width,
            int height,
            InventoryGridPosition origin,
            InventoryGridPosition grabbedCell,
            bool canRotate = true)
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem(
                "item",
                width,
                height,
                canRotate);

            grid.TryPlace(item, origin);
            grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement);
            return new InventoryDragSession(placement, grabbedCell);
        }

        private static InventoryItemModel CreateItem(
            string instanceId,
            int width,
            int height,
            bool canRotate = true)
        {
            return new InventoryItemModel(
                instanceId,
                "definition",
                new InventoryGridSize(width, height),
                canRotate: canRotate);
        }

        private static void AssertGrabbedCellStaysUnderPointer(
            InventoryDragSession session)
        {
            Assert.That(
                session.TargetOrigin.Column + session.GrabOffset.Column,
                Is.EqualTo(session.PointerCell.Column));
            Assert.That(
                session.TargetOrigin.Row + session.GrabOffset.Row,
                Is.EqualTo(session.PointerCell.Row));
        }
    }
}
