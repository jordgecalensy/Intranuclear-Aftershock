using System;
using Failsafe.Inventory.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Failsafe.Chests
{
    [DisallowMultipleComponent]
    public sealed class ChestInventoryPresenter3D : MonoBehaviour, IDisposable
    {
        [Header("References")]
        [SerializeField] private ChestInventoryController _inventory;
        [SerializeField] private InventoryRobotPresentationLayout3D _layout;
        [SerializeField] private GameObject _visualRoot;

        [Header("3D Item Presentation")]
        [SerializeField, Min(0.001f)] private float _cellSize = 0.2f;
        [SerializeField] private string _inventoryLayerName = "Inventory";

        private GameObject _runtimeRoot;
        private RectTransform _fittedGridFrame;
        private Vector2 _originalFrameSizeDelta;
        private Vector2 _originalFramePosition;

        public ChestInventoryController Inventory => _inventory;
        public InventoryGridPresenter3D Presenter { get; private set; }
        public bool IsInitialized => Presenter != null && Presenter.IsInitialized;
        public bool IsVisible =>
            _visualRoot != null && _visualRoot.activeSelf;

        private void Awake()
        {
            ResolveReferences();

            if (!IsVisualRootSafe() && _visualRoot != null)
            {
                Debug.LogError(
                    "Chest inventory visual root cannot contain the chest " +
                    "inventory controller itself.",
                    this);
            }
        }

        public bool TryInitialize(out string error)
        {
            ResolveReferences();

            if (IsInitialized)
            {
                error = null;
                return true;
            }

            if (_inventory == null)
            {
                error = "Chest inventory controller is not assigned.";
                return false;
            }

            if (_layout == null)
            {
                error = "Chest inventory layout is not assigned.";
                return false;
            }

            if (_visualRoot == null)
            {
                error = "Chest inventory visual root is not assigned.";
                return false;
            }

            if (!IsVisualRootSafe())
            {
                error =
                    "Chest inventory visual root must be a child UI object and " +
                    "cannot contain the chest inventory controller.";
                return false;
            }

            if (_cellSize <= 0f)
            {
                error = "Chest inventory cell size must be greater than zero.";
                return false;
            }

            int inventoryLayer = LayerMask.NameToLayer(_inventoryLayerName);

            if (inventoryLayer < 0)
            {
                error =
                    $"Unity layer '{_inventoryLayerName}' does not exist.";
                return false;
            }

            if (!TryPrepareGridLayout(out error))
            {
                return false;
            }

            _runtimeRoot = new GameObject("Chest Inventory 3D Views");
            _runtimeRoot.layer = inventoryLayer;
            _runtimeRoot.transform.SetParent(transform, false);
            Presenter = _runtimeRoot.AddComponent<InventoryGridPresenter3D>();

            try
            {
                Presenter.Initialize(
                    _inventory.Grid,
                    new InventoryGridSpace3D(
                        _inventory.Columns,
                        _inventory.Rows,
                        _cellSize),
                    _inventory.ViewResolver);

                if (!_layout.TryApplyGridPose(
                        _runtimeRoot.transform,
                        _inventory.Columns,
                        _inventory.Rows,
                        _cellSize,
                        out error))
                {
                    Dispose();
                    return false;
                }

                Presenter.SetManualGridLayout(_layout);
                Presenter.SetPrototypeGridVisible(false);
                SetVisible(false);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                error =
                    $"Chest inventory presentation could not initialize: " +
                    $"{exception.Message}";
                return false;
            }
        }

        public bool SetVisible(bool visible)
        {
            ResolveReferences();

            if (!IsVisualRootSafe() ||
                (visible && !IsInitialized))
            {
                return false;
            }

            if (!visible)
                Presenter?.ClearSelectedItem();

            if (_visualRoot != null &&
                _visualRoot.activeSelf != visible)
            {
                _visualRoot.SetActive(visible);
            }

            if (_runtimeRoot != null &&
                _runtimeRoot.activeSelf != visible)
            {
                _runtimeRoot.SetActive(visible);
            }

            return _visualRoot != null;
        }

        public void Dispose()
        {
            Presenter?.SetManualGridLayout(null);
            Presenter?.Dispose();

            if (_runtimeRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeRoot);
                else
                    DestroyImmediate(_runtimeRoot);
            }

            Presenter = null;
            _runtimeRoot = null;
            RestoreGridFrame();

            if (_visualRoot != null)
                _visualRoot.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (_inventory == null)
                _inventory = GetComponentInParent<ChestInventoryController>();

            if (_layout == null)
            {
                _layout = GetComponentInChildren<
                    InventoryRobotPresentationLayout3D>(true);
            }

            if (_visualRoot == null && _layout != null)
                _visualRoot = _layout.gameObject;
        }

        private bool TryPrepareGridLayout(out string error)
        {
            bool restoreHiddenState = !_visualRoot.activeSelf;

            if (restoreHiddenState)
                _visualRoot.SetActive(true);

            try
            {
                if (!_visualRoot.activeInHierarchy)
                {
                    error =
                        "Chest inventory visual root must be active in the " +
                        "hierarchy while its layout is initialized.";
                    return false;
                }

                RestoreGridFrame();
                Canvas.ForceUpdateCanvases();

                if (!TrySynchronizeGridCells(out error))
                    return false;

                FitGridFrame();

                if (_layout.GridCellsRoot != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        _layout.GridCellsRoot);
                }

                Canvas.ForceUpdateCanvases();
                return _layout.TryValidateGrid(
                    _inventory.Columns,
                    _inventory.Rows,
                    out error);
            }
            finally
            {
                if (restoreHiddenState && _visualRoot != null)
                    _visualRoot.SetActive(false);
            }
        }

        private bool TrySynchronizeGridCells(out string error)
        {
            RectTransform root = _layout.GridCellsRoot;
            GridLayoutGroup grid = root != null ? root.GetComponent<GridLayoutGroup>() : null;
            if (root == null || grid == null || root.childCount == 0)
            {
                error = "Chest grid requires a GridLayoutGroup and at least one template cell.";
                return false;
            }

            int columns = _inventory.Columns;
            int rows = _inventory.Rows;
            if (columns <= 0 || rows <= 0 || (long)columns * rows > int.MaxValue)
            {
                error = "Chest grid dimensions are invalid.";
                return false;
            }

            float width = root.rect.width - grid.padding.horizontal;
            float height = root.rect.height - grid.padding.vertical;
            float size = Mathf.Min(width / columns, height / rows);
            if (size <= Mathf.Epsilon || float.IsNaN(size) || float.IsInfinity(size))
            {
                error = "Chest grid panel must have a non-zero available size.";
                return false;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                if (!(root.GetChild(index) is RectTransform))
                {
                    error = "Chest grid cells must use RectTransform.";
                    return false;
                }
            }

            int requiredCount = columns * rows;
            RectTransform template = (RectTransform)root.GetChild(0);
            while (root.childCount < requiredCount)
            {
                RectTransform cell = Instantiate(template, root, false);
                cell.name = $"Cell {root.childCount - 1}";
            }

            for (int index = 0; index < root.childCount; index++)
            {
                GameObject cell = root.GetChild(index).gameObject;
                bool visible = index < requiredCount;
                if (visible && cell.TryGetComponent(out LayoutElement element))
                    element.ignoreLayout = false;
                cell.SetActive(visible);
            }

            // GridSpace3D uses a contiguous, row-major grid of square cells.
            grid.enabled = true;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.spacing = Vector2.zero;
            grid.cellSize = new Vector2(size, size);
            error = null;
            return true;
        }

        private void FitGridFrame()
        {
            RectTransform cells = _layout.GridCellsRoot;
            RectTransform panel = cells.parent as RectTransform;
            RectTransform frame = panel != null ? panel.parent as RectTransform : null;
            // The existing chest UI nests stretched cells inside its background and frame.
            // Other layouts may have no decorative frame and need no resizing.
            if (panel == null || frame == null || panel.name != "GridPanel" ||
                frame.name != "GridFrame" || !IsStretched(cells) || !IsStretched(panel))
                return;

            GridLayoutGroup grid = cells.GetComponent<GridLayoutGroup>();
            Vector2 occupiedSize = new Vector2(
                _inventory.Columns * grid.cellSize.x + grid.padding.horizontal,
                _inventory.Rows * grid.cellSize.y + grid.padding.vertical);
            Vector2 sizeChange = occupiedSize - cells.rect.size;
            _fittedGridFrame = frame;
            _originalFrameSizeDelta = frame.sizeDelta;
            _originalFramePosition = frame.anchoredPosition;
            frame.sizeDelta += sizeChange;
            // Preserve the grid center even when the frame pivot is not centered.
            frame.anchoredPosition += Vector2.Scale(sizeChange, frame.pivot - Vector2.one * 0.5f);
            frame.ForceUpdateRectTransforms();
            LayoutRebuilder.ForceRebuildLayoutImmediate(frame);
        }

        private static bool IsStretched(RectTransform rect)
        {
            return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                   rect.localScale == Vector3.one && rect.localRotation == Quaternion.identity;
        }

        private void RestoreGridFrame()
        {
            if (_fittedGridFrame == null)
                return;

            _fittedGridFrame.sizeDelta = _originalFrameSizeDelta;
            _fittedGridFrame.anchoredPosition = _originalFramePosition;
            _fittedGridFrame.ForceUpdateRectTransforms();
            _fittedGridFrame = null;
        }

        private bool IsVisualRootSafe()
        {
            return _visualRoot != null &&
                   (_inventory == null ||
                    (_inventory.transform != _visualRoot.transform &&
                     !_inventory.transform.IsChildOf(
                         _visualRoot.transform)));
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
