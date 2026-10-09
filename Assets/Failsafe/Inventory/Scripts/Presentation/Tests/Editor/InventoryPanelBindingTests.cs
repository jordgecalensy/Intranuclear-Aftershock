using NUnit.Framework;
using UnityEngine;

namespace Failsafe.Inventory.Presentation.Tests
{
    public sealed class InventoryPanelBindingTests
    {
        private GameObject _scene;
        private InventoryPanelBinding _binding;

        [SetUp]
        public void SetUp()
        {
            _scene = new GameObject("Test");
            _binding = _scene.AddComponent<InventoryPanelBinding>();
            _binding.PreviewInEditor = false;
            _binding.BottomLeft = Make("BL", new Vector3(2, 3, 4));
            _binding.BottomRight = Make("BR", new Vector3(6, 3, 4));
            _binding.TopLeft = Make("TL", new Vector3(2, 5, 4));
            var ui = new GameObject("UI", typeof(RectTransform));
            ui.transform.SetParent(_scene.transform, false);
            var rect = (RectTransform)ui.transform;
            rect.sizeDelta = new Vector2(200, 100);
            rect.pivot = new Vector2(0.2f, 0.8f);
            _binding.TargetRoot = rect;
            _binding.ReferenceRect = rect;
            _binding.SurfaceOffset = 0.01f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_scene);

        [Test]
        public void FitsArbitraryPivotAndFollowsMovingPanelWithoutAccumulatingScale()
        {
            Assert.That(_binding.TryApply(out string error), Is.True, error);
            var corners = new Vector3[4];
            _binding.ReferenceRect.GetWorldCorners(corners);
            Near(corners[0], new Vector3(2, 3, 3.99f));
            Near(corners[2], new Vector3(6, 5, 3.99f));
            Vector3 scale = _binding.TargetRoot.localScale;
            for (int i = 0; i < 50; i++) Assert.That(_binding.TryApply(out _), Is.True);
            Near(_binding.TargetRoot.localScale, scale);
            Quaternion rotation = Quaternion.Euler(30, 50, 10);
            foreach (Transform marker in new[] { _binding.BottomLeft, _binding.BottomRight, _binding.TopLeft })
                marker.position = rotation * marker.position + Vector3.one;
            Assert.That(_binding.TryApply(out error), Is.True, error);
            _binding.ReferenceRect.GetWorldCorners(corners);
            Near(corners[0], rotation * new Vector3(2, 3, 3.99f) + Vector3.one);
        }

        [Test]
        public void PreservesSquareCellsAndMovesSeparateItemRootWithUI()
        {
            _binding.TopLeft.position = new Vector3(2, 7, 4);
            Transform items = Make("Items", _binding.ReferenceRect.TransformPoint(_binding.ReferenceRect.rect.center));
            _binding.AdditionalRoots = new[] { items };
            Assert.That(_binding.TryApply(out string error), Is.True, error);
            var corners = new Vector3[4];
            _binding.ReferenceRect.GetWorldCorners(corners);
            Near(corners[0], new Vector3(2, 4, 3.99f));
            Near(corners[2], new Vector3(6, 6, 3.99f));
            Near(items.position, (corners[0] + corners[2]) * 0.5f);
            Assert.That(_binding.TargetRoot.localScale.x,
                Is.EqualTo(_binding.TargetRoot.localScale.y).Within(1e-6f));
        }

        [Test]
        public void InvalidCornersAndFeedbackHierarchyDoNotChangeUI()
        {
            Vector3 before = _binding.TargetRoot.position;
            _binding.TopLeft.position = _binding.BottomLeft.position;
            Assert.That(_binding.TryApply(out _), Is.False);
            Near(_binding.TargetRoot.position, before);
            _binding.TopLeft.position = new Vector3(2, 5, 4);
            _binding.BottomLeft.SetParent(_binding.TargetRoot, true);
            Assert.That(_binding.TryApply(out _), Is.False);
            Near(_binding.TargetRoot.position, before);
        }

        private Transform Make(string name, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(_scene.transform);
            obj.transform.position = position;
            return obj.transform;
        }

        private static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
    }
}
