using NUnit.Framework;

namespace Failsafe.Inventory.Core.Tests
{
    [TestFixture]
    public sealed class InventoryQuickSlotsTests
    {
        [Test]
        public void Constructor_CreatesFiveSlotsByDefault()
        {
            InventoryGridModel grid = new InventoryGridModel();

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                Assert.That(quickSlots.SlotCount, Is.EqualTo(5));
            }
        }

        [Test]
        public void Assign_StoresReferenceToPlacedItem()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item");
            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                InventoryOperationResult result = quickSlots.Assign(0, item.InstanceId);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(quickSlots.GetAssignedInstanceId(0), Is.EqualTo(item.InstanceId));
                Assert.That(quickSlots.TryGetAssignedItem(0, out InventoryItemModel assigned), Is.True);
                Assert.That(assigned, Is.SameAs(item));
            }
        }

        [Test]
        public void Assign_MovesSameItemFromPreviousSlot()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item");
            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                quickSlots.Assign(0, item.InstanceId);
                quickSlots.Assign(3, item.InstanceId);

                Assert.That(quickSlots.GetAssignedInstanceId(0), Is.Null);
                Assert.That(quickSlots.GetAssignedInstanceId(3), Is.EqualTo(item.InstanceId));
            }
        }

        [Test]
        public void Assign_RejectsItemThatDoesNotAllowQuickSlots()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem(
                "item",
                canAssignQuickSlot: false);
            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                InventoryOperationResult result = quickSlots.Assign(0, item.InstanceId);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.QuickSlotNotAllowed));
                Assert.That(quickSlots.GetAssignedInstanceId(0), Is.Null);
            }
        }

        [Test]
        public void Assign_RejectsSlotOutsideFiveSlotRange()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item");
            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                InventoryOperationResult result = quickSlots.Assign(5, item.InstanceId);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.FailureReason, Is.EqualTo(InventoryFailureReason.QuickSlotOutOfRange));
            }
        }

        [Test]
        public void RemovingItemFromGrid_ClearsAssignedSlot()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("item");
            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                quickSlots.Assign(2, item.InstanceId);

                grid.TryRemove(item.InstanceId);

                Assert.That(quickSlots.GetAssignedInstanceId(2), Is.Null);
            }
        }

        [Test]
        public void RemovingPartOfStack_KeepsAssignedSlot()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem(
                "stack",
                quantity: 5,
                maxStack: 5);
            grid.TryPlace(item, new InventoryGridPosition(0, 0));

            using (InventoryQuickSlots quickSlots = new InventoryQuickSlots(grid))
            {
                quickSlots.Assign(1, item.InstanceId);

                grid.TryRemoveQuantity(item.InstanceId, 1);

                Assert.That(quickSlots.GetAssignedInstanceId(1), Is.EqualTo(item.InstanceId));
                Assert.That(item.Quantity, Is.EqualTo(4));
            }
        }

        private static InventoryItemModel CreateItem(
            string instanceId,
            int quantity = 1,
            int maxStack = 1,
            bool canAssignQuickSlot = true)
        {
            return new InventoryItemModel(
                instanceId,
                "definition",
                new InventoryGridSize(1, 1),
                quantity,
                maxStack,
                canAssignQuickSlot: canAssignQuickSlot);
        }
    }
}
