using System.Collections.Generic;
using Failsafe.Inventory.Core;
using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Inventory.Presentation.Tests
{
    [TestFixture]
    public sealed class InventoryItemView3DTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in _createdObjects)
            {
                if (createdObject != null)
                    Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Initialize_CentersModelIndependentlyOfSourcePivot()
        {
            GameObject model = CreateModel(
                new Vector3(1f, 1f, 1f),
                new Vector3(2f, 1f, -3f));

            InventoryItemView3D view = CreateView();
            view.Initialize(
                CreateDefinition(model, maxDepthInCells: 10f, fitPaddingRatio: 0f),
                new InventoryGridSize(2, 2),
                1f);

            AssertVector(view.UnscaledModelBounds.center, new Vector3(2f, 1f, -3f));
            AssertVector(view.CenterOffset, new Vector3(-2f, -1f, 3f));
        }

        [Test]
        public void Initialize_AutoFitsAfterCanonicalPoseIsApplied()
        {
            GameObject model = CreateModel(
                new Vector3(4f, 1f, 1f),
                Vector3.zero);

            InventoryItemView3D view = CreateView();
            Quaternion baseRotation = Quaternion.AngleAxis(90f, Vector3.up);

            view.Initialize(
                CreateDefinition(
                    model,
                    baseRotation,
                    fitPaddingRatio: 0.1f,
                    maxDepthInCells: 1f),
                new InventoryGridSize(1, 2),
                1f);

            Assert.That(view.AppliedScale, Is.EqualTo(0.4f).Within(Tolerance));
            Assert.That(
                Quaternion.Angle(view.AppliedBaseRotation, baseRotation),
                Is.LessThan(Tolerance));
        }

        [Test]
        public void ApplyPlacement_SetsGridRotationAbsolutelyWithoutAccumulation()
        {
            GameObject model = CreateModel(Vector3.one, Vector3.zero);
            InventoryItemView3D view = CreateView();
            Quaternion baseRotation = Quaternion.Euler(15f, 35f, 5f);

            view.Initialize(
                CreateDefinition(model, baseRotation),
                new InventoryGridSize(2, 1),
                1f);

            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            view.ApplyPlacement(
                new InventoryGridPosition(0, 0),
                new InventoryGridSize(1, 2),
                InventoryItemRotation.Clockwise90,
                gridSpace);

            Quaternion firstRotation = view.AppliedGridRotation;

            view.ApplyPlacement(
                new InventoryGridPosition(0, 0),
                new InventoryGridSize(1, 2),
                InventoryItemRotation.Clockwise90,
                gridSpace);

            Assert.That(
                Quaternion.Angle(firstRotation, view.AppliedGridRotation),
                Is.LessThan(Tolerance));

            Assert.That(
                Quaternion.Angle(view.AppliedBaseRotation, baseRotation),
                Is.LessThan(Tolerance));

            view.ApplyPlacement(
                new InventoryGridPosition(0, 0),
                new InventoryGridSize(2, 1),
                InventoryItemRotation.Default,
                gridSpace);

            Assert.That(
                Quaternion.Angle(view.AppliedGridRotation, Quaternion.identity),
                Is.LessThan(Tolerance));
        }

        [Test]
        public void ApplyPlacement_UsesCenterOfRotatedFootprint()
        {
            GameObject model = CreateModel(Vector3.one, Vector3.zero);
            InventoryItemView3D view = CreateView();
            view.Initialize(
                CreateDefinition(model),
                new InventoryGridSize(2, 1),
                1f);

            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);

            view.ApplyPlacement(
                new InventoryGridPosition(2, 1),
                new InventoryGridSize(1, 2),
                InventoryItemRotation.Clockwise90,
                gridSpace);

            AssertVector(view.transform.localPosition, new Vector3(-0.5f, 0f, 0.5f));
        }

        [Test]
        public void ApplyFreePreview_FollowsArbitraryPositionAndRotation()
        {
            GameObject model = CreateModel(Vector3.one, Vector3.zero);
            InventoryItemView3D view = CreateView();
            view.Initialize(
                CreateDefinition(model),
                new InventoryGridSize(2, 1),
                1f);

            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(6, 5, 1f);
            Vector3 previewPosition = new Vector3(0.37f, 0f, -0.22f);

            view.ApplyFreePreview(
                previewPosition,
                InventoryItemRotation.Clockwise90,
                gridSpace);

            AssertVector(view.transform.localPosition, previewPosition);
            Assert.That(
                Quaternion.Angle(
                    view.AppliedGridRotation,
                    Quaternion.AngleAxis(90f, Vector3.up)),
                Is.LessThan(Tolerance));
        }

        [Test]
        public void Initialize_DisablesPhysicsOnVisualCopy()
        {
            GameObject model = CreateModel(Vector3.one, Vector3.zero);
            Rigidbody sourceBody = model.AddComponent<Rigidbody>();
            sourceBody.isKinematic = false;

            InventoryItemView3D view = CreateView();
            view.Initialize(
                CreateDefinition(model),
                new InventoryGridSize(1, 1),
                1f);

            Collider copiedCollider = view.ModelInstance.GetComponentInChildren<Collider>();
            Rigidbody copiedBody = view.ModelInstance.GetComponent<Rigidbody>();

            Assert.That(copiedCollider.enabled, Is.False);
            Assert.That(copiedBody.isKinematic, Is.True);
            Assert.That(copiedBody.detectCollisions, Is.False);
        }

        private GameObject CreateModel(Vector3 size, Vector3 localCenter)
        {
            GameObject root = new GameObject("Model Prefab");
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Mesh";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = localCenter;
            mesh.transform.localScale = size;

            _createdObjects.Add(root);
            return root;
        }

        private InventoryItemView3D CreateView()
        {
            GameObject root = new GameObject("Inventory Item View");
            _createdObjects.Add(root);
            return root.AddComponent<InventoryItemView3D>();
        }

        private static InventoryModelViewDefinition CreateDefinition(
            GameObject model,
            Quaternion? baseRotation = null,
            float fitPaddingRatio = 0f,
            float maxDepthInCells = 10f)
        {
            return new InventoryModelViewDefinition(
                model,
                baseRotation ?? Quaternion.identity,
                Vector3.zero,
                scaleMultiplier: 1f,
                fitPaddingRatio: fitPaddingRatio,
                maxDepthInCells: maxDepthInCells);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
