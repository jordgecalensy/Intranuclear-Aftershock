using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Failsafe.Inventory.Presentation
{
    [DisallowMultipleComponent]
    public sealed class InventoryPrototypeGridVisual3D : MonoBehaviour
    {
        private const float BackgroundThicknessInCells = 0.025f;
        private const float BackgroundDepthOffsetInCells = -0.75f;
        private const float LineDepthOffsetInCells = -0.70f;
        private const float InternalLineWidthInCells = 0.018f;
        private const float BorderLineWidthInCells = 0.045f;

        private static readonly Color BackgroundColor =
            new Color(0.018f, 0.02f, 0.022f, 1f);

        private static readonly Color LineColor =
            new Color(1f, 0.28f, 0f, 1f);

        public bool IsInitialized { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int VerticalLineCount { get; private set; }
        public int HorizontalLineCount { get; private set; }
        public int LineCount => VerticalLineCount + HorizontalLineCount;

        private readonly List<GameObject> _geometry = new List<GameObject>();
        private Material _backgroundMaterial;
        private Material _lineMaterial;

        public void Initialize(InventoryGridSpace3D gridSpace)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException(
                    "Prototype inventory grid visual is already initialized.");
            }

            Shader shader = FindUnlitShader();

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Prototype inventory grid visual could not find a supported unlit shader.");
            }

            Columns = gridSpace.Columns;
            Rows = gridSpace.Rows;
            _backgroundMaterial = CreateMaterial(
                shader,
                "Inventory Prototype Background (Runtime)",
                BackgroundColor);

            _lineMaterial = CreateMaterial(
                shader,
                "Inventory Prototype Lines (Runtime)",
                LineColor);

            try
            {
                CreateBackground(gridSpace);
                CreateGridLines(gridSpace);
                IsInitialized = true;
            }
            catch
            {
                DestroyGeometry();
                DestroyMaterial(_backgroundMaterial);
                DestroyMaterial(_lineMaterial);
                _backgroundMaterial = null;
                _lineMaterial = null;
                throw;
            }
        }

        private void CreateBackground(InventoryGridSpace3D gridSpace)
        {
            GameObject background = CreateCube(
                "Prototype Grid Background",
                _backgroundMaterial);

            background.transform.localPosition = Vector3.up *
                (gridSpace.CellSize * BackgroundDepthOffsetInCells);

            background.transform.localScale = new Vector3(
                gridSpace.Columns * gridSpace.CellSize,
                gridSpace.CellSize * BackgroundThicknessInCells,
                gridSpace.Rows * gridSpace.CellSize);
        }

        private void CreateGridLines(InventoryGridSpace3D gridSpace)
        {
            float width = gridSpace.Columns * gridSpace.CellSize;
            float height = gridSpace.Rows * gridSpace.CellSize;
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float lineDepth = gridSpace.CellSize * LineDepthOffsetInCells;

            for (int column = 0; column <= gridSpace.Columns; column++)
            {
                bool isBorder = column == 0 || column == gridSpace.Columns;
                float lineWidth = gridSpace.CellSize *
                    (isBorder
                        ? BorderLineWidthInCells
                        : InternalLineWidthInCells);

                GameObject line = CreateCube(
                    $"Prototype Vertical Line [{column}]",
                    _lineMaterial);

                line.transform.localPosition = new Vector3(
                    -halfWidth + column * gridSpace.CellSize,
                    lineDepth,
                    0f);

                line.transform.localScale = new Vector3(
                    lineWidth,
                    gridSpace.CellSize * BackgroundThicknessInCells,
                    height + lineWidth);

                VerticalLineCount++;
            }

            for (int row = 0; row <= gridSpace.Rows; row++)
            {
                bool isBorder = row == 0 || row == gridSpace.Rows;
                float lineWidth = gridSpace.CellSize *
                    (isBorder
                        ? BorderLineWidthInCells
                        : InternalLineWidthInCells);

                GameObject line = CreateCube(
                    $"Prototype Horizontal Line [{row}]",
                    _lineMaterial);

                line.transform.localPosition = new Vector3(
                    0f,
                    lineDepth,
                    halfHeight - row * gridSpace.CellSize);

                line.transform.localScale = new Vector3(
                    width + lineWidth,
                    gridSpace.CellSize * BackgroundThicknessInCells,
                    lineWidth);

                HorizontalLineCount++;
            }
        }

        private GameObject CreateCube(string objectName, Material material)
        {
            GameObject geometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
            geometry.name = objectName;
            geometry.layer = gameObject.layer;
            geometry.transform.SetParent(transform, false);

            if (geometry.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;

                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }

            MeshRenderer renderer = geometry.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _geometry.Add(geometry);
            return geometry;
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit") ??
                   Shader.Find("Unlit/Color") ??
                   Shader.Find("Sprites/Default") ??
                   Shader.Find("Hidden/Internal-Colored");
        }

        private static Material CreateMaterial(
            Shader shader,
            string materialName,
            Color color)
        {
            Material material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            return material;
        }

        private void DestroyGeometry()
        {
            foreach (GameObject geometry in _geometry)
            {
                if (geometry == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(geometry);
                else
                    DestroyImmediate(geometry);
            }

            _geometry.Clear();
            VerticalLineCount = 0;
            HorizontalLineCount = 0;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        private void OnDestroy()
        {
            DestroyMaterial(_backgroundMaterial);
            DestroyMaterial(_lineMaterial);
            _backgroundMaterial = null;
            _lineMaterial = null;
        }
    }
}
