using System;
using System.Collections.Generic;
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
