using System;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    internal sealed class InventoryQuickBarPreviewStage3D : IDisposable
    {
        private const float CameraDistance = 3f;
        private const float CameraNearPlane = 0.01f;
        private const float CameraFarPlane = 6f;
        private const float StageSeparation = 16f;
        private const float StageOrigin = 10000f;
        public int SlotCount { get; }
        public int ItemLayer { get; }
        public int TextureHeight { get; }
        public float SlotPaddingRatio { get; }
        public RenderTexture Texture { get; private set; }

        private readonly float _slotWorldSize;
        private readonly float _slotStride;
        private GameObject _root;
        private Transform _itemsRoot;
        private Camera _camera;

        public InventoryQuickBarPreviewStage3D(
            int ownerInstanceId,
            int slotCount,
            int itemLayer,
            int textureHeight,
            float slotPaddingRatio)
        {
            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            if (itemLayer < 0 || itemLayer > 31)
                throw new ArgumentOutOfRangeException(nameof(itemLayer));

            if (textureHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(textureHeight));

            if (slotPaddingRatio < 0f)
                throw new ArgumentOutOfRangeException(nameof(slotPaddingRatio));

            SlotCount = slotCount;
            ItemLayer = itemLayer;
            TextureHeight = textureHeight;
            SlotPaddingRatio = slotPaddingRatio;
            _slotWorldSize = 1f;
            _slotStride = _slotWorldSize * (1f + slotPaddingRatio * 2f);

            CreateStage(ownerInstanceId);
        }

        public bool Matches(
            int slotCount,
            int itemLayer,
            int textureHeight,
            float slotPaddingRatio)
        {
            return SlotCount == slotCount &&
                   ItemLayer == itemLayer &&
                   TextureHeight == textureHeight &&
                   Mathf.Approximately(
                       SlotPaddingRatio,
                       slotPaddingRatio);
        }

        public void AttachPresenterRoot(Transform presenterRoot)
        {
            if (presenterRoot == null)
                throw new ArgumentNullException(nameof(presenterRoot));

            presenterRoot.SetParent(_itemsRoot, false);
            presenterRoot.localPosition = Vector3.zero;
            presenterRoot.localRotation = Quaternion.identity;
            presenterRoot.localScale = Vector3.one;
        }

        public bool TryApplySlotPose(
            int slotIndex,
            Transform target,
            float sourceCellSize,
            float modelDepthOffset,
            out string error)
        {
            if (target == null)
            {
                error = "Quick-slot presentation target is null.";
                return false;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                error = $"Quick-slot index {slotIndex} is outside the preview stage.";
                return false;
            }

            if (sourceCellSize <= 0f)
            {
                error = "Source quick-slot cell size must be greater than zero.";
                return false;
            }

            float centerIndex = (SlotCount - 1) * 0.5f;
            float x = (slotIndex - centerIndex) * _slotStride;

            target.localPosition = new Vector3(
                x,
                modelDepthOffset,
                0f);

            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one *
                                (_slotWorldSize / sourceCellSize);

            error = null;
            return true;
        }

        public Rect GetSlotUvRect(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            float width = 1f / SlotCount;
            return new Rect(slotIndex * width, 0f, width, 1f);
        }

        public void SetVisible(bool visible)
        {
            if (_root == null)
                return;

            if (visible)
            {
                _root.SetActive(true);

                if (_camera != null)
                    _camera.enabled = true;

                return;
            }

            if (_camera != null)
                _camera.enabled = false;

            _root.SetActive(false);
        }

        public void Dispose()
        {
            if (_camera != null)
            {
                _camera.enabled = false;
                _camera.targetTexture = null;
            }

            if (Texture != null)
            {
                Texture.Release();
                DestroyUnityObject(Texture);
                Texture = null;
            }

            if (_root != null)
                DestroyUnityObject(_root);

            _camera = null;
            _itemsRoot = null;
            _root = null;
        }

        private void CreateStage(int ownerInstanceId)
        {
            _root = new GameObject(
                $"Quick Bar Preview Stage [{ownerInstanceId}]");

            _root.hideFlags = HideFlags.DontSave;
            _root.layer = ItemLayer;
            _root.transform.position = GetIsolatedStagePosition(
                ownerInstanceId);

            GameObject itemsObject = new GameObject("Items");
            itemsObject.hideFlags = HideFlags.DontSave;
            itemsObject.layer = ItemLayer;
            itemsObject.transform.SetParent(_root.transform, false);
            _itemsRoot = itemsObject.transform;

            Texture = CreateColorRenderTexture();
            _camera = CreateCamera(Texture);
            _root.SetActive(false);
        }

        private RenderTexture CreateColorRenderTexture()
        {
            int textureWidth = TextureHeight * SlotCount;

            RenderTexture texture = new RenderTexture(
                textureWidth,
                TextureHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = "Inventory Quick Bar Preview Atlas (Runtime)",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };

            texture.Create();
            return texture;
        }

        private Camera CreateCamera(RenderTexture targetTexture)
        {
            GameObject cameraObject = new GameObject("Preview Camera");
            cameraObject.hideFlags = HideFlags.DontSave;
            cameraObject.layer = ItemLayer;
            cameraObject.transform.SetParent(_root.transform, false);
            cameraObject.transform.localPosition =
                new Vector3(0f, CameraDistance, 0f);
            cameraObject.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);

            Camera previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.black;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = _slotStride * 0.5f;
            previewCamera.aspect = SlotCount;
            previewCamera.nearClipPlane = CameraNearPlane;
            previewCamera.farClipPlane = CameraFarPlane;
            previewCamera.cullingMask = 1 << ItemLayer;
            previewCamera.useOcclusionCulling = false;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.depth = -100f;
            previewCamera.targetTexture = targetTexture;

            return previewCamera;
        }

        private static Vector3 GetIsolatedStagePosition(int ownerInstanceId)
        {
            int positiveId = ownerInstanceId == int.MinValue
                ? int.MaxValue
                : Mathf.Abs(ownerInstanceId);

            int xIndex = positiveId % 512;
            int zIndex = positiveId / 512 % 512;

            return new Vector3(
                StageOrigin + xIndex * StageSeparation,
                StageOrigin,
                StageOrigin + zIndex * StageSeparation);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
