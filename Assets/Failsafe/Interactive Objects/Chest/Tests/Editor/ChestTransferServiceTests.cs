using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Integration;
using Failsafe.Items;
using Failsafe.Player.View;
using Failsafe.PlayerMovements;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Chests.Tests
{
    [TestFixture]
    public sealed class ChestTransferServiceTests
    {
        private readonly List<UnityEngine.Object> _objects =
            new List<UnityEngine.Object>();
        private InventoryQuickSlotEquipService _equipService;
        private PlayerHandsContainer _hands;

        [TearDown]
        public void TearDown()
        {
            _equipService?.Dispose();
            _equipService = null;
            _hands = null;

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
        }

        [Test]
        public void Take_MovesItemFromChestIntoPlayerInventory()
        {
            ItemData itemData = CreateItemData("take-item");
            ChestInventoryController chest = CreateGeneratedChest(itemData);
            InventoryRuntimeController playerInventory =
                CreatePlayerInventory();
            ChestTransferService transfer = CreateTransferService(
                playerInventory);
            string chestInstanceId = GetOnlyChestInstanceId(chest);

            bool taken = transfer.TryTake(
                chest,
                chestInstanceId,
                out string playerInstanceId,
                out string error);

            Assert.That(taken, Is.True, error);
            Assert.That(chest.ItemCount, Is.Zero);
            Assert.That(chest.RemainingWeight, Is.Zero);
            Assert.That(
                playerInventory.Grid.TryGetItem(playerInstanceId, out _),
                Is.True);
            Assert.That(playerInventory.RegisteredWorldItemCount, Is.EqualTo(1));
        }

        [Test]
        public void Equip_UsesLowestFreeQuickSlotAndPlacesItemInHands()
        {
            ItemData itemData = CreateItemData("equip-item");
            ChestInventoryController chest = CreateGeneratedChest(itemData);
            InventoryRuntimeController playerInventory =
                CreatePlayerInventory();
            FillQuickSlot(playerInventory, slotIndex: 0);
            FillQuickSlot(playerInventory, slotIndex: 1);
            ChestTransferService transfer = CreateTransferService(
                playerInventory);
            string chestInstanceId = GetOnlyChestInstanceId(chest);

            bool equipped = transfer.TryEquip(
                chest,
                chestInstanceId,
                out string playerInstanceId,
                out int quickSlotIndex,
                out string error);

            Assert.That(equipped, Is.True, error);
            Assert.That(quickSlotIndex, Is.EqualTo(2));
            Assert.That(
                playerInventory.QuickSlots.GetAssignedInstanceId(2),
                Is.EqualTo(playerInstanceId));
            Assert.That(_equipService.ActiveSlotIndex, Is.EqualTo(2));
            Assert.That(_hands.State, Is.EqualTo(
                PlayerHandsContainer.HandState.ItemInHand));
            Assert.That(
                _hands.ItemInHand.ItemObject.ItemData,
                Is.SameAs(itemData));
            Assert.That(chest.ItemCount, Is.Zero);
        }

        [Test]
        public void Equip_WhenAddedUnitMergesIntoActiveStack_KeepsStackEquipped()
        {
            ItemData itemData = CreateItemData(
                "equipped-stack",
                maxStack: 2);
            ChestInventoryController chest = CreateGeneratedChest(itemData);
            InventoryRuntimeController playerInventory =
                CreatePlayerInventory();
            ChestTransferService transfer = CreateTransferService(
                playerInventory);

            InventoryOperationResult initialAdd =
                playerInventory.CreateAndStoreRuntimeItem(
                    itemData,
                    out string existingInstanceId,
                    out string initialAddError);

            Assert.That(initialAdd.IsSuccess, Is.True, initialAddError);
            Assert.That(
                _equipService.TryEquipItem(
                    existingInstanceId,
                    out string initialEquipError),
                Is.True,
                initialEquipError);

            bool equipped = transfer.TryEquip(
                chest,
                GetOnlyChestInstanceId(chest),
                out string playerInstanceId,
                out int quickSlotIndex,
                out string error);

            Assert.That(equipped, Is.True, error);
            Assert.That(playerInstanceId, Is.EqualTo(existingInstanceId));
            Assert.That(quickSlotIndex, Is.EqualTo(0));
            Assert.That(_equipService.ActiveSlotIndex, Is.EqualTo(0));
            Assert.That(
                _hands.State,
                Is.EqualTo(PlayerHandsContainer.HandState.ItemInHand));
            Assert.That(
                playerInventory.Grid.TryGetItem(
                    existingInstanceId,
                    out InventoryItemModel stackedItem),
                Is.True);
            Assert.That(stackedItem.Quantity, Is.EqualTo(2));
            Assert.That(chest.ItemCount, Is.Zero);
        }

        [Test]
        public void Equip_WhenHeldStackHasNoFreeQuickSlot_RestoresOnlyChestUnit()
        {
            ItemData itemData = CreateItemData(
                "held-unassigned-stack",
                maxStack: 2);
            ChestInventoryController chest = CreateGeneratedChest(itemData);
            InventoryRuntimeController playerInventory =
                CreatePlayerInventory();
            ChestTransferService transfer = CreateTransferService(
                playerInventory);

            InventoryOperationResult initialAdd =
                playerInventory.CreateAndStoreRuntimeItem(
                    itemData,
                    out string heldInstanceId,
                    out string initialAddError);

            Assert.That(initialAdd.IsSuccess, Is.True, initialAddError);
            Assert.That(
                playerInventory.TryGetWorldItem(
                    heldInstanceId,
                    out Item heldItem),
                Is.True);
            Assert.That(_hands.TryTakeItemInHand(heldItem), Is.True);

            for (int slotIndex = 0;
                 slotIndex < playerInventory.QuickSlots.SlotCount;
                 slotIndex++)
            {
                FillQuickSlot(playerInventory, slotIndex);
            }

            string chestInstanceId = GetOnlyChestInstanceId(chest);

            bool equipped = transfer.TryEquip(
                chest,
                chestInstanceId,
                out string playerInstanceId,
                out int quickSlotIndex,
                out string error);

            Assert.That(equipped, Is.False);
            Assert.That(playerInstanceId, Is.Null);
            Assert.That(
                quickSlotIndex,
                Is.EqualTo(InventoryQuickSlotEquipService.NoActiveSlot));
            Assert.That(error, Does.Contain("quick slots are occupied"));
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(GetOnlyChestInstanceId(chest), Is.EqualTo(chestInstanceId));
            Assert.That(
                playerInventory.Grid.TryGetItem(
                    heldInstanceId,
                    out InventoryItemModel heldStack),
                Is.True);
            Assert.That(heldStack.Quantity, Is.EqualTo(1));
            Assert.That(
                _hands.State,
                Is.EqualTo(PlayerHandsContainer.HandState.ItemInHand));
            Assert.That(_hands.ItemInHand.ItemObject, Is.SameAs(heldItem));
            Assert.That(
                _equipService.ActiveSlotIndex,
                Is.EqualTo(InventoryQuickSlotEquipService.NoActiveSlot));
        }

        [Test]
        public void Take_WhenInventoryIsFull_RestoresChestItem()
        {
            ItemData itemData = CreateItemData("rejected-item");
            ChestInventoryController chest = CreateGeneratedChest(itemData);
            InventoryRuntimeController playerInventory =
                CreatePlayerInventory();
            ItemData blocker = CreateItemData(
                "full-grid-blocker",
                width: InventoryGridModel.DefaultColumns,
                height: InventoryGridModel.DefaultRows,
                createWorldPrefab: false);

            InventoryOperationResult fillResult =
                playerInventory.AddFirstAvailable(
                    blocker,
                    quantity: 1,
                    out _,
                    out string fillError);

            Assert.That(fillResult.IsSuccess, Is.True, fillError);

            ChestTransferService transfer = CreateTransferService(
                playerInventory);
            string chestInstanceId = GetOnlyChestInstanceId(chest);

            bool taken = transfer.TryTake(
                chest,
                chestInstanceId,
                out string playerInstanceId,
                out string error);

            Assert.That(taken, Is.False);
            Assert.That(playerInstanceId, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(chest.RemainingWeight, Is.EqualTo(1));
            Assert.That(GetOnlyChestInstanceId(chest), Is.EqualTo(chestInstanceId));
            Assert.That(playerInventory.RegisteredWorldItemCount, Is.Zero);
        }

        [Test]
        public void Equip_WhenQuickBarIsFull_RollsBackIntoChest()
        {
            ItemData itemData = CreateItemData("quickbar-rejected-item");
            ChestInventoryController chest = CreateGeneratedChest(itemData);
            InventoryRuntimeController playerInventory =
                CreatePlayerInventory();

            for (int slotIndex = 0;
                 slotIndex < playerInventory.QuickSlots.SlotCount;
                 slotIndex++)
            {
                FillQuickSlot(playerInventory, slotIndex);
            }

            ChestTransferService transfer = CreateTransferService(
                playerInventory);
            string chestInstanceId = GetOnlyChestInstanceId(chest);

            bool equipped = transfer.TryEquip(
                chest,
                chestInstanceId,
                out string playerInstanceId,
                out int quickSlotIndex,
                out string error);

            Assert.That(equipped, Is.False);
            Assert.That(playerInstanceId, Is.Null);
            Assert.That(
                quickSlotIndex,
                Is.EqualTo(InventoryQuickSlotEquipService.NoActiveSlot));
            Assert.That(error, Does.Contain("quick slots are occupied"));
            Assert.That(chest.ItemCount, Is.EqualTo(1));
            Assert.That(GetOnlyChestInstanceId(chest), Is.EqualTo(chestInstanceId));
            Assert.That(playerInventory.RegisteredWorldItemCount, Is.Zero);
            Assert.That(
                playerInventory.Grid.Placements.Count,
                Is.EqualTo(playerInventory.QuickSlots.SlotCount));
        }

        private ChestTransferService CreateTransferService(
            InventoryRuntimeController inventory)
        {
            GameObject playerRoot = Track(
                new GameObject("Chest Transfer Test Player"));
            playerRoot.SetActive(false);

            PlayerView playerView = playerRoot.AddComponent<PlayerView>();
            PlayerControlBlocker blocker =
                playerRoot.AddComponent<PlayerControlBlocker>();
            GameObject handRoot = Track(new GameObject("Right Hand Item Place"));
            handRoot.transform.SetParent(playerRoot.transform, false);
            playerView.RightHandItemPlace = handRoot.transform;
            playerRoot.SetActive(true);

            _hands = new PlayerHandsContainer(
                Array.Empty<IUsable>(),
                playerView);
            _equipService = new InventoryQuickSlotEquipService(
                inventory,
                _hands,
                blocker);
            _equipService.Initialize();

            return new ChestTransferService(
                inventory,
                _equipService,
                _hands);
        }

        private InventoryRuntimeController CreatePlayerInventory()
        {
            GameObject root = Track(
                new GameObject("Chest Transfer Test Inventory"));
            root.SetActive(false);
            InventoryRuntimeController inventory =
                root.AddComponent<InventoryRuntimeController>();

            SetPrivateField(
                inventory,
                "_initializeOnAwake",
                false);
            root.SetActive(true);

            Assert.That(
                inventory.TryInitialize(out string error),
                Is.True,
                error);
            return inventory;
        }

        private ChestInventoryController CreateGeneratedChest(
            ItemData itemData)
        {
            ChestLootEntry entry = new ChestLootEntry(
                itemData,
                ItemRarity.Common,
                weight: 1);
            ChestLootTable table = Track(
                ScriptableObject.CreateInstance<ChestLootTable>());

            table.name = "Transfer Test Loot Table";
            SetPrivateField(
                table,
                "_entries",
                new List<ChestLootEntry> { entry });

            GameObject root = Track(new GameObject("Transfer Test Chest"));
            root.SetActive(false);
            ChestInventoryController chest =
                root.AddComponent<ChestInventoryController>();

            SetPrivateField(chest, "_lootTable", table);
            SetPrivateField(chest, "_exactWeightBudget", 1);
            SetPrivateField(chest, "_columns", 1);
            SetPrivateField(chest, "_rows", 1);
            root.SetActive(true);

            Assert.That(
                chest.TryEnsureGenerated(seed: 123, out string error),
                Is.True,
                error);
            return chest;
        }

        private ItemData CreateItemData(
            string definitionId,
            int width = 1,
            int height = 1,
            bool createWorldPrefab = true,
            int maxStack = 1)
        {
            ItemData itemData = Track(
                ScriptableObject.CreateInstance<ItemData>());

            itemData.name = $"Item Data {definitionId}";
            itemData.InventoryDefinitionId = definitionId;
            itemData.InventoryWidth = width;
            itemData.InventoryHeight = height;
            itemData.InventoryMaxStack = maxStack;
            itemData.CanRotateInInventory = true;
            itemData.CanAssignQuickSlot = true;
            itemData.InventoryModelScaleMultiplier = 1f;
            itemData.InventoryModelFitPadding = 0.08f;
            itemData.InventoryModelMaxDepthInCells = 0.75f;
            itemData.InventoryModelPrefab = Track(
                GameObject.CreatePrimitive(PrimitiveType.Cube));

            if (createWorldPrefab)
                itemData.WorldItemPrefab = CreateWorldItem(itemData);

            return itemData;
        }

        private void FillQuickSlot(
            InventoryRuntimeController inventory,
            int slotIndex)
        {
            ItemData itemData = CreateItemData(
                $"quick-slot-{slotIndex}",
                createWorldPrefab: false);
            InventoryOperationResult addResult =
                inventory.AddFirstAvailable(
                    itemData,
                    quantity: 1,
                    out string instanceId,
                    out string addError);

            Assert.That(addResult.IsSuccess, Is.True, addError);

            InventoryOperationResult assignResult =
                inventory.AssignQuickSlot(slotIndex, instanceId);

            Assert.That(assignResult.IsSuccess, Is.True);
        }

        private Item CreateWorldItem(ItemData itemData)
        {
            GameObject root = Track(
                new GameObject($"{itemData.name} World Prefab"));
            root.SetActive(false);
            root.AddComponent<BoxCollider>();
            root.AddComponent<TestUsable>();
            Item item = root.AddComponent<Item>();
            item.ItemData = itemData;
            root.SetActive(true);
            return item;
        }

        private static string GetOnlyChestInstanceId(
            ChestInventoryController chest)
        {
            using (IEnumerator<ChestStoredItem> enumerator =
                   chest.StoredItems.GetEnumerator())
            {
                Assert.That(enumerator.MoveNext(), Is.True);
                string instanceId = enumerator.Current.InstanceId;
                Assert.That(enumerator.MoveNext(), Is.False);
                return instanceId;
            }
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

        public sealed class TestUsable : MonoBehaviour, IUsable
        {
            public ItemUseResult Use()
            {
                return new ItemUseResult
                {
                    UsageType = UsageType.ClickToUse,
                    ItemStateAfterUse = ItemState.Hold
                };
            }

            public void AltMode()
            {
            }

            public void ParseItem(Item itemObject)
            {
            }

            public void GetItemUseDelays(
                out float startDelay,
                out float useDelay)
            {
                startDelay = 0f;
                useDelay = 0f;
            }
        }
    }
}
