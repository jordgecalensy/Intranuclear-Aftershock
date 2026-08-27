using System;
using System.Collections.Generic;
using System.Reflection;
using Failsafe.Inventory.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Failsafe.Inventory.Presentation.Tests
{
    public sealed class InventoryGridPresenter3DTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<GameObject> _createdObjects =
            new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                if (_createdObjects[index] != null)
                    Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Initialize_RebuildsViewsForExistingPlacements()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("existing", 2, 1);
            InventoryOperationResult placementResult = grid.TryPlace(
                item,
                new InventoryGridPosition(2, 1));

            Assert.That(placementResult.IsSuccess, Is.True);

            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            presenter.Initialize(grid, gridSpace, resolver);

            Assert.That(presenter.ViewCount, Is.EqualTo(1));
            Assert.That(
                presenter.TryGetView(item.InstanceId, out InventoryItemView3D view),
                Is.True);

            AssertVector(
                view.transform.localPosition,
                gridSpace.GetPlacementCenter(
                    new InventoryGridPosition(2, 1),
                    new InventoryGridSize(2, 1)));
        }

        [Test]
        public void ItemPlaced_CreatesViewAutomatically()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("placed", 1, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            presenter.Initialize(
                grid,
                new InventoryGridSpace3D(6, 5, 1f),
                resolver);

            InventoryOperationResult result = grid.TryPlace(
                item,
                new InventoryGridPosition(0, 0));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(presenter.ViewCount, Is.EqualTo(1));
            Assert.That(presenter.TryGetView(item.InstanceId, out _), Is.True);
            Assert.That(
                presenter.TryGetHitTarget(
                    item.InstanceId,
                    out InventoryItemHitTarget3D hitTarget),
                Is.True);
            Assert.That(hitTarget.InstanceId, Is.EqualTo(item.InstanceId));
            Assert.That(hitTarget.Collider.isTrigger, Is.True);
            AssertVector(hitTarget.Collider.size, Vector3.one);
        }

        [Test]
        public void PlacementChanged_UpdatesPositionAndAbsoluteRotation()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("moving", 2, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            presenter.Initialize(grid, gridSpace, resolver);

            Assert.That(
                grid.TryPlace(item, new InventoryGridPosition(0, 0)).IsSuccess,
                Is.True);
            Assert.That(
                grid.TryMove(item.InstanceId, new InventoryGridPosition(2, 2)).IsSuccess,
                Is.True);
            Assert.That(
                presenter.TryGetView(item.InstanceId, out InventoryItemView3D view),
                Is.True);

            AssertVector(
                view.transform.localPosition,
                gridSpace.GetPlacementCenter(
                    new InventoryGridPosition(2, 2),
                    new InventoryGridSize(2, 1)));

            Assert.That(grid.TryRotate(item.InstanceId).IsSuccess, Is.True);

            AssertVector(
                view.transform.localPosition,
                gridSpace.GetPlacementCenter(
                    new InventoryGridPosition(2, 2),
                    new InventoryGridSize(1, 2)));
            Assert.That(
                Quaternion.Angle(
                    view.AppliedGridRotation,
                    Quaternion.AngleAxis(90f, Vector3.up)),
                Is.LessThan(Tolerance));

            Assert.That(grid.TryRotate(item.InstanceId).IsSuccess, Is.True);
            Assert.That(
                Quaternion.Angle(
                    view.AppliedGridRotation,
                    Quaternion.identity),
                Is.LessThan(Tolerance));
        }

        [Test]
        public void TryPreviewPlacement_ChangesOnlyViewUntilCoreRelocation()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("preview", 2, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            presenter.Initialize(grid, gridSpace, resolver);

            Assert.That(
                grid.TryPlace(item, new InventoryGridPosition(0, 0)).IsSuccess,
                Is.True);

            bool previewed = presenter.TryPreviewPlacement(
                item.InstanceId,
                new InventoryGridPosition(3, 1),
                new InventoryGridSize(1, 2),
                InventoryItemRotation.Clockwise90);

            Assert.That(previewed, Is.True);
            Assert.That(
                presenter.TryGetView(item.InstanceId, out InventoryItemView3D view),
                Is.True);
            AssertVector(
                view.transform.localPosition,
                gridSpace.GetPlacementCenter(
                    new InventoryGridPosition(3, 1),
                    new InventoryGridSize(1, 2)));
            Assert.That(
                Quaternion.Angle(
                    view.AppliedGridRotation,
                    Quaternion.AngleAxis(90f, Vector3.up)),
                Is.LessThan(Tolerance));

            Assert.That(
                presenter.TryGetHitTarget(
                    item.InstanceId,
                    out InventoryItemHitTarget3D hitTarget),
                Is.True);
            AssertVector(hitTarget.Collider.size, new Vector3(1f, 1f, 2f));

            Assert.That(
                grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement),
                Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(0, 0)));
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Default));
        }

        [Test]
        public void TryPreviewFreePosition_MovesOnlyViewAndHitTarget()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("free-preview", 2, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            presenter.Initialize(
                grid,
                new InventoryGridSpace3D(6, 5, 1f),
                resolver);

            Assert.That(
                grid.TryPlace(item, new InventoryGridPosition(0, 0)).IsSuccess,
                Is.True);

            Vector3 previewPosition = new Vector3(0.37f, 0f, -0.22f);
            bool previewed = presenter.TryPreviewFreePosition(
                item.InstanceId,
                previewPosition,
                new InventoryGridSize(1, 2),
                InventoryItemRotation.Clockwise90);

            Assert.That(previewed, Is.True);
            Assert.That(
                presenter.TryGetView(item.InstanceId, out InventoryItemView3D view),
                Is.True);
            AssertVector(view.transform.localPosition, previewPosition);
            Assert.That(
                Quaternion.Angle(
                    view.AppliedGridRotation,
                    Quaternion.AngleAxis(90f, Vector3.up)),
                Is.LessThan(Tolerance));

            Assert.That(
                presenter.TryGetHitTarget(
                    item.InstanceId,
                    out InventoryItemHitTarget3D hitTarget),
                Is.True);
            AssertVector(hitTarget.Collider.size, new Vector3(1f, 1f, 2f));

            Assert.That(
                grid.TryGetPlacement(item.InstanceId, out InventoryPlacement placement),
                Is.True);
            Assert.That(placement.Origin, Is.EqualTo(new InventoryGridPosition(0, 0)));
            Assert.That(item.Rotation, Is.EqualTo(InventoryItemRotation.Default));
        }

        [Test]
        public void PlacementHighlight_TogglesManualValidAndInvalidVisuals()
        {
            InventoryGridPresenter3D presenter = CreatePresenter();
            presenter.Initialize(
                new InventoryGridModel(),
                new InventoryGridSpace3D(6, 5, 1f),
                new FakeResolver());

            InventoryRobotPresentationLayout3D layout =
                CreateManualHighlightLayout(
                    out GameObject[] validHighlights,
                    out GameObject[] invalidHighlights);

            presenter.SetManualGridLayout(layout);

            Assert.That(
                presenter.ShowPlacementHighlight(
                    new InventoryGridPosition(4, 3),
                    new InventoryGridSize(2, 2),
                    true),
                Is.True);

            Assert.That(CountActive(validHighlights), Is.EqualTo(4));
            Assert.That(CountActive(invalidHighlights), Is.Zero);
            Assert.That(validHighlights[22].activeSelf, Is.True);
            Assert.That(validHighlights[23].activeSelf, Is.True);
            Assert.That(validHighlights[28].activeSelf, Is.True);
            Assert.That(validHighlights[29].activeSelf, Is.True);

            Assert.That(
                presenter.ShowPlacementHighlight(
                    new InventoryGridPosition(1, 1),
                    new InventoryGridSize(1, 2),
                    false),
                Is.True);

            Assert.That(CountActive(validHighlights), Is.Zero);
            Assert.That(CountActive(invalidHighlights), Is.EqualTo(2));
            Assert.That(invalidHighlights[7].activeSelf, Is.True);
            Assert.That(invalidHighlights[13].activeSelf, Is.True);

            presenter.HidePlacementHighlight();

            Assert.That(CountActive(validHighlights), Is.Zero);
            Assert.That(CountActive(invalidHighlights), Is.Zero);
        }

        [Test]
        public void ItemFootprintVisual_FollowsItemLifecycleAndSelection()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("footprint", 2, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            Assert.That(
                grid.TryPlace(
                    item,
                    new InventoryGridPosition(1, 1)).IsSuccess,
                Is.True);

            InventoryGridPresenter3D presenter = CreatePresenter();
            presenter.Initialize(
                grid,
                new InventoryGridSpace3D(6, 5, 1f),
                resolver);

            InventoryRobotPresentationLayout3D layout =
                CreateFootprintLayout(6, 5);

            presenter.SetManualGridLayout(layout);

            Assert.That(layout.ItemFootprintVisualCount, Is.EqualTo(1));
            Assert.That(
                layout.TryGetItemFootprintVisual(
                    item.InstanceId,
                    out RectTransform visualRoot),
                Is.True);

            AssertVector2(
                visualRoot.sizeDelta,
                new Vector2(20f, 10f));

            Transform selectedState = visualRoot.Find("Selected");
            Assert.That(selectedState, Is.Not.Null);
            Assert.That(selectedState.gameObject.activeSelf, Is.False);

            Assert.That(
                presenter.SetSelectedItem(item.InstanceId),
                Is.True);
            Assert.That(selectedState.gameObject.activeSelf, Is.True);

            Assert.That(grid.TryRotate(item.InstanceId).IsSuccess, Is.True);
            Assert.That(
                layout.TryGetItemFootprintVisual(
                    item.InstanceId,
                    out visualRoot),
                Is.True);

            AssertVector2(
                visualRoot.sizeDelta,
                new Vector2(10f, 20f));
            Assert.That(
                visualRoot.Find("Selected").gameObject.activeSelf,
                Is.True);

            Assert.That(
                presenter.TryPreviewFreePosition(
                    item.InstanceId,
                    Vector3.zero,
                    new InventoryGridSize(1, 2),
                    InventoryItemRotation.Clockwise90),
                Is.True);
            Assert.That(visualRoot.gameObject.activeSelf, Is.False);

            Assert.That(
                presenter.RestorePlacement(item.InstanceId),
                Is.True);
            Assert.That(visualRoot.gameObject.activeSelf, Is.True);

            presenter.ClearSelectedItem();
            Assert.That(
                visualRoot.Find("Selected").gameObject.activeSelf,
                Is.False);

            Assert.That(grid.TryRemove(item.InstanceId).IsSuccess, Is.True);
            Assert.That(layout.ItemFootprintVisualCount, Is.Zero);
            Assert.That(
                layout.TryGetItemFootprintVisual(item.InstanceId, out _),
                Is.False);
        }

        [Test]
        public void Initialize_CreatesSixByFivePrototypeGridVisual()
        {
            InventoryGridPresenter3D presenter = CreatePresenter();
            presenter.Initialize(
                new InventoryGridModel(),
                new InventoryGridSpace3D(6, 5, 1f),
                new FakeResolver());

            Assert.That(
                presenter.TryGetPrototypeGridVisual(
                    out InventoryPrototypeGridVisual3D visual),
                Is.True);
            Assert.That(visual.IsInitialized, Is.True);
            Assert.That(visual.Columns, Is.EqualTo(6));
            Assert.That(visual.Rows, Is.EqualTo(5));
            Assert.That(visual.VerticalLineCount, Is.EqualTo(7));
            Assert.That(visual.HorizontalLineCount, Is.EqualTo(6));
            Assert.That(visual.LineCount, Is.EqualTo(13));
        }

        [Test]
        public void RestorePlacement_ReappliesAuthoritativeCorePlacement()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("restore", 2, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            presenter.Initialize(grid, gridSpace, resolver);

            Assert.That(
                grid.TryPlace(item, new InventoryGridPosition(1, 2)).IsSuccess,
                Is.True);
            Assert.That(
                presenter.TryPreviewPlacement(
                    item.InstanceId,
                    new InventoryGridPosition(4, 0),
                    new InventoryGridSize(1, 2),
                    InventoryItemRotation.Clockwise90),
                Is.True);

            Assert.That(presenter.RestorePlacement(item.InstanceId), Is.True);
            Assert.That(
                presenter.TryGetView(item.InstanceId, out InventoryItemView3D view),
                Is.True);

            AssertVector(
                view.transform.localPosition,
                gridSpace.GetPlacementCenter(
                    new InventoryGridPosition(1, 2),
                    new InventoryGridSize(2, 1)));
            Assert.That(
                Quaternion.Angle(
                    view.AppliedGridRotation,
                    Quaternion.identity),
                Is.LessThan(Tolerance));

            Assert.That(
                presenter.TryGetHitTarget(
                    item.InstanceId,
                    out InventoryItemHitTarget3D hitTarget),
                Is.True);
            AssertVector(hitTarget.Collider.size, new Vector3(2f, 1f, 1f));
        }

        [Test]
        public void ItemRemoved_DestroysViewAndForgetsIt()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("removed", 1, 1);
            FakeResolver resolver = new FakeResolver();
            resolver.Register(item.InstanceId, CreateDefinition());

            InventoryGridPresenter3D presenter = CreatePresenter();
            presenter.Initialize(
                grid,
                new InventoryGridSpace3D(6, 5, 1f),
                resolver);

            Assert.That(
                grid.TryPlace(item, new InventoryGridPosition(0, 0)).IsSuccess,
                Is.True);
            Assert.That(grid.TryRemove(item.InstanceId).IsSuccess, Is.True);

            Assert.That(presenter.ViewCount, Is.Zero);
            Assert.That(presenter.TryGetView(item.InstanceId, out _), Is.False);
        }

        [Test]
        public void Initialize_WhenResolverFails_RollsBackCreatedViews()
        {
            InventoryGridModel grid = new InventoryGridModel();
            InventoryItemModel item = CreateItem("missing", 1, 1);

            Assert.That(
                grid.TryPlace(item, new InventoryGridPosition(0, 0)).IsSuccess,
                Is.True);

            InventoryGridPresenter3D presenter = CreatePresenter();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => presenter.Initialize(
                    grid,
                    new InventoryGridSpace3D(6, 5, 1f),
                    new FakeResolver()));

            Assert.That(exception.Message, Does.Contain(item.InstanceId));
            Assert.That(presenter.IsInitialized, Is.False);
            Assert.That(presenter.ViewCount, Is.Zero);
        }

        private InventoryGridPresenter3D CreatePresenter()
        {
            GameObject presenterObject = new GameObject("Inventory Presenter Test");
            _createdObjects.Add(presenterObject);
            return presenterObject.AddComponent<InventoryGridPresenter3D>();
        }

        private InventoryRobotPresentationLayout3D
            CreateManualHighlightLayout(
                out GameObject[] validHighlights,
                out GameObject[] invalidHighlights)
        {
            GameObject layoutObject = new GameObject(
                "Inventory Robot Layout Test");
            _createdObjects.Add(layoutObject);

            InventoryRobotPresentationLayout3D layout =
                layoutObject.AddComponent<
                    InventoryRobotPresentationLayout3D>();

            GameObject cellsRootObject = new GameObject(
                "Cells",
                typeof(RectTransform));
            _createdObjects.Add(cellsRootObject);

            RectTransform cellsRoot =
                cellsRootObject.GetComponent<RectTransform>();

            validHighlights = new GameObject[30];
            invalidHighlights = new GameObject[30];

            for (int index = 0; index < 30; index++)
            {
                GameObject cell = new GameObject(
                    $"Cell {index}",
                    typeof(RectTransform));
                cell.transform.SetParent(cellsRoot, false);

                GameObject valid = new GameObject("HighlightValid");
                valid.transform.SetParent(cell.transform, false);
                valid.SetActive(false);
                validHighlights[index] = valid;

                GameObject invalid = new GameObject("HighlightInvalid");
                invalid.transform.SetParent(cell.transform, false);
                invalid.SetActive(false);
                invalidHighlights[index] = invalid;
            }

            SetPrivateField(layout, "_gridCellsRoot", cellsRoot);
            return layout;
        }

        private InventoryRobotPresentationLayout3D CreateFootprintLayout(
            int columns,
            int rows)
        {
            GameObject layoutObject = new GameObject(
                "Inventory Footprint Layout Test",
                typeof(RectTransform));
            _createdObjects.Add(layoutObject);

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
                    cell.anchoredPosition = new Vector2(
                        (column + 0.5f) * 10f,
                        -(row + 0.5f) * 10f);
                }
            }

            RectTransform visualsRoot = new GameObject(
                "Item Footprints",
                typeof(RectTransform)).GetComponent<RectTransform>();

            visualsRoot.SetParent(layoutObject.transform, false);
            visualsRoot.sizeDelta = cellsRoot.sizeDelta;

            RectTransform template = new GameObject(
                "Item Footprint Template",
                typeof(RectTransform)).GetComponent<RectTransform>();

            template.SetParent(visualsRoot, false);

            GameObject selectedState = new GameObject(
                "Selected",
                typeof(RectTransform));

            selectedState.transform.SetParent(template, false);
            selectedState.SetActive(false);
            template.gameObject.SetActive(false);

            SetPrivateField(layout, "_gridCellsRoot", cellsRoot);
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

        private static int CountActive(GameObject[] objects)
        {
            int count = 0;

            foreach (GameObject target in objects)
            {
                if (target.activeSelf)
                    count++;
            }

            return count;
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

        private InventoryModelViewDefinition CreateDefinition()
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Inventory Test Model";
            _createdObjects.Add(model);

            return new InventoryModelViewDefinition(
                model,
                Quaternion.identity,
                Vector3.zero,
                1f,
                0.08f,
                0.75f);
        }

        private static InventoryItemModel CreateItem(
            string instanceId,
            int width,
            int height)
        {
            return new InventoryItemModel(
                instanceId,
                $"definition-{instanceId}",
                new InventoryGridSize(width, height));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        private static void AssertVector2(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        private sealed class FakeResolver : IInventoryItemViewDefinitionResolver
        {
            private readonly Dictionary<string, InventoryModelViewDefinition> _definitions =
                new Dictionary<string, InventoryModelViewDefinition>(StringComparer.Ordinal);

            public void Register(
                string instanceId,
                InventoryModelViewDefinition definition)
            {
                _definitions.Add(instanceId, definition);
            }

            public bool TryResolve(
                InventoryItemModel item,
                out InventoryModelViewDefinition definition,
                out string error)
            {
                if (item != null &&
                    _definitions.TryGetValue(item.InstanceId, out definition))
                {
                    error = null;
                    return true;
                }

                definition = null;
                error = $"No view definition is registered for '{item?.InstanceId ?? "<null>"}'.";
                return false;
            }
        }
    }
}
