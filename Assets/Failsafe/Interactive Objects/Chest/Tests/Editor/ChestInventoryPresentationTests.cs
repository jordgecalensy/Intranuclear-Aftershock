using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Core;
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

        [TestCase(2, 1, 2, 1)]
        [TestCase(6, 3, 6, 5)]
        [TestCase(4, 4, 6, 5)]
        [TestCase(8, 5, 6, 5)]
        [TestCase(1, 1, 6, 5)]
        [TestCase(1, 7, 2, 1)]
        public void Initialize_RebuildsHiddenGridLayoutWithoutQuickSlots(
            int columns, int rows, int initialColumns, int initialRows)
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
            SetPrivateField(chest, "_columns", columns);
            SetPrivateField(chest, "_rows", rows);

            InventoryRobotPresentationLayout3D layout =
                CreateGridOnlyLayout(chestRoot.transform, initialColumns, initialRows);
            RectTransform frame = initialColumns == 6 ? AddGridFrame(layout) : null;
            Vector3 frameCenter = frame != null ? frame.TransformPoint(frame.rect.center) : Vector3.zero;

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
                layout.TryValidateGrid(columns, rows, out string gridError),
                Is.True,
                gridError);
            Assert.That(
                layout.TryValidate(
                    columns,
                    rows,
                    quickSlotCount: 1,
                    out string quickSlotError),
                Is.False);
            Assert.That(quickSlotError, Does.Contain("Quick-slots root"));

            Assert.That(presentation.SetVisible(true), Is.True);
            Assert.That(presentation.IsVisible, Is.True);
            AssertGridMatchesModel(presentation, layout, columns, rows);
            if (frame != null)
                AssertGridFrame(layout, frame, frameCenter, columns, rows);

            int cellCount = layout.GridCellsRoot.childCount;
            presentation.Dispose();
            Assert.That(presentation.TryInitialize(out presentationError), Is.True, presentationError);
            Assert.That(layout.GridCellsRoot.childCount, Is.EqualTo(cellCount));
            Assert.That(presentation.IsVisible, Is.False);
            Assert.That(presentation.SetVisible(true), Is.True);
            AssertGridMatchesModel(presentation, layout, columns, rows);
            if (frame != null)
                AssertGridFrame(layout, frame, frameCenter, columns, rows);

            string instanceId = GetOnlyChestInstanceId(chest);
            if (columns == 2 && rows == 1)
                VerifyPointerInteraction(chest, presentation, instanceId);
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

        private static RectTransform AddGridFrame(InventoryRobotPresentationLayout3D layout)
        {
            RectTransform cells = layout.GridCellsRoot;
            Vector2 availableSize = cells.rect.size;
            RectTransform frame = new GameObject("GridFrame", typeof(RectTransform))
                .GetComponent<RectTransform>();
            frame.SetParent(cells.parent, false);
            frame.pivot = new Vector2(0.2f, 0.8f);
            frame.sizeDelta = availableSize + Vector2.one * 4f;
            frame.anchoredPosition = new Vector2(7f, 11f);
            RectTransform panel = new GameObject("GridPanel", typeof(RectTransform))
                .GetComponent<RectTransform>();
            panel.SetParent(frame, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.sizeDelta = -Vector2.one * 4f;
            cells.SetParent(panel, false);
            cells.anchorMin = Vector2.zero;
            cells.anchorMax = Vector2.one;
            cells.sizeDelta = Vector2.zero;
            cells.anchoredPosition = Vector2.zero;
            frame.ForceUpdateRectTransforms();
            return frame;
        }

        private static void AssertGridFrame(InventoryRobotPresentationLayout3D layout,
            RectTransform frame, Vector3 originalCenter, int columns, int rows)
        {
            GridLayoutGroup grid = layout.GridCellsRoot.GetComponent<GridLayoutGroup>();
            Vector2 occupiedSize = new Vector2(columns * grid.cellSize.x, rows * grid.cellSize.y);
            Assert.That(Vector2.Distance(frame.rect.size, occupiedSize + Vector2.one * 4f), Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(layout.GridCellsRoot.rect.size, occupiedSize), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(frame.TransformPoint(frame.rect.center), originalCenter), Is.LessThan(0.001f));
        }

        private static void AssertGridMatchesModel(ChestInventoryPresenter3D presentation,
            InventoryRobotPresentationLayout3D layout, int columns, int rows)
        {
            RectTransform root = layout.GridCellsRoot;
            GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
            Assert.That(grid.constraintCount, Is.EqualTo(columns));
            Assert.That(grid.cellSize.x, Is.EqualTo(grid.cellSize.y));
            Vector3[] corners = new Vector3[4];
            for (int index = 0; index < root.childCount; index++)
            {
                RectTransform cell = (RectTransform)root.GetChild(index);
                Assert.That(cell.gameObject.activeSelf, Is.EqualTo(index < columns * rows));
                if (index >= columns * rows)
                    continue;

                Assert.That(cell.Find("HighlightValid"), Is.Not.Null);
                Assert.That(cell.Find("HighlightInvalid"), Is.Not.Null);
                InventoryGridPosition position = new InventoryGridPosition(index % columns, index / columns);
                Vector3 visibleCenter = cell.TransformPoint(cell.rect.center);
                Vector3 modelCenter = presentation.Presenter.transform.TransformPoint(
                    presentation.Presenter.GridSpace.GetCellCenter(position));
                Assert.That(Vector3.Distance(visibleCenter, modelCenter), Is.LessThan(0.001f));
                Assert.That(presentation.Presenter.GridSpace.TryGetGridPosition(
                    presentation.Presenter.transform.InverseTransformPoint(visibleCenter), out var hitCell), Is.True);
                Assert.That(hitCell, Is.EqualTo(position));
                cell.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = root.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(root.rect.xMin - 0.001f, root.rect.xMax + 0.001f));
                    Assert.That(local.y, Is.InRange(root.rect.yMin - 0.001f, root.rect.yMax + 0.001f));
                }
            }
        }

        [Test]
        public void ClickSequence_RequiresSameItemAndInterval_AndResetsOnClose()
        {
            GameObject root = Track(new GameObject("Click sequence"));
            ChestItemContextMenuController3D controller = root.AddComponent<ChestItemContextMenuController3D>();
            Assert.That(Invoke(controller, "RegisterClick", "a", 1f), Is.False);
            Assert.That(Invoke(controller, "RegisterClick", "b", 1.1f), Is.False);
            Assert.That(Invoke(controller, "RegisterClick", "b", 1.2f), Is.True);
            Assert.That(Invoke(controller, "RegisterClick", "b", 1.3f), Is.False);
            controller.CloseAll();
            Assert.That(Invoke(controller, "RegisterClick", "b", 1.4f), Is.False);
            Assert.That(Invoke(controller, "RegisterClick", "b", 2f), Is.False);
        }

        [Test]
        public void CanvasCamera_UsesUntaggedPlayerCameraForHiddenMenuAndInfo()
        {
            GameObject root = Track(new GameObject("Chest UI"));
            ChestInventoryPresenter3D presentation = root.AddComponent<ChestInventoryPresenter3D>();
            ChestItemContextMenuController3D controller = root.AddComponent<ChestItemContextMenuController3D>();
            Camera camera = Track(new GameObject("Untagged camera")).AddComponent<Camera>();
            Canvas canvas = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas))
                .GetComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform menu = new GameObject("Menu", typeof(RectTransform)).GetComponent<RectTransform>();
            menu.SetParent(canvas.transform, false);
            Canvas infoCanvas = new GameObject("Info", typeof(RectTransform), typeof(Canvas))
                .GetComponent<Canvas>();
            infoCanvas.transform.SetParent(root.transform, false);
            infoCanvas.renderMode = RenderMode.WorldSpace;
            infoCanvas.gameObject.SetActive(false);
            SetPrivateField(controller, "_presentation", presentation);
            SetPrivateField(controller, "_menuRoot", menu);
            SetPrivateField(controller, "_playerCamera", camera);
            Invoke(controller, "ConfigureCanvasCameras");
            Assert.That(camera.CompareTag("MainCamera"), Is.False);
            Assert.That(canvas.worldCamera, Is.SameAs(camera));
            Assert.That(infoCanvas.worldCamera, Is.SameAs(camera));
            Assert.That(infoCanvas.gameObject.activeSelf, Is.False);
        }

        private void VerifyPointerInteraction(ChestInventoryController chest,
            ChestInventoryPresenter3D presentation, string instanceId)
        {
            ChestItemContextMenuController3D controller =
                chest.gameObject.AddComponent<ChestItemContextMenuController3D>();
            Camera camera = Track(new GameObject("Interaction camera")).AddComponent<Camera>();
            SetPrivateField(controller, "_chest", chest);
            SetPrivateField(controller, "_presentation", presentation);
            SetPrivateField(controller, "_playerCamera", camera);
            SetPrivateField(controller, "_inventoryLayerMask", 1 << LayerMask.NameToLayer("Inventory"));
            SetPrivateField(controller, "_maximumRayDistance", 100f);
            InventoryGridPresenter3D presenter = presentation.Presenter;
            Assert.That(chest.Grid.TryGetPlacement(instanceId, out InventoryPlacement original), Is.True);
            InventoryGridPosition target = new InventoryGridPosition(1 - original.Origin.Column, 0);
            Physics.SyncTransforms();
            Invoke(controller, "BeginPointerPress", CellRay(presenter, original.Origin), Vector2.zero);
            Assert.That(controller.TryCloseTopmost(), Is.True);
            Assert.That(chest.Grid.TryGetPlacement(instanceId, out InventoryPlacement cancelled), Is.True);
            Assert.That(cancelled.Origin, Is.EqualTo(original.Origin));

            Invoke(controller, "BeginPointerPress", CellRay(presenter, original.Origin), Vector2.zero);
            SetPrivateField(controller, "<IsDragging>k__BackingField", true);
            Invoke(controller, "EndPointerPress", CellRay(presenter, target), new Vector2(100f, 0f));
            Assert.That(chest.Grid.TryGetPlacement(instanceId, out InventoryPlacement moved), Is.True);
            Assert.That(moved.Origin, Is.EqualTo(target));
            Assert.That(controller.IsDragging, Is.False);

            Physics.SyncTransforms();
            Invoke(controller, "BeginPointerPress", CellRay(presenter, target), Vector2.zero);
            SetPrivateField(controller, "<IsDragging>k__BackingField", true);
            Invoke(controller, "EndPointerPress", CellRay(presenter, new InventoryGridPosition(-2, 0)),
                new Vector2(100f, 0f));
            Assert.That(chest.Grid.TryGetPlacement(instanceId, out InventoryPlacement rejected), Is.True);
            Assert.That(rejected.Origin, Is.EqualTo(target));
            Assert.That(Invoke(controller, "RegisterClick", instanceId, 1f), Is.False);
        }

        private static Ray CellRay(InventoryGridPresenter3D presenter, InventoryGridPosition cell)
        {
            Vector3 point = presenter.GridSpace.GetCellCenter(cell);
            return new Ray(presenter.transform.TransformPoint(point + Vector3.up * 2f),
                -presenter.transform.up);
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, method);
            return info.Invoke(target, args);
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
                    foreach (string highlightName in new[] { "HighlightValid", "HighlightInvalid" })
                    {
                        GameObject highlight = new GameObject(highlightName, typeof(RectTransform));
                        highlight.transform.SetParent(cell, false);
                        highlight.SetActive(false);
                    }
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
