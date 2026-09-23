using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Core;
using Failsafe.Scripts.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Failsafe.Chests.Tests
{
    [TestFixture]
    public sealed class ChestInventoryControllerTests
    {
        private readonly List<UnityEngine.Object> _objects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
        }

        [Test]
        public void Generate_CreatesExactWeightAndValidGridPlacements()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: true,
                CreateEntry("small", weight: 1, maxCopies: 4),
                CreateEntry(
                    "wide",
                    weight: 2,
                    maxCopies: 2,
                    width: 2,
                    height: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 4,
                columns: 3,
                rows: 2);

            bool generated = chest.TryEnsureGenerated(
                seed: 17,
                out string error);

            Assert.That(generated, Is.True, error);
            Assert.That(chest.IsGenerated, Is.True);
            Assert.That(chest.GeneratedWeight, Is.EqualTo(4));
            Assert.That(chest.RemainingWeight, Is.EqualTo(4));
            Assert.That(chest.Grid.Placements.Count, Is.EqualTo(chest.ItemCount));

            int storedWeight = chest.StoredItems.Sum(item => item.Weight);
            Assert.That(storedWeight, Is.EqualTo(4));
        }

        [Test]
        public void Generate_UsesReachableWeightInsideConfiguredSpread()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("four", weight: 4));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 5,
                columns: 1,
                rows: 1,
                weightSpread: 1);

            bool generated = chest.TryEnsureGenerated(
                seed: 17,
                out string error);

            Assert.That(generated, Is.True, error);
            Assert.That(chest.TargetWeight, Is.EqualTo(5));
            Assert.That(chest.WeightSpread, Is.EqualTo(1));
            Assert.That(chest.GeneratedWeight, Is.EqualTo(4));
            Assert.That(chest.RemainingWeight, Is.EqualTo(4));
            Assert.That(
                chest.StoredItems.Sum(item => item.Weight),
                Is.EqualTo(4));
        }

        [Test]
        public void PersistentState_RestoresRolledWeightInsideSpread()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("four", weight: 4));
            ChestInventoryController source = CreateChest(
                table,
                exactWeight: 5,
                columns: 1,
                rows: 1,
                weightSpread: 1);

            Assert.That(
                source.TryEnsureGenerated(seed: 19, out string generationError),
                Is.True,
                generationError);

            string serialized = source.CapturePersistentState();
            ChestInventoryController restored = CreateChest(
                table,
                exactWeight: 5,
                columns: 1,
                rows: 1,
                weightSpread: 1);

            restored.RestorePersistentState(serialized, source.StateVersion);

            Assert.That(restored.GeneratedWeight, Is.EqualTo(4));
            Assert.That(restored.RemainingWeight, Is.EqualTo(4));
            Assert.That(restored.StoredItems.Single().Weight, Is.EqualTo(4));
        }

        [Test]
        public void DetachAndRestore_PreservesInstanceAndPlacement()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("only", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 2,
                rows: 1);

            Assert.That(
                chest.TryEnsureGenerated(seed: 7, out string generationError),
                Is.True,
                generationError);

            ChestStoredItem storedItem = chest.StoredItems.Single();
            Assert.That(
                chest.Grid.TryGetPlacement(
                    storedItem.InstanceId,
                    out Failsafe.Inventory.Core.InventoryPlacement originalPlacement),
                Is.True);

            Assert.That(
                chest.TryDetach(
                    storedItem.InstanceId,
                    out ChestDetachedItem detached,
                    out string detachError),
                Is.True,
                detachError);
            Assert.That(chest.ItemCount, Is.Zero);
            Assert.That(chest.RemainingWeight, Is.Zero);

            Assert.That(
                chest.TryRestoreDetached(detached, out string restoreError),
                Is.True,
                restoreError);
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(chest.RemainingWeight, Is.EqualTo(1));
            Assert.That(
                chest.StoredItems.Single().InstanceId,
                Is.EqualTo(storedItem.InstanceId));
            Assert.That(
                chest.Grid.TryGetPlacement(
                    storedItem.InstanceId,
                    out Failsafe.Inventory.Core.InventoryPlacement restoredPlacement),
                Is.True);
            Assert.That(
                restoredPlacement.Origin,
                Is.EqualTo(originalPlacement.Origin));
            Assert.That(
                restoredPlacement.Item.Rotation,
                Is.EqualTo(originalPlacement.Item.Rotation));
        }

        [Test]
        public void PersistentState_RestoresRemainingContentsWithoutReroll()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: true,
                CreateEntry("unit", weight: 1, maxCopies: 3));
            ChestInventoryController source = CreateChest(
                table,
                exactWeight: 3,
                columns: 3,
                rows: 1);

            Assert.That(
                source.TryEnsureGenerated(seed: 99, out string generationError),
                Is.True,
                generationError);

            string removedInstanceId =
                source.StoredItems.First().InstanceId;

            Assert.That(
                source.TryDetach(
                    removedInstanceId,
                    out ChestDetachedItem detachedItem,
                    out string detachError),
                Is.True,
                detachError);
            Assert.That(
                source.TryCommitDetached(
                    detachedItem,
                    out string commitError),
                Is.True,
                commitError);

            HashSet<string> expectedIds = source.StoredItems
                .Select(item => item.InstanceId)
                .ToHashSet();
            string serialized = source.CapturePersistentState();

            ChestInventoryController restored = CreateChest(
                table,
                exactWeight: 3,
                columns: 3,
                rows: 1);

            restored.RestorePersistentState(
                serialized,
                source.StateVersion);

            Assert.That(restored.IsGenerated, Is.True);
            Assert.That(restored.GenerationSeed, Is.EqualTo(99));
            Assert.That(restored.RemainingWeight, Is.EqualTo(2));
            Assert.That(
                restored.StoredItems.Select(item => item.InstanceId),
                Is.EquivalentTo(expectedIds));
            Assert.That(
                restored.StoredItems.Select(item => item.InstanceId),
                Does.Not.Contain(removedInstanceId));
        }

        [Test]
        public void PersistentState_RestoresByDefinitionIdWhenLegacyEntryIdDiffers()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("legacy-save", weight: 1));
            ChestInventoryController source = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                source.TryEnsureGenerated(seed: 101, out string generationError),
                Is.True,
                generationError);

            string serialized = source.CapturePersistentState();
            string legacyState = serialized.Replace(
                "\"lootEntryId\":\"definition-legacy-save\"",
                "\"lootEntryId\":\"old-manual-entry-id\"");

            Assert.That(legacyState, Is.Not.EqualTo(serialized));

            ChestInventoryController restored = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            restored.RestorePersistentState(
                legacyState,
                source.StateVersion);

            Assert.That(restored.IsGenerated, Is.True);
            Assert.That(restored.StoredItems.Single().ItemData,
                Is.SameAs(table.Entries[0].ItemData));
        }

        [Test]
        public void Controller_RequiresRunPersistentObjectOnSameGameObject()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("persistent", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                chest.GetComponent<RunPersistentObject>(),
                Is.Not.Null);
        }

        [Test]
        public void CaptureBeforeGeneration_GeneratesValidContents()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("capture", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            string serialized = chest.CapturePersistentState();

            Assert.That(serialized, Is.Not.Empty);
            Assert.That(chest.IsGenerated, Is.True);
            Assert.That(chest.RemainingWeight, Is.EqualTo(1));
        }

        [Test]
        public void RestoreMalformedPayload_DoesNotClearCurrentContents()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("malformed", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                chest.TryEnsureGenerated(seed: 31, out string generationError),
                Is.True,
                generationError);

            string instanceId = chest.StoredItems.Single().InstanceId;

            Assert.Throws<InvalidOperationException>(() =>
                chest.RestorePersistentState("{}", chest.StateVersion));
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(
                chest.StoredItems.Single().InstanceId,
                Is.EqualTo(instanceId));
        }

        [Test]
        public void RestoreInvalidSavedItem_DoesNotClearCurrentContents()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("invalid-saved-item", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                chest.TryEnsureGenerated(seed: 43, out string generationError),
                Is.True,
                generationError);

            string instanceId = chest.StoredItems.Single().InstanceId;
            string validState = chest.CapturePersistentState();
            string invalidState = validState.Replace(
                "\"weight\":1",
                "\"weight\":2");

            Assert.That(invalidState, Is.Not.EqualTo(validState));
            Assert.Throws<InvalidOperationException>(() =>
                chest.RestorePersistentState(
                    invalidState,
                    chest.StateVersion));
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(chest.RemainingWeight, Is.EqualTo(1));
            Assert.That(
                chest.StoredItems.Single().InstanceId,
                Is.EqualTo(instanceId));
        }

        [Test]
        public void RestoreWhileTransferIsPending_PreservesPendingTransaction()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("pending-restore", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                chest.TryEnsureGenerated(seed: 47, out string generationError),
                Is.True,
                generationError);

            string savedState = chest.CapturePersistentState();
            string instanceId = chest.StoredItems.Single().InstanceId;

            Assert.That(
                chest.TryDetach(
                    instanceId,
                    out ChestDetachedItem detachedItem,
                    out string detachError),
                Is.True,
                detachError);

            Assert.Throws<InvalidOperationException>(() =>
                chest.RestorePersistentState(
                    savedState,
                    chest.StateVersion));
            Assert.That(chest.ItemCount, Is.Zero);
            Assert.That(chest.RemainingWeight, Is.Zero);
            Assert.That(
                chest.TryRestoreDetached(
                    detachedItem,
                    out string restoreError),
                Is.True,
                restoreError);
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(chest.RemainingWeight, Is.EqualTo(1));
            Assert.That(
                chest.StoredItems.Single().InstanceId,
                Is.EqualTo(instanceId));
        }

        [Test]
        public void CommittedDetachedItem_CannotBeRestoredAgain()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("committed", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                chest.TryEnsureGenerated(seed: 13, out string generationError),
                Is.True,
                generationError);

            string instanceId = chest.StoredItems.Single().InstanceId;
            Assert.That(
                chest.TryDetach(
                    instanceId,
                    out ChestDetachedItem detachedItem,
                    out string detachError),
                Is.True,
                detachError);
            Assert.That(
                chest.TryCommitDetached(
                    detachedItem,
                    out string commitError),
                Is.True,
                commitError);

            bool restored = chest.TryRestoreDetached(
                detachedItem,
                out string restoreError);

            Assert.That(restored, Is.False);
            Assert.That(restoreError, Does.Contain("not a pending transfer"));
            Assert.That(chest.ItemCount, Is.Zero);
        }

        [Test]
        public void Detach_WhenContentsSubscriberThrows_RemainsConsistent()
        {
            ChestLootTable table = CreateTable(
                allowDuplicates: false,
                CreateEntry("subscriber", weight: 1));
            ChestInventoryController chest = CreateChest(
                table,
                exactWeight: 1,
                columns: 1,
                rows: 1);

            Assert.That(
                chest.TryEnsureGenerated(seed: 21, out string generationError),
                Is.True,
                generationError);

            string instanceId = chest.StoredItems.Single().InstanceId;
            chest.ContentsChanged += () =>
                throw new InvalidOperationException("Subscriber failure.");
            LogAssert.Expect(
                LogType.Exception,
                new Regex("InvalidOperationException: Subscriber failure\\."));

            bool detached = chest.TryDetach(
                instanceId,
                out ChestDetachedItem detachedItem,
                out string detachError);

            Assert.That(detached, Is.True, detachError);
            Assert.That(chest.ItemCount, Is.Zero);
            Assert.That(chest.RemainingWeight, Is.Zero);
            Assert.That(
                chest.TryCommitDetached(detachedItem, out string commitError),
                Is.True,
                commitError);
        }

        [Test]
        public void Relocate_RotatesAndPersistsWithoutChangingLootWeight()
        {
            ChestLootTable table = CreateTable(false,
                CreateEntry("movable", weight: 1, width: 2, height: 1));
            ChestInventoryController chest = CreateChest(table, 1, 3, 3);
            Assert.That(chest.TryEnsureGenerated(71, out string error), Is.True, error);
            string id = chest.StoredItems.Single().InstanceId;
            int notifications = 0;
            chest.ContentsChanged += () => notifications++;

            Assert.That(chest.Relocate(id, new InventoryGridPosition(2, 1),
                InventoryItemRotation.Clockwise90).IsSuccess, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(chest.RemainingWeight, Is.EqualTo(1));
            Assert.That(chest.ItemCount, Is.EqualTo(1));

            string saved = chest.CapturePersistentState();
            ChestInventoryController restored = CreateChest(table, 1, 3, 3);
            restored.RestorePersistentState(saved, chest.StateVersion);
            Assert.That(restored.Grid.TryGetPlacement(id, out InventoryPlacement placement), Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(2, 1)));
            Assert.That(placement.Item.Rotation, Is.EqualTo(InventoryItemRotation.Clockwise90));

            Assert.That(chest.Relocate(id, new InventoryGridPosition(3, 2),
                InventoryItemRotation.Default).IsSuccess, Is.False);
            Assert.That(chest.CapturePersistentState(), Is.EqualTo(saved));
            Assert.That(notifications, Is.EqualTo(1));
        }

        private ChestInventoryController CreateChest(
            ChestLootTable table,
            int exactWeight,
            int columns,
            int rows,
            int weightSpread = 0)
        {
            GameObject root = Track(new GameObject("Chest Test"));
            root.SetActive(false);
            ChestInventoryController chest =
                root.AddComponent<ChestInventoryController>();

            SetPrivateField(chest, "_lootTable", table);
            SetPrivateField(chest, "_exactWeightBudget", exactWeight);
            SetPrivateField(chest, "_weightSpread", weightSpread);
            SetPrivateField(chest, "_columns", columns);
            SetPrivateField(chest, "_rows", rows);
            root.SetActive(true);
            return chest;
        }

        private ChestLootEntry CreateEntry(
            string id,
            int weight,
            int maxCopies = 1,
            int width = 1,
            int height = 1)
        {
            ItemData itemData = Track(
                ScriptableObject.CreateInstance<ItemData>());

            itemData.name = $"Item {id}";
            itemData.InventoryDefinitionId = $"definition-{id}";
            itemData.InventoryWidth = width;
            itemData.InventoryHeight = height;
            itemData.InventoryMaxStack = 1;
            itemData.CanRotateInInventory = true;
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
                maxCopies);
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
            SetPrivateField(table, "_allowDuplicateEntries", allowDuplicates);
            SetPrivateField(
                table,
                "_entries",
                new List<ChestLootEntry>(entries));
            return table;
        }

        private static void SetPrivateField<TTarget, TValue>(
            TTarget target,
            string fieldName,
            TValue value)
        {
            FieldInfo field = typeof(TTarget).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private T Track<T>(T target) where T : UnityEngine.Object
        {
            _objects.Add(target);
            return target;
        }
    }
}
