using System.Collections.Generic;
using System.Reflection;
using Assets.Failsafe.Scripts.RandomGeneration;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Chests.Tests
{
    [TestFixture]
    public sealed class ChestExactWeightGeneratorTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                    Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
        }

        [Test]
        public void Generate_ReturnsExactlyRequestedWeight()
        {
            ChestLootEntry two = CreateEntry("two", weight: 2, maxCopies: 2);
            ChestLootEntry three = CreateEntry("three", weight: 3, maxCopies: 2);
            ChestLootTable table = CreateTable(
                allowDuplicates: true,
                two,
                three);
            var generator = new ChestExactWeightGenerator(seed: 42);

            bool generated = generator.TryGenerate(
                table,
                exactWeight: 7,
                maximumItems: 4,
                finalSelectionValidator: null,
                out List<ChestLootEntry> selection,
                out string error);

            Assert.That(generated, Is.True, error);
            Assert.That(selection, Is.Not.Null);

            int totalWeight = 0;

            for (int index = 0; index < selection.Count; index++)
                totalWeight += selection[index].Weight;

            Assert.That(totalWeight, Is.EqualTo(7));
        }

        [Test]
        public void Generate_RejectsImpossibleExactWeight()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: true,
                CreateEntry("two", weight: 2, maxCopies: 4),
                CreateEntry("four", weight: 4, maxCopies: 4));
            var generator = new ChestExactWeightGenerator(seed: 1);

            bool generated = generator.TryGenerate(
                table,
                exactWeight: 3,
                maximumItems: 4,
                finalSelectionValidator: null,
                out _,
                out string error);

            Assert.That(generated, Is.False);
            Assert.That(error, Does.Contain("exactly 3"));
        }

        [Test]
        public void Generate_WithSpread_UsesReachableWeightInsideRange()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("four", weight: 4));
            var generator = new ChestExactWeightGenerator(seed: 11);

            bool generated = generator.TryGenerate(
                table,
                targetWeight: 5,
                weightSpread: 1,
                maximumItems: 1,
                finalSelectionValidator: null,
                out List<ChestLootEntry> selection,
                out int generatedWeight,
                out string error);

            Assert.That(generated, Is.True, error);
            Assert.That(generatedWeight, Is.EqualTo(4));
            Assert.That(selection, Has.Count.EqualTo(1));
            Assert.That(selection[0].Weight, Is.EqualTo(4));
        }

        [Test]
        public void Generate_WithZeroSpread_StillRequiresExactTarget()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("four", weight: 4));
            var generator = new ChestExactWeightGenerator(seed: 12);

            bool generated = generator.TryGenerate(
                table,
                targetWeight: 5,
                weightSpread: 0,
                maximumItems: 1,
                finalSelectionValidator: null,
                out _,
                out _,
                out string error);

            Assert.That(generated, Is.False);
            Assert.That(error, Does.Contain("exactly 5"));
        }

        [Test]
        public void Generate_WithSpread_RejectsWhenRangeIsUnreachable()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("two", weight: 2));
            var generator = new ChestExactWeightGenerator(seed: 13);

            bool generated = generator.TryGenerate(
                table,
                targetWeight: 5,
                weightSpread: 1,
                maximumItems: 1,
                finalSelectionValidator: null,
                out _,
                out _,
                out string error);

            Assert.That(generated, Is.False);
            Assert.That(error, Does.Contain("from 4 to 6"));
        }

        [Test]
        public void Generate_RespectsMutualExclusionFromEitherEntry()
        {
            ChestLootEntry second = CreateEntry("second", weight: 3);
            ChestLootEntry first = CreateEntry(
                "first",
                weight: 2,
                excludedItems: new[] { second.ItemData });
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                first,
                second);
            var generator = new ChestExactWeightGenerator(seed: 5);

            bool generated = generator.TryGenerate(
                table,
                exactWeight: 5,
                maximumItems: 2,
                finalSelectionValidator: null,
                out _,
                out string error);

            Assert.That(generated, Is.False);
            Assert.That(error, Does.Contain("cannot produce exactly 5"));
        }

        [Test]
        public void Packing_RotatesItemWhenDefaultOrientationDoesNotFit()
        {
            ChestLootEntry entry = CreateEntry(
                "rotated",
                weight: 1,
                width: 2,
                height: 3,
                canRotate: true);

            bool packed = ChestGridPackingSolver.TryPack(
                new[] { entry },
                columns: 3,
                rows: 2,
                out List<ChestGridPlacement> placements,
                out string error);

            Assert.That(packed, Is.True, error);
            Assert.That(placements, Has.Count.EqualTo(1));
            Assert.That(
                placements[0].Rotation,
                Is.EqualTo(
                    Failsafe.Inventory.Core.InventoryItemRotation.Clockwise90));
        }

        [Test]
        public void LootTableValidation_RejectsMissingInventoryModel()
        {
            ChestLootEntry entry = CreateEntry("missing-model", weight: 1);
            entry.ItemData.InventoryModelPrefab = null;
            ChestLootTable table = CreateTable(false, entry);

            bool valid = table.TryValidate(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("3D inventory model prefab"));
        }

        [Test]
        public void LootTableValidation_RejectsMissingWorldPrefab()
        {
            ChestLootEntry entry = CreateEntry("missing-world", weight: 1);
            entry.ItemData.WorldItemPrefab = null;
            ChestLootTable table = CreateTable(false, entry);

            bool valid = table.TryValidate(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("World Item Prefab"));
        }

        [Test]
        public void LootTableValidation_RejectsMismatchedWorldPrefab()
        {
            ChestLootEntry entry = CreateEntry("mismatch", weight: 1);
            ChestLootEntry foreign = CreateEntry("foreign", weight: 1);
            entry.ItemData.WorldItemPrefab.ItemData = foreign.ItemData;
            ChestLootTable table = CreateTable(false, entry);

            bool valid = table.TryValidate(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("same ItemData"));
        }

        [Test]
        public void LootTableValidation_RejectsDefinitionIdCollision()
        {
            ChestLootEntry first = CreateEntry("first-id", weight: 1);
            ChestLootEntry second = CreateEntry("second-id", weight: 1);
            second.ItemData.InventoryDefinitionId =
                first.ItemData.InventoryDefinitionId;
            ChestLootTable table = CreateTable(false, first, second);

            bool valid = table.TryValidate(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("more than one entry"));
        }

        [Test]
        public void LootEntry_IdComesFromItemDataDefinitionId()
        {
            ChestLootEntry entry = CreateEntry("automatic-id", weight: 1);

            Assert.That(entry.Id, Is.EqualTo("definition-automatic-id"));
        }

        [Test]
        public void LootTableValidation_RejectsDuplicateItemDataEntry()
        {
            ChestLootEntry first = CreateEntry("shared", weight: 1);
            var second = new ChestLootEntry(
                first.ItemData,
                ItemRarity.Rare,
                weight: 2);
            ChestLootTable table = CreateTable(false, first, second);

            bool valid = table.TryValidate(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("Use Max Copies instead"));
        }

        private ChestLootEntry CreateEntry(
            string id,
            int weight,
            int maxCopies = 1,
            int width = 1,
            int height = 1,
            bool canRotate = true,
            ItemData[] excludedItems = null)
        {
            ItemData itemData = Track(
                ScriptableObject.CreateInstance<ItemData>());

            itemData.name = $"Item {id}";
            itemData.InventoryDefinitionId = $"definition-{id}";
            itemData.InventoryWidth = width;
            itemData.InventoryHeight = height;
            itemData.InventoryMaxStack = 1;
            itemData.CanRotateInInventory = canRotate;
            itemData.InventoryModelScaleMultiplier = 1f;
            itemData.InventoryModelFitPadding = 0.08f;
            itemData.InventoryModelMaxDepthInCells = 0.75f;
            itemData.InventoryModelPrefab = Track(
                GameObject.CreatePrimitive(PrimitiveType.Cube));
            itemData.WorldItemPrefab = CreateWorldItem(itemData, id);

            return new ChestLootEntry(
                itemData,
                ItemRarity.Common,
                weight,
                maxCopies,
                excludedItems);
        }

        private Item CreateWorldItem(ItemData itemData, string id)
        {
            GameObject root = Track(new GameObject($"World Item {id}"));
            root.SetActive(false);
            root.AddComponent<BoxCollider>();
            Item item = root.AddComponent<Item>();
            item.ItemData = itemData;
            return item;
        }

        private ChestLootTable CreateTable(
            bool allowDuplicates,
            params ChestLootEntry[] entries)
        {
            ChestLootTable table = Track(
                ScriptableObject.CreateInstance<ChestLootTable>());

            table.name = "Test Chest Loot Table";
            SetPrivateField(
                table,
                "_allowDuplicateEntries",
                allowDuplicates);
            SetPrivateField(
                table,
                "_entries",
                new List<ChestLootEntry>(entries));
            return table;
        }

        private static void SetPrivateField<T>(
            ChestLootTable table,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(ChestLootTable).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(table, value);
        }

        private T Track<T>(T target) where T : Object
        {
            _objects.Add(target);
            return target;
        }
    }
}
