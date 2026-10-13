using System;
using System.Collections.Generic;
using System.Reflection;
using Failsafe.Inventory.Core;
using Failsafe.Items;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using Failsafe.Scripts.SaveSystem;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Inventory.Integration.Tests
{
    [TestFixture]
    public sealed class InventoryRunSaveParticipantTests
    {
        private readonly List<UnityEngine.Object> _objects =
            new List<UnityEngine.Object>();

        private InventoryQuickSlotEquipService _equipService;
        private InventoryRunSaveParticipant _participant;
        private RunPersistentObjectRegistry _persistentObjectRegistry;

        [TearDown]
        public void TearDown()
        {
            _participant?.Dispose();
            _participant = null;
            _equipService?.Dispose();
            _equipService = null;
            _persistentObjectRegistry?.Dispose();
            _persistentObjectRegistry = null;

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object target = _objects[index];

                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }

            _objects.Clear();
        }

        [Test]
        public void Capture_EmptyInventoryWritesCompleteState()
        {
            InventoryRuntimeController inventory =
                CreateInitializedController();
            InventoryRunSaveParticipant participant =
                CreateParticipant(inventory);
            var checkpoint = new RunCheckpointData();

            participant.Capture(checkpoint);

            Assert.That(checkpoint.inventory, Is.Not.Null);
            Assert.That(checkpoint.inventory.hasState, Is.True);
            Assert.That(checkpoint.inventory.items, Is.Empty);
            Assert.That(
                checkpoint.inventory.quickSlotInstanceIds.Count,
                Is.EqualTo(inventory.QuickSlots.SlotCount));
            Assert.That(
                checkpoint.inventory.activeQuickSlotIndex,
                Is.EqualTo(InventoryQuickSlotEquipService.NoActiveSlot));
        }

        [Test]
        public void Restore_DuplicateInstanceIdLeavesCurrentInventoryUntouched()
        {
            ItemData itemData = CreateItemData("duplicate-test");
            InventoryRuntimeController inventory =
                CreateInitializedController();
            string existingInstanceId = AddItem(inventory, itemData);
            InventoryRunSaveParticipant participant =
                CreateParticipant(inventory);
            InventoryStateData savedState = CreateEmptySavedState(inventory);
            savedState.items.Add(CreateSavedItem(
                itemData,
                "duplicate-instance",
                column: 0));
            savedState.items.Add(CreateSavedItem(
                itemData,
                "duplicate-instance",
                column: 1));
            RunCheckpointData checkpoint = CreateCheckpoint(savedState);

            InvalidOperationException exception = Assert.Throws<
                InvalidOperationException>(
                () => participant.RestoreAsync(
                        checkpoint,
                        CreateLoadContext(checkpoint))
                    .GetAwaiter()
                    .GetResult());

            Assert.That(exception.Message, Does.Contain("occurs more than once"));
            Assert.That(
                inventory.Grid.TryGetItem(existingInstanceId, out _),
                Is.True);
            Assert.That(inventory.Grid.Placements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Restore_InvalidWorldOriginLeavesCurrentInventoryUntouched()
        {
            ItemData itemData = CreateItemData("origin-test");
            InventoryRuntimeController inventory =
                CreateInitializedController();
            string existingInstanceId = AddItem(inventory, itemData);
            InventoryRunSaveParticipant participant =
                CreateParticipant(inventory);
            InventoryStateData savedState = CreateEmptySavedState(inventory);
            InventoryItemStateData invalidItem = CreateSavedItem(
                itemData,
                "invalid-origin",
                column: 0);
            invalidItem.runtimeGeneratedWorldItem = true;
            invalidItem.hasWorldItem = false;
            savedState.items.Add(invalidItem);
            RunCheckpointData checkpoint = CreateCheckpoint(savedState);

            InvalidOperationException exception = Assert.Throws<
                InvalidOperationException>(
                () => participant.RestoreAsync(
                        checkpoint,
                        CreateLoadContext(checkpoint))
                    .GetAwaiter()
                    .GetResult());

            Assert.That(exception.Message, Does.Contain("world origin metadata"));
            Assert.That(
                inventory.Grid.TryGetItem(existingInstanceId, out _),
                Is.True);
            Assert.That(inventory.Grid.Placements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Restore_ValidEmptyStateClearsCurrentInventory()
        {
            ItemData itemData = CreateItemData("clear-test");
            InventoryRuntimeController inventory =
                CreateInitializedController();
            AddItem(inventory, itemData);
            InventoryRunSaveParticipant participant =
                CreateParticipant(inventory);
            InventoryStateData savedState = CreateEmptySavedState(inventory);
            RunCheckpointData checkpoint = CreateCheckpoint(savedState);

            participant.RestoreAsync(
                    checkpoint,
                    CreateLoadContext(checkpoint))
                .GetAwaiter()
                .GetResult();

            Assert.That(inventory.Grid.Placements, Is.Empty);
            Assert.That(inventory.RegisteredWorldItemCount, Is.Zero);
            Assert.That(
                _equipService.ActiveSlotIndex,
                Is.EqualTo(InventoryQuickSlotEquipService.NoActiveSlot));
        }

        private InventoryRunSaveParticipant CreateParticipant(
            InventoryRuntimeController inventory)
        {
            GameObject playerRoot = Track(
                new GameObject("Inventory Save Participant Test Player"));
            playerRoot.SetActive(false);

            PlayerView playerView = playerRoot.AddComponent<PlayerView>();
            PlayerControlBlocker controlBlocker =
                playerRoot.AddComponent<PlayerControlBlocker>();
            var handRoot = new GameObject("Right Hand Item Place");
            handRoot.transform.SetParent(playerRoot.transform, false);
            playerView.RightHandItemPlace = handRoot.transform;
            playerRoot.SetActive(true);

            var hands = new PlayerHandsContainer(
                Array.Empty<IUsable>(),
                playerView);

            _equipService = new InventoryQuickSlotEquipService(
                inventory,
                hands,
                controlBlocker);
            _persistentObjectRegistry =
                new RunPersistentObjectRegistry();
            _participant = new InventoryRunSaveParticipant(
                inventory,
                _equipService,
                new RunSaveParticipantRegistry(),
                _persistentObjectRegistry);

            return _participant;
        }

        private InventoryRuntimeController CreateInitializedController()
        {
            GameObject root = Track(
                new GameObject("Inventory Save Participant Test Runtime"));
            root.SetActive(false);

            InventoryRuntimeController controller =
                root.AddComponent<InventoryRuntimeController>();

            SetPrivateField(controller, "_initializeOnAwake", false);
            root.SetActive(true);

            Assert.That(
                controller.TryInitialize(out string error),
                Is.True,
                error);

            return controller;
        }

        private string AddItem(
            InventoryRuntimeController inventory,
            ItemData itemData)
        {
            InventoryOperationResult result = inventory.AddFirstAvailable(
                itemData,
                quantity: 1,
                out string instanceId,
                out string error);

            Assert.That(result.IsSuccess, Is.True, error);
            return instanceId;
        }

        private ItemData CreateItemData(string definitionId)
        {
            ItemData itemData = Track(
                ScriptableObject.CreateInstance<ItemData>());

            itemData.name = $"Item Data {definitionId}";
            itemData.InventoryDefinitionId = definitionId;
            itemData.InventoryWidth = 1;
            itemData.InventoryHeight = 1;
            itemData.InventoryMaxStack = 1;
            itemData.InventoryModelScaleMultiplier = 1f;
            itemData.InventoryModelFitPadding = 0.08f;
            itemData.InventoryModelMaxDepthInCells = 0.75f;
            itemData.InventoryModelPrefab = Track(
                GameObject.CreatePrimitive(PrimitiveType.Cube));
            itemData.InventoryModelPrefab.name =
                $"{definitionId} Inventory Model";

            return itemData;
        }

        private static InventoryStateData CreateEmptySavedState(
            InventoryRuntimeController inventory)
        {
            var state = new InventoryStateData
            {
                hasState = true,
                activeQuickSlotIndex =
                    InventoryQuickSlotEquipService.NoActiveSlot
            };

            for (int index = 0;
                 index < inventory.QuickSlots.SlotCount;
                 index++)
            {
                state.quickSlotInstanceIds.Add(null);
            }

            return state;
        }

        private static InventoryItemStateData CreateSavedItem(
            ItemData itemData,
            string instanceId,
            int column)
        {
            return new InventoryItemStateData
            {
                itemId = itemData.InventoryDefinitionId,
                instanceId = instanceId,
                quantity = 1,
                row = 0,
                column = column,
                rotation = (int)InventoryItemRotation.Default,
                energy = 0f
            };
        }

        private static RunCheckpointData CreateCheckpoint(
            InventoryStateData inventoryState)
        {
            return new RunCheckpointData
            {
                hasCheckpoint = true,
                inventory = inventoryState
            };
        }

        private static RunLoadContext CreateLoadContext(
            RunCheckpointData checkpoint)
        {
            return new RunLoadContext(
                new RunSaveFile
                {
                    runId = "inventory-test-run",
                    checkpoint = checkpoint
                });
        }

        private static void SetPrivateField<T>(
            InventoryRuntimeController controller,
            string fieldName,
            T value)
        {
            FieldInfo field = typeof(InventoryRuntimeController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(controller, value);
        }

        private T Track<T>(T target) where T : UnityEngine.Object
        {
            _objects.Add(target);
            return target;
        }
    }
}
