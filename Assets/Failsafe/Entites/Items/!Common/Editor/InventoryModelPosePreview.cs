using System;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal readonly struct InventoryModelPosePreviewSettings : IEquatable<InventoryModelPosePreviewSettings>
{
    public GameObject ModelPrefab { get; }
    public Vector3 BaseEulerAngles { get; }
    public Vector3 OffsetInCells { get; }
    public float ScaleMultiplier { get; }
    public float FitPadding { get; }
    public float MaxDepthInCells { get; }
    public int Width { get; }
    public int Height { get; }

    public InventoryModelPosePreviewSettings(
        GameObject modelPrefab,
        Vector3 baseEulerAngles,
        Vector3 offsetInCells,
        float scaleMultiplier,
        float fitPadding,
        float maxDepthInCells,
        int width,
        int height)
    {
        ModelPrefab = modelPrefab;
        BaseEulerAngles = baseEulerAngles;
        OffsetInCells = offsetInCells;
        ScaleMultiplier = scaleMultiplier;
        FitPadding = fitPadding;
        MaxDepthInCells = maxDepthInCells;
        Width = width;
        Height = height;
    }

    public bool TryValidate(out string error)
    {
        if (ModelPrefab == null)
        {
            error = "Assign an Inventory Model Prefab to use the pose preview.";
            return false;
        }

        if (ScaleMultiplier <= 0f)
        {
            error = "Inventory model scale multiplier must be greater than zero.";
            return false;
        }

        if (FitPadding < 0f || FitPadding >= 0.5f)
        {
            error = "Inventory model fit padding must be at least zero and less than 0.5.";
            return false;
        }

        if (MaxDepthInCells <= 0f)
        {
            error = "Inventory model maximum depth must be greater than zero.";
            return false;
        }

        if (Width <= 0 || Height <= 0)
        {
            error = "Inventory footprint dimensions must be greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    public bool Equals(InventoryModelPosePreviewSettings other)
    {
        return ModelPrefab == other.ModelPrefab &&
               BaseEulerAngles.Equals(other.BaseEulerAngles) &&
               OffsetInCells.Equals(other.OffsetInCells) &&
               ScaleMultiplier.Equals(other.ScaleMultiplier) &&
               FitPadding.Equals(other.FitPadding) &&
               MaxDepthInCells.Equals(other.MaxDepthInCells) &&
               Width == other.Width &&
               Height == other.Height;
    }

    public override bool Equals(object obj)
    {
        return obj is InventoryModelPosePreviewSettings other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ModelPrefab != null ? ModelPrefab.GetInstanceID() : 0;
            hash = (hash * 397) ^ BaseEulerAngles.GetHashCode();
            hash = (hash * 397) ^ OffsetInCells.GetHashCode();
            hash = (hash * 397) ^ ScaleMultiplier.GetHashCode();
            hash = (hash * 397) ^ FitPadding.GetHashCode();
            hash = (hash * 397) ^ MaxDepthInCells.GetHashCode();
            hash = (hash * 397) ^ Width;
            hash = (hash * 397) ^ Height;
            return hash;
        }
    }
}

internal sealed class InventoryModelPosePreview : IDisposable
{
    private const float PreviewMargin = 1.18f;
    private const float CameraDistance = 10f;

    private static readonly Color BackgroundColor =
        new Color(0.018f, 0.02f, 0.022f, 1f);

    private static readonly Color GridColor =
        new Color(1f, 0.28f, 0f, 0.9f);

    private PreviewRenderUtility _previewUtility;
    private GameObject _previewRoot;
    private InventoryModelPosePreviewSettings _currentSettings;
    private bool _hasCurrentSettings;

    public void Draw(
        Rect previewRect,
        InventoryModelPosePreviewSettings settings)
    {
        EditorGUI.DrawRect(previewRect, BackgroundColor);

        if (!settings.TryValidate(out string validationError))
        {
            EditorGUI.HelpBox(previewRect, validationError, MessageType.Info);
            return;
        }

        try
        {
            EnsurePreview(settings);
        }
        catch (Exception exception)
        {
            EditorGUI.HelpBox(
                previewRect,
                $"Could not build inventory model preview:\n{exception.Message}",
                MessageType.Error);

            return;
        }

        if (Event.current.type != EventType.Repaint)
            return;

        ConfigureCamera(previewRect, settings);

        _previewUtility.BeginPreview(previewRect, GUIStyle.none);
        _previewUtility.camera.Render();
        Texture previewTexture = _previewUtility.EndPreview();

        GUI.DrawTexture(
            previewRect,
            previewTexture,
            ScaleMode.StretchToFill,
            false);

        DrawGridOverlay(previewRect, settings);
    }

    public void Dispose()
    {
        DestroyPreviewRoot();

        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }

        _hasCurrentSettings = false;
    }

    private void EnsurePreview(InventoryModelPosePreviewSettings settings)
    {
        EnsurePreviewUtility();

        if (_previewRoot != null &&
            _hasCurrentSettings &&
            _currentSettings.Equals(settings))
        {
            return;
        }

        RebuildPreview(settings);
    }

    private void EnsurePreviewUtility()
    {
        if (_previewUtility != null)
            return;

        _previewUtility = new PreviewRenderUtility();
        _previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
        _previewUtility.camera.backgroundColor = BackgroundColor;
        _previewUtility.camera.orthographic = true;
        _previewUtility.camera.nearClipPlane = 0.01f;
        _previewUtility.camera.farClipPlane = CameraDistance * 2f;
        _previewUtility.ambientColor = new Color(0.42f, 0.42f, 0.42f, 1f);

        _previewUtility.lights[0].intensity = 1.15f;
        _previewUtility.lights[0].transform.rotation =
            Quaternion.Euler(35f, 35f, 0f);

        _previewUtility.lights[1].intensity = 0.65f;
        _previewUtility.lights[1].transform.rotation =
            Quaternion.Euler(340f, 210f, 0f);
    }

    private void RebuildPreview(InventoryModelPosePreviewSettings settings)
    {
        DestroyPreviewRoot();

        _previewRoot = new GameObject("Inventory Model Pose Preview")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        _previewUtility.AddSingleGO(_previewRoot);

        try
        {
            InventoryGridSize footprint = new InventoryGridSize(
                settings.Width,
                settings.Height);

            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(
                settings.Width,
                settings.Height,
                1f);

            InventoryModelViewDefinition definition =
                new InventoryModelViewDefinition(
                    settings.ModelPrefab,
                    Quaternion.Euler(settings.BaseEulerAngles),
                    settings.OffsetInCells,
                    settings.ScaleMultiplier,
                    settings.FitPadding,
                    settings.MaxDepthInCells);

            InventoryItemView3D view =
                _previewRoot.AddComponent<InventoryItemView3D>();

            view.Initialize(definition, footprint, gridSpace.CellSize);
            view.ApplyPlacement(
                new InventoryGridPosition(0, 0),
                footprint,
                InventoryItemRotation.Default,
                gridSpace);

            _currentSettings = settings;
            _hasCurrentSettings = true;
        }
        catch
        {
            DestroyPreviewRoot();
            throw;
        }
    }

    private void ConfigureCamera(
        Rect previewRect,
        InventoryModelPosePreviewSettings settings)
    {
        float aspect = Mathf.Max(previewRect.width / previewRect.height, 0.01f);
        float halfWidth = settings.Width * 0.5f + Mathf.Abs(settings.OffsetInCells.x);
        float halfHeight = settings.Height * 0.5f + Mathf.Abs(settings.OffsetInCells.z);
        float scaleMargin = Mathf.Max(1f, settings.ScaleMultiplier);

        _previewUtility.camera.aspect = aspect;
        _previewUtility.camera.orthographicSize = Mathf.Max(
            0.5f,
            Mathf.Max(halfHeight, halfWidth / aspect) *
            PreviewMargin *
            scaleMargin);

        _previewUtility.camera.transform.position =
            Vector3.up * CameraDistance;

        _previewUtility.camera.transform.rotation =
            Quaternion.LookRotation(Vector3.down, Vector3.forward);
    }

    private void DrawGridOverlay(
        Rect previewRect,
        InventoryModelPosePreviewSettings settings)
    {
        float orthographicSize = _previewUtility.camera.orthographicSize;
        float pixelsPerUnit = previewRect.height / (orthographicSize * 2f);
        float halfWidth = settings.Width * 0.5f;
        float halfHeight = settings.Height * 0.5f;

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        Handles.color = GridColor;

        for (int column = 0; column <= settings.Width; column++)
        {
            float x = -halfWidth + column;
            float lineWidth = column == 0 || column == settings.Width
                ? 2.5f
                : 1f;

            Handles.DrawAAPolyLine(
                lineWidth,
                ToGuiPoint(previewRect, x, halfHeight, pixelsPerUnit),
                ToGuiPoint(previewRect, x, -halfHeight, pixelsPerUnit));
        }

        for (int row = 0; row <= settings.Height; row++)
        {
            float z = halfHeight - row;
            float lineWidth = row == 0 || row == settings.Height
                ? 2.5f
                : 1f;

            Handles.DrawAAPolyLine(
                lineWidth,
                ToGuiPoint(previewRect, -halfWidth, z, pixelsPerUnit),
                ToGuiPoint(previewRect, halfWidth, z, pixelsPerUnit));
        }

        Handles.color = previousColor;
        Handles.EndGUI();
    }

    private static Vector3 ToGuiPoint(
        Rect previewRect,
        float worldX,
        float worldZ,
        float pixelsPerUnit)
    {
        return new Vector3(
            previewRect.center.x + worldX * pixelsPerUnit,
            previewRect.center.y - worldZ * pixelsPerUnit,
            0f);
    }

    private void DestroyPreviewRoot()
    {
        if (_previewRoot == null)
            return;

        Object.DestroyImmediate(_previewRoot);
        _previewRoot = null;
        _hasCurrentSettings = false;
    }
}
