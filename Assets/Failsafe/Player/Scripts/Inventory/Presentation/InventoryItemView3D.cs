using System;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    public sealed class InventoryItemView3D : MonoBehaviour
    {
        private const float MinimumBoundsSize = 0.00001f;

        public bool IsInitialized { get; private set; }
        public GameObject ModelInstance { get; private set; }
        public Bounds UnscaledModelBounds { get; private set; }
        public float AppliedScale { get; private set; }

        public Vector3 CenterOffset =>
            _centerRoot != null ? _centerRoot.localPosition : Vector3.zero;

        public Quaternion AppliedBaseRotation =>
            _basePoseRoot != null ? _basePoseRoot.localRotation : Quaternion.identity;

        public Quaternion AppliedGridRotation =>
            _gridRotationRoot != null ? _gridRotationRoot.localRotation : Quaternion.identity;

        private Transform _gridRotationRoot;
        private Transform _offsetRoot;
        private Transform _scaleRoot;
        private Transform _centerRoot;
        private Transform _basePoseRoot;

        public void Initialize(
            InventoryModelViewDefinition definition,
            InventoryGridSize baseFootprint,
            float cellSize)
        {
            if (IsInitialized)
                throw new InvalidOperationException("Inventory item view is already initialized.");

            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            if (cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            CreateHierarchy();

            _basePoseRoot.localRotation = definition.BaseRotation;
            _offsetRoot.localPosition = definition.OffsetInCells * cellSize;

            ModelInstance = Instantiate(
                definition.ModelPrefab,
                _basePoseRoot,
                false);

            ModelInstance.name = definition.ModelPrefab.name;
            SetLayerRecursively(ModelInstance.transform, gameObject.layer);
            DisablePhysicalComponents(ModelInstance);

            if (!TryCalculateLocalBounds(
                    _centerRoot,
                    ModelInstance,
                    out Bounds unscaledBounds))
            {
                throw new InvalidOperationException(
                    $"Inventory model '{definition.ModelPrefab.name}' has no supported renderers.");
            }

            UnscaledModelBounds = unscaledBounds;
            _centerRoot.localPosition = -unscaledBounds.center;

            AppliedScale = CalculateFitScale(
                unscaledBounds.size,
                baseFootprint,
                cellSize,
                definition);

            _scaleRoot.localScale = Vector3.one * AppliedScale;
            IsInitialized = true;
        }

        public void ApplyPlacement(
            InventoryGridPosition origin,
            InventoryGridSize occupiedFootprint,
            InventoryItemRotation rotation,
            InventoryGridSpace3D gridSpace)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Inventory item view is not initialized.");

            transform.localPosition = gridSpace.GetPlacementCenter(
                origin,
                occupiedFootprint);

            _gridRotationRoot.localRotation = gridSpace.GetGridRotation(rotation);
        }

        public void ApplyFreePreview(
            Vector3 localPosition,
            InventoryItemRotation rotation,
            InventoryGridSpace3D gridSpace)
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Inventory item view is not initialized.");

            transform.localPosition = localPosition;
            _gridRotationRoot.localRotation = gridSpace.GetGridRotation(rotation);
        }

        private void CreateHierarchy()
        {
            _gridRotationRoot = CreateChild("Grid Rotation", transform);
            _offsetRoot = CreateChild("Artistic Offset", _gridRotationRoot);
            _scaleRoot = CreateChild("Automatic Scale", _offsetRoot);
            _centerRoot = CreateChild("Automatic Center", _scaleRoot);
            _basePoseRoot = CreateChild("Canonical Pose", _centerRoot);
        }

        private static Transform CreateChild(string childName, Transform parent)
        {
            GameObject child = new GameObject(childName);
            child.layer = parent.gameObject.layer;

            Transform childTransform = child.transform;
            childTransform.SetParent(parent, false);

            return childTransform;
        }

        private static float CalculateFitScale(
            Vector3 modelSize,
            InventoryGridSize footprint,
            float cellSize,
            InventoryModelViewDefinition definition)
        {
            float usableRatio = 1f - definition.FitPaddingRatio * 2f;

            float availableWidth = footprint.Width * cellSize * usableRatio;
            float availableHeight = footprint.Height * cellSize * usableRatio;
            float availableDepth = definition.MaxDepthInCells * cellSize * usableRatio;

            float widthScale = GetAxisFitScale(availableWidth, modelSize.x);
            float heightScale = GetAxisFitScale(availableHeight, modelSize.z);
            float depthScale = GetAxisFitScale(availableDepth, modelSize.y);

            float automaticScale = Mathf.Min(widthScale, heightScale, depthScale);

            if (float.IsInfinity(automaticScale) ||
                float.IsNaN(automaticScale) ||
                automaticScale <= 0f)
            {
                throw new InvalidOperationException(
                    "Inventory model bounds cannot be fitted into the configured footprint.");
            }

            return automaticScale * definition.ScaleMultiplier;
        }

        private static float GetAxisFitScale(float availableSize, float modelSize)
        {
            return modelSize <= MinimumBoundsSize
                ? float.PositiveInfinity
                : availableSize / modelSize;
        }

        private static bool TryCalculateLocalBounds(
            Transform relativeTo,
            GameObject model,
            out Bounds bounds)
        {
            bounds = default;
            bool hasPoint = false;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    EncapsulateTransformedBounds(
                        skinnedMeshRenderer.localBounds,
                        skinnedMeshRenderer.transform,
                        relativeTo,
                        ref bounds,
                        ref hasPoint);

                    continue;
                }

                if (renderer is MeshRenderer &&
                    renderer.TryGetComponent(out MeshFilter meshFilter) &&
                    meshFilter.sharedMesh != null)
                {
                    EncapsulateTransformedBounds(
                        meshFilter.sharedMesh.bounds,
                        meshFilter.transform,
                        relativeTo,
                        ref bounds,
                        ref hasPoint);

                    continue;
                }

                EncapsulateWorldBounds(
                    renderer.bounds,
                    relativeTo,
                    ref bounds,
                    ref hasPoint);
            }

            return hasPoint;
        }

        private static void EncapsulateTransformedBounds(
            Bounds sourceBounds,
            Transform sourceTransform,
            Transform relativeTo,
            ref Bounds result,
            ref bool hasPoint)
        {
            EncapsulateBoundsCorners(
                sourceBounds,
                point => relativeTo.InverseTransformPoint(
                    sourceTransform.TransformPoint(point)),
                ref result,
                ref hasPoint);
        }

        private static void EncapsulateWorldBounds(
            Bounds worldBounds,
            Transform relativeTo,
            ref Bounds result,
            ref bool hasPoint)
        {
            EncapsulateBoundsCorners(
                worldBounds,
                relativeTo.InverseTransformPoint,
                ref result,
                ref hasPoint);
        }

        private static void EncapsulateBoundsCorners(
            Bounds sourceBounds,
            Func<Vector3, Vector3> convertPoint,
            ref Bounds result,
            ref bool hasPoint)
        {
            Vector3 center = sourceBounds.center;
            Vector3 extents = sourceBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 sourcePoint = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));

                        Vector3 point = convertPoint(sourcePoint);

                        if (!hasPoint)
                        {
                            result = new Bounds(point, Vector3.zero);
                            hasPoint = true;
                        }
                        else
                        {
                            result.Encapsulate(point);
                        }
                    }
                }
            }
        }

        private static void DisablePhysicalComponents(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }
    }
}
