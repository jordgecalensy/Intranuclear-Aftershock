using Failsafe.Inventory.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Failsafe.Inventory.Presentation.Tests
{
    [TestFixture]
    public sealed class InventoryGridRaycaster3DTests
    {
        private GameObject _gridObject;

        [TearDown]
        public void TearDown()
        {
            if (_gridObject != null)
                Object.DestroyImmediate(_gridObject);
        }

        [Test]
        public void TryGetGridPosition_HandlesTranslatedAndRotatedGrid()
        {
            Transform gridTransform = CreateGridTransform();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 0.4f);
            InventoryGridPosition expected = new InventoryGridPosition(4, 3);
            Vector3 localCellCenter = gridSpace.GetPlacementCenter(
                expected,
                new InventoryGridSize(1, 1));
            Vector3 worldCellCenter = gridTransform.TransformPoint(localCellCenter);
            Ray ray = new Ray(
                worldCellCenter + gridTransform.up * 3f,
                -gridTransform.up);

            bool found = InventoryGridRaycaster3D.TryGetGridPosition(
                ray,
                gridTransform,
                gridSpace,
                out InventoryGridPosition actual);

            Assert.That(found, Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void TryGetGridPosition_WhenRayIsParallel_ReturnsFalse()
        {
            Transform gridTransform = CreateGridTransform();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            Ray ray = new Ray(
                gridTransform.position + gridTransform.up,
                gridTransform.right);

            bool found = InventoryGridRaycaster3D.TryGetGridPosition(
                ray,
                gridTransform,
                gridSpace,
                out _);

            Assert.That(found, Is.False);
        }

        [Test]
        public void TryGetGridPosition_WhenPlaneHitIsOutsideGrid_ReturnsFalse()
        {
            Transform gridTransform = CreateGridTransform();
            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            Vector3 outsidePoint = gridTransform.TransformPoint(
                new Vector3(10f, 0f, 0f));
            Ray ray = new Ray(
                outsidePoint + gridTransform.up * 2f,
                -gridTransform.up);

            bool found = InventoryGridRaycaster3D.TryGetGridPosition(
                ray,
                gridTransform,
                gridSpace,
                out _);

            Assert.That(found, Is.False);
        }

        private Transform CreateGridTransform()
        {
            _gridObject = new GameObject("Inventory Grid Raycaster Test");
            _gridObject.transform.position = new Vector3(4f, -2f, 7f);
            _gridObject.transform.rotation = Quaternion.Euler(25f, 37f, -11f);
            return _gridObject.transform;
        }
    }
}
