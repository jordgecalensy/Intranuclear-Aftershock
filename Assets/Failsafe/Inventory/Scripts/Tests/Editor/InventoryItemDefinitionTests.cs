using System;
using NUnit.Framework;

namespace Failsafe.Inventory.Core.Tests
{
    [TestFixture]
    public sealed class InventoryItemDefinitionTests
    {
        [Test]
        public void Constructor_StoresInventoryRules()
        {
            InventoryItemDefinition definition = new InventoryItemDefinition(
                "stasis-gun",
                new InventoryGridSize(2, 3),
                maxStack: 4,
                canRotate: false,
                canAssignQuickSlot: true);

            Assert.That(definition.DefinitionId, Is.EqualTo("stasis-gun"));
            Assert.That(definition.Footprint, Is.EqualTo(new InventoryGridSize(2, 3)));
            Assert.That(definition.MaxStack, Is.EqualTo(4));
            Assert.That(definition.CanRotate, Is.False);
            Assert.That(definition.CanAssignQuickSlot, Is.True);
        }

        [Test]
        public void Constructor_RejectsEmptyDefinitionId()
        {
            Assert.Throws<ArgumentException>(() =>
                new InventoryItemDefinition(
                    " ",
                    new InventoryGridSize(1, 1)));
        }

        [Test]
        public void Constructor_RejectsInvalidMaxStack()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new InventoryItemDefinition(
                    "item",
                    new InventoryGridSize(1, 1),
                    maxStack: 0));
        }

        [Test]
        public void ItemModel_FromDefinition_CopiesDefinitionRules()
        {
            InventoryItemDefinition definition = new InventoryItemDefinition(
                "medkit",
                new InventoryGridSize(2, 1),
                maxStack: 5,
                canRotate: true,
                canAssignQuickSlot: false);

            InventoryItemModel item = new InventoryItemModel(
                "instance",
                definition,
                quantity: 3);

            Assert.That(item.Definition, Is.SameAs(definition));
            Assert.That(item.DefinitionId, Is.EqualTo("medkit"));
            Assert.That(item.BaseFootprint, Is.EqualTo(new InventoryGridSize(2, 1)));
            Assert.That(item.MaxStack, Is.EqualTo(5));
            Assert.That(item.CanRotate, Is.True);
            Assert.That(item.CanAssignQuickSlot, Is.False);
            Assert.That(item.Quantity, Is.EqualTo(3));
        }

        [Test]
        public void ItemModel_FromDefinition_RejectsQuantityAboveStackLimit()
        {
            InventoryItemDefinition definition = new InventoryItemDefinition(
                "item",
                new InventoryGridSize(1, 1),
                maxStack: 2);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new InventoryItemModel("instance", definition, quantity: 3));
        }
    }
}
