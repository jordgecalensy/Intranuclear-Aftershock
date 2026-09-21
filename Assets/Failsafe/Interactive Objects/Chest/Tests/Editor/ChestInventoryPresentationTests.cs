using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Presentation;
using Failsafe.Items;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Chests.Tests
{
    [TestFixture]
    public sealed class ChestInventoryPresentationTests
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
        public void Initialize_RebuildsHiddenGridLayoutWithoutQuickSlots()
        {
            ItemData itemData = CreateItemData("presentation-item");
            ChestLootTable table = Track(
                ScriptableObject.CreateInstance<ChestLootTable>());
            SetPrivateField(
                table,
                "_entries",
                new List<ChestLootEntry>
                {
                    new ChestLootEntry(
                        itemData,
                        ItemRarity.Common,
                        weight: 1)
                });

            GameObject chestRoot = Track(
                new GameObject("Presentation Test Chest"));
            chestRoot.SetActive(false);
            ChestInventoryController chest =
                chestRoot.AddComponent<ChestInventoryController>();
            ChestInventoryPresenter3D presentation =
                chestRoot.AddComponent<ChestInventoryPresenter3D>();

            SetPrivateField(chest, "_lootTable", table);
            SetPrivateField(chest, "_exactWeightBudget", 1);
            SetPrivateField(chest, "_columns", 2);
            SetPrivateField(chest, "_rows", 1);

            InventoryRobotPresentationLayout3D layout =
                CreateGridOnlyLayout(chestRoot.transform, 2, 1);

            SetPrivateField(presentation, "_inventory", chest);
            SetPrivateField(presentation, "_layout", layout);
            SetPrivateField(
                presentation,
                "_visualRoot",
                layout.gameObject);
            SetPrivateField(presentation, "_cellSize", 1f);
            SetPrivateField(
                presentation,
                "_inventoryLayerName",
                "Inventory");

            layout.gameObject.SetActive(false);

            chestRoot.SetActive(true);

            Assert.That(
                chest.TryEnsureGenerated(seed: 71, out string generationError),
                Is.True,
                generationError);
            Assert.That(
                presentation.TryInitialize(out string presentationError),
                Is.True,
                presentationError);
            Assert.That(presentation.IsInitialized, Is.True);
            Assert.That(presentation.IsVisible, Is.False);
            Assert.That(presentation.Presenter.ViewCount, Is.EqualTo(1));
            Assert.That(
                chestRoot.GetComponentInChildren<
                    InventoryQuickBarPresenter3D>(true),
                Is.Null);
            Assert.That(
                layout.TryValidateGrid(2, 1, out string gridError),
                Is.True,
                gridError);
            Assert.That(
                layout.TryValidate(
                    2,
                    1,
                    quickSlotCount: 1,
                    out string quickSlotError),
                Is.False);
            Assert.That(quickSlotError, Does.Contain("Quick-slots root"));

            Assert.That(presentation.SetVisible(true), Is.True);
            Assert.That(presentation.IsVisible, Is.True);

            string instanceId = GetOnlyChestInstanceId(chest);
            Assert.That(
                chest.TryDetach(
                    instanceId,
                    out ChestDetachedItem detachedItem,
                    out string detachError),
                Is.True,
                detachError);
            Assert.That(presentation.Presenter.ViewCount, Is.Zero);
            Assert.That(
                chest.TryRestoreDetached(
                    detachedItem,
                    out string restoreError),
                Is.True,
                restoreError);
            Assert.That(presentation.Presenter.ViewCount, Is.EqualTo(1));

            Assert.That(presentation.SetVisible(false), Is.True);
            Assert.That(presentation.IsVisible, Is.False);
        }

        private InventoryRobotPresentationLayout3D CreateGridOnlyLayout(
            Transform parent,
            int columns,
            int rows)
        {
            GameObject layoutObject = new GameObject(
                "Chest Grid Layout",
                typeof(RectTransform));
            layoutObject.transform.SetParent(parent, false);
            InventoryRobotPresentationLayout3D layout =
                layoutObject.AddComponent<
                    InventoryRobotPresentationLayout3D>();

            RectTransform cellsRoot = new GameObject(
                "Cells",
                typeof(RectTransform)).GetComponent<RectTransform>();
            cellsRoot.SetParent(layoutObject.transform, false);
            cellsRoot.sizeDelta = new Vector2(
                columns * 10f,
                rows * 10f);
            GridLayoutGroup gridLayout =
                cellsRoot.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(10f, 10f);
            gridLayout.spacing = Vector2.zero;
            gridLayout.startCorner =
                GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    RectTransform cell = new GameObject(
                        $"Cell {row * columns + column}",
                        typeof(RectTransform)).GetComponent<RectTransform>();
                    cell.SetParent(cellsRoot, false);
                    cell.anchorMin = new Vector2(0.5f, 0.5f);
                    cell.anchorMax = new Vector2(0.5f, 0.5f);
                    cell.pivot = new Vector2(0.5f, 0.5f);
                    cell.sizeDelta = new Vector2(10f, 10f);
                }
            }

            Transform itemsRoot = new GameObject("Items Root").transform;
            itemsRoot.SetParent(layoutObject.transform, false);

            RectTransform visualsRoot = new GameObject(
                "Item Footprints",
                typeof(RectTransform)).GetComponent<RectTransform>();
            visualsRoot.SetParent(layoutObject.transform, false);

            RectTransform template = new GameObject(
                "Item Footprint Template",
                typeof(RectTransform)).GetComponent<RectTransform>();
            template.SetParent(visualsRoot, false);

            GameObject selectedState = new GameObject(
                "Selected",
                typeof(RectTransform));
            selectedState.transform.SetParent(template, false);
            selectedState.SetActive(false);

            GameObject quantityText = new GameObject(
                "Quantity",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            quantityText.transform.SetParent(template, false);
            template.gameObject.SetActive(false);

            SetPrivateField(layout, "_gridCellsRoot", cellsRoot);
            SetPrivateField(layout, "_inventoryItemsRoot", itemsRoot);
            SetPrivateField(
                layout,
                "_itemFootprintVisualsRoot",
                visualsRoot);
            SetPrivateField(
                layout,
                "_itemFootprintVisualTemplate",
                template);
            return layout;
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
            itemData.CanRotateInInventory = true;
            itemData.CanAssignQuickSlot = true;
            itemData.InventoryModelScaleMultiplier = 1f;
            itemData.InventoryModelFitPadding = 0.08f;
            itemData.InventoryModelMaxDepthInCells = 0.75f;
            itemData.InventoryModelPrefab = Track(
                GameObject.CreatePrimitive(PrimitiveType.Cube));
            itemData.WorldItemPrefab = CreateWorldItem(itemData);
            return itemData;
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

        private T Track<T>(T target) where T : UnityEngine.Object
        {
            _objects.Add(target);
            return target;
        }

        private static void SetPrivateField<T>(
            object target,
            string fieldName,
            T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
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

            public void AltMode() { }
            public void ParseItem(Item itemObject) { }

            public void GetItemUseDelays(
                out float useStartDelay,
                out float useDelay)
            {
                useStartDelay = 0f;
                useDelay = 0f;
            }
        }
    }
}
