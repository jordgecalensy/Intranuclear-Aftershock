using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    [DisallowMultipleComponent]
    public sealed class InventoryRobotPresentationLayout3D : MonoBehaviour
    {
        [Header("Inventory Grid")]
        [SerializeField] private RectTransform _gridCellsRoot;
        [SerializeField] private Transform _inventoryItemsRoot;
        [SerializeField] private float _gridModelDepthOffset;

        [Header("Manual Cell Highlights")]
        [SerializeField] private string _validCellHighlightPath =
            "HighlightValid";
        [SerializeField] private string _invalidCellHighlightPath =
            "HighlightInvalid";

        [Header("Item Footprints")]
        [SerializeField] private RectTransform _itemFootprintVisualsRoot;
        [SerializeField] private RectTransform _itemFootprintVisualTemplate;
        [SerializeField] private string _itemSelectedStatePath =
            "Selected";

        [Header("Open Quick Slots")]
        [SerializeField] private RectTransform _quickSlotsRoot;
        [SerializeField] private Transform _quickSlotItemsRoot;
        [SerializeField] private float _quickSlotModelDepthOffset;
        [SerializeField] private string _quickSlotAssignedStatePath =
            "Assigned";

        [Header("Validation")]
        [SerializeField, Range(0f, 0.25f)]
        private float _maximumCellAspectError = 0.08f;

        public RectTransform GridCellsRoot => _gridCellsRoot;
        public Transform InventoryItemsRoot => _inventoryItemsRoot;
        public RectTransform QuickSlotsRoot => _quickSlotsRoot;
        public Transform QuickSlotItemsRoot => _quickSlotItemsRoot;
        public int ItemFootprintVisualCount => _itemFootprintVisuals.Count;

        private GameObject[] _validCellHighlights;
        private GameObject[] _invalidCellHighlights;
        private Transform[] _quickSlotAssignedStates;

        private readonly Dictionary<string, ItemFootprintVisual>
            _itemFootprintVisuals =
                new Dictionary<string, ItemFootprintVisual>(
                    StringComparer.Ordinal);

        public bool TryValidate(
            int columns,
            int rows,
            int quickSlotCount,
            out string error)
        {
            if (_gridCellsRoot == null)
            {
                error = "Inventory grid cells root is not assigned.";
                return false;
            }

            if (_inventoryItemsRoot == null)
            {
                error = "Inventory items root is not assigned.";
                return false;
            }

            if (_itemFootprintVisualsRoot == null)
            {
                error = "Item footprint visuals root is not assigned.";
                return false;
            }

            if (_itemFootprintVisualTemplate == null)
            {
                error = "Item footprint visual template is not assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_itemSelectedStatePath) ||
                _itemFootprintVisualTemplate.Find(
                    _itemSelectedStatePath) == null)
            {
                error = $"Item footprint visual template must contain " +
                        $"'{_itemSelectedStatePath}'.";
                return false;
            }

            if (_quickSlotsRoot == null)
            {
                error = "Quick-slots root is not assigned.";
                return false;
            }

            if (_quickSlotItemsRoot == null)
            {
                error = "Quick-slot items root is not assigned.";
                return false;
            }

            if (!TryGetGridPose(
                    columns,
                    rows,
                    out _,
                    out _,
                    out _,
                    out error))
            {
                return false;
            }

            if (_quickSlotsRoot.childCount < quickSlotCount)
            {
                error = $"Quick-slots root must have at least " +
                        $"{quickSlotCount} direct children, but it has " +
                        $"{_quickSlotsRoot.childCount}.";
                return false;
            }

            for (int index = 0; index < quickSlotCount; index++)
            {
                if (!TryGetRectPose(
                        _quickSlotsRoot.GetChild(index) as RectTransform,
                        out _,
                        out _,
                        out _,
                        out error))
                {
                    error = $"Quick slot {index + 1} is invalid: {error}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public bool TryApplyGridPose(
            Transform target,
            int columns,
            int rows,
            float sourceCellSize,
            out string error)
        {
            if (target == null)
            {
                error = "Grid presentation target is null.";
                return false;
            }

            if (sourceCellSize <= 0f)
            {
                error = "Source grid cell size must be greater than zero.";
                return false;
            }

            if (!TryGetGridPose(
                    columns,
                    rows,
                    out Vector3 worldCenter,
                    out Quaternion worldRotation,
                    out float worldCellSize,
                    out error))
            {
                return false;
            }

            Vector3 depthAxis = worldRotation * Vector3.up;
            worldCenter += depthAxis * _gridModelDepthOffset;

            target.SetParent(_inventoryItemsRoot, false);
            ApplyWorldPose(
                target,
                worldCenter,
                worldRotation,
                worldCellSize / sourceCellSize);

            error = null;
            return true;
        }

        public bool TryAttachQuickBarRoot(
            Transform quickBarRoot,
            int expectedSlotCount,
            out string error)
        {
            if (quickBarRoot == null)
            {
                error = "Quick-bar presentation root is null.";
                return false;
            }

            if (_quickSlotItemsRoot == null || _quickSlotsRoot == null)
            {
                error = "Quick-slot presentation roots are not assigned.";
                return false;
            }

            if (_quickSlotsRoot.childCount < expectedSlotCount)
            {
                error = $"Quick-slots root must have at least " +
                        $"{expectedSlotCount} direct children.";
                return false;
            }

            quickBarRoot.SetParent(_quickSlotItemsRoot, false);
            quickBarRoot.localPosition = Vector3.zero;
            quickBarRoot.localRotation = Quaternion.identity;
            quickBarRoot.localScale = Vector3.one;

            error = null;
            return true;
        }

        public bool TryApplyQuickSlotPose(
            int slotIndex,
            Transform target,
            float sourceCellSize,
            out string error)
        {
            if (target == null)
            {
                error = "Quick-slot presentation target is null.";
                return false;
            }

            if (sourceCellSize <= 0f)
            {
                error = "Source quick-slot cell size must be greater than zero.";
                return false;
            }

            if (_quickSlotsRoot == null ||
                slotIndex < 0 ||
                slotIndex >= _quickSlotsRoot.childCount)
            {
                error = $"Quick-slot index {slotIndex} is outside the layout.";
                return false;
            }

            if (!TryGetRectPose(
                    _quickSlotsRoot.GetChild(slotIndex) as RectTransform,
                    out Vector3 worldCenter,
                    out Quaternion worldRotation,
                    out float worldCellSize,
                    out error))
            {
                return false;
            }

            Vector3 depthAxis = worldRotation * Vector3.up;
            worldCenter += depthAxis * _quickSlotModelDepthOffset;

            ApplyWorldPose(
                target,
                worldCenter,
                worldRotation,
                worldCellSize / sourceCellSize);

            error = null;
            return true;
        }

        public bool TryShowGridCellHighlights(
            Inventory.Core.InventoryGridPosition origin,
            Inventory.Core.InventoryGridSize footprint,
            bool isValidPlacement,
            int columns,
            int rows,
            out string error)
        {
            if (!TryEnsureCellHighlightCache(columns, rows, out error))
                return false;

            HideGridCellHighlights();

            GameObject[] selectedHighlights = isValidPlacement
                ? _validCellHighlights
                : _invalidCellHighlights;

            for (int row = 0; row < footprint.Height; row++)
            {
                int gridRow = origin.Row + row;

                if (gridRow < 0 || gridRow >= rows)
                    continue;

                for (int column = 0; column < footprint.Width; column++)
                {
                    int gridColumn = origin.Column + column;

                    if (gridColumn < 0 || gridColumn >= columns)
                        continue;

                    int index = gridRow * columns + gridColumn;
                    selectedHighlights[index].SetActive(true);
                }
            }

            error = null;
            return true;
        }

        public void HideGridCellHighlights()
        {
            SetHighlightsActive(_validCellHighlights, false);
            SetHighlightsActive(_invalidCellHighlights, false);
        }

        public bool TryShowItemFootprint(
            string instanceId,
            InventoryGridPosition origin,
            InventoryGridSize footprint,
            int columns,
            int rows,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Item footprint instance ID is required.";
                return false;
            }

            if (!TryGetFootprintCells(
                    origin,
                    footprint,
                    columns,
                    rows,
                    out RectTransform firstCell,
                    out RectTransform lastCell,
                    out error))
            {
                return false;
            }

            if (!TryGetOrCreateItemFootprintVisual(
                    instanceId,
                    out ItemFootprintVisual visual,
                    out error))
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();

            Vector2 minimum = new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);

            Vector2 maximum = new Vector2(
                float.NegativeInfinity,
                float.NegativeInfinity);

            EncapsulateRectInRoot(
                firstCell,
                _itemFootprintVisualsRoot,
                ref minimum,
                ref maximum);

            EncapsulateRectInRoot(
                lastCell,
                _itemFootprintVisualsRoot,
                ref minimum,
                ref maximum);

            RectTransform visualRoot = visual.Root;
            visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
            visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
            visualRoot.pivot = new Vector2(0.5f, 0.5f);
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            visualRoot.anchoredPosition =
                (minimum + maximum) * 0.5f -
                _itemFootprintVisualsRoot.rect.center;

            visualRoot.sizeDelta = maximum - minimum;
            visualRoot.gameObject.SetActive(true);

            error = null;
            return true;
        }

        public bool TryGetItemFootprintVisual(
            string instanceId,
            out RectTransform visualRoot)
        {
            visualRoot = null;

            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_itemFootprintVisuals.TryGetValue(
                    instanceId,
                    out ItemFootprintVisual visual) ||
                visual.Root == null)
            {
                return false;
            }

            visualRoot = visual.Root;
            return true;
        }

        public bool SetItemFootprintSelected(
            string instanceId,
            bool selected)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_itemFootprintVisuals.TryGetValue(
                    instanceId,
                    out ItemFootprintVisual visual) ||
                visual.SelectedState == null)
            {
                return false;
            }

            visual.SelectedState.gameObject.SetActive(selected);
            return true;
        }

        public bool SetItemFootprintVisible(
            string instanceId,
            bool visible)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_itemFootprintVisuals.TryGetValue(
                    instanceId,
                    out ItemFootprintVisual visual) ||
                visual.Root == null)
            {
                return false;
            }

            visual.Root.gameObject.SetActive(visible);
            return true;
        }

        public bool RemoveItemFootprint(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_itemFootprintVisuals.TryGetValue(
                    instanceId,
                    out ItemFootprintVisual visual))
            {
                return false;
            }

            _itemFootprintVisuals.Remove(instanceId);

            if (visual.Root != null)
                DestroyObject(visual.Root.gameObject);

            return true;
        }

        public void ClearItemFootprints()
        {
            foreach (ItemFootprintVisual visual in
                     _itemFootprintVisuals.Values)
            {
                if (visual.Root != null)
                    DestroyObject(visual.Root.gameObject);
            }

            _itemFootprintVisuals.Clear();
        }

        public void SetQuickSlotState(
            int slotIndex,
            bool isAssigned)
        {
            EnsureQuickSlotStateCache();

            if (_quickSlotsRoot == null ||
                slotIndex < 0 ||
                slotIndex >= _quickSlotsRoot.childCount)
            {
                return;
            }

            Transform assignedState =
                _quickSlotAssignedStates?[slotIndex];

            if (assignedState != null)
                assignedState.gameObject.SetActive(isAssigned);
        }

        private bool TryEnsureCellHighlightCache(
            int columns,
            int rows,
            out string error)
        {
            int requiredCellCount = columns * rows;

            if (_validCellHighlights != null &&
                _validCellHighlights.Length == requiredCellCount &&
                _invalidCellHighlights != null &&
                _invalidCellHighlights.Length == requiredCellCount)
            {
                error = null;
                return true;
            }

            if (_gridCellsRoot == null ||
                _gridCellsRoot.childCount < requiredCellCount)
            {
                error = "Inventory grid cells root is incomplete.";
                return false;
            }

            GameObject[] validHighlights =
                new GameObject[requiredCellCount];
            GameObject[] invalidHighlights =
                new GameObject[requiredCellCount];

            for (int index = 0; index < requiredCellCount; index++)
            {
                Transform cell = _gridCellsRoot.GetChild(index);
                Transform valid = cell.Find(_validCellHighlightPath);
                Transform invalid = cell.Find(_invalidCellHighlightPath);

                if (valid == null || invalid == null)
                {
                    error = $"Grid cell {index} ('{cell.name}') must contain " +
                            $"'{_validCellHighlightPath}' and " +
                            $"'{_invalidCellHighlightPath}'.";
                    return false;
                }

                validHighlights[index] = valid.gameObject;
                invalidHighlights[index] = invalid.gameObject;
            }

            _validCellHighlights = validHighlights;
            _invalidCellHighlights = invalidHighlights;
            HideGridCellHighlights();

            error = null;
            return true;
        }

        private bool TryGetOrCreateItemFootprintVisual(
            string instanceId,
            out ItemFootprintVisual visual,
            out string error)
        {
            if (_itemFootprintVisuals.TryGetValue(
                    instanceId,
                    out visual) &&
                visual.Root != null &&
                visual.SelectedState != null)
            {
                error = null;
                return true;
            }

            if (_itemFootprintVisualsRoot == null ||
                _itemFootprintVisualTemplate == null)
            {
                error = "Item footprint visual references are not assigned.";
                visual = default;
                return false;
            }

            RectTransform visualRoot = Instantiate(
                _itemFootprintVisualTemplate,
                _itemFootprintVisualsRoot,
                false);

            visualRoot.name = $"Item Footprint [{instanceId}]";

            Transform selectedState = string.IsNullOrWhiteSpace(
                    _itemSelectedStatePath)
                ? null
                : visualRoot.Find(_itemSelectedStatePath);

            if (selectedState == null)
            {
                DestroyObject(visualRoot.gameObject);
                error = $"Item footprint visual template must contain " +
                        $"'{_itemSelectedStatePath}'.";
                visual = default;
                return false;
            }

            selectedState.gameObject.SetActive(false);
            visual = new ItemFootprintVisual(
                visualRoot,
                selectedState);

            _itemFootprintVisuals[instanceId] = visual;
            error = null;
            return true;
        }

        private bool TryGetFootprintCells(
            InventoryGridPosition origin,
            InventoryGridSize footprint,
            int columns,
            int rows,
            out RectTransform firstCell,
            out RectTransform lastCell,
            out string error)
        {
            firstCell = null;
            lastCell = null;

            if (columns <= 0 || rows <= 0 ||
                origin.Column < 0 ||
                origin.Row < 0 ||
                origin.Column + footprint.Width > columns ||
                origin.Row + footprint.Height > rows)
            {
                error = "Item footprint must stay inside the inventory grid.";
                return false;
            }

            int requiredCellCount = columns * rows;

            if (_gridCellsRoot == null ||
                _gridCellsRoot.childCount < requiredCellCount)
            {
                error = "Inventory grid cells root is incomplete.";
                return false;
            }

            int firstIndex = origin.Row * columns + origin.Column;
            int lastIndex =
                (origin.Row + footprint.Height - 1) * columns +
                origin.Column + footprint.Width - 1;

            firstCell = _gridCellsRoot.GetChild(
                firstIndex) as RectTransform;

            lastCell = _gridCellsRoot.GetChild(
                lastIndex) as RectTransform;

            if (firstCell == null || lastCell == null)
            {
                error = "Inventory grid children must use RectTransform.";
                return false;
            }

            error = null;
            return true;
        }

        private static void EncapsulateRectInRoot(
            RectTransform source,
            RectTransform relativeTo,
            ref Vector2 minimum,
            ref Vector2 maximum)
        {
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);

            foreach (Vector3 corner in corners)
            {
                Vector3 localCorner = relativeTo.InverseTransformPoint(
                    corner);

                minimum = Vector2.Min(minimum, localCorner);
                maximum = Vector2.Max(maximum, localCorner);
            }
        }

        private void EnsureQuickSlotStateCache()
        {
            int slotCount = _quickSlotsRoot != null
                ? _quickSlotsRoot.childCount
                : 0;

            if (_quickSlotAssignedStates != null &&
                _quickSlotAssignedStates.Length == slotCount)
            {
                return;
            }

            _quickSlotAssignedStates = new Transform[slotCount];

            for (int index = 0; index < slotCount; index++)
            {
                Transform slot = _quickSlotsRoot.GetChild(index);

                if (!string.IsNullOrWhiteSpace(
                        _quickSlotAssignedStatePath))
                {
                    _quickSlotAssignedStates[index] =
                        slot.Find(_quickSlotAssignedStatePath);
                }
            }
        }

        private static void SetHighlightsActive(
            GameObject[] highlights,
            bool active)
        {
            if (highlights == null)
                return;

            foreach (GameObject highlight in highlights)
            {
                if (highlight != null)
                    highlight.SetActive(active);
            }
        }

        private bool TryGetGridPose(
            int columns,
            int rows,
            out Vector3 worldCenter,
            out Quaternion worldRotation,
            out float worldCellSize,
            out string error)
        {
            worldCenter = default;
            worldRotation = default;
            worldCellSize = 0f;

            if (columns <= 0 || rows <= 0)
            {
                error = "Inventory grid dimensions must be greater than zero.";
                return false;
            }

            if (_gridCellsRoot == null)
            {
                error = "Inventory grid cells root is not assigned.";
                return false;
            }

            int requiredCellCount = columns * rows;

            if (_gridCellsRoot.childCount < requiredCellCount)
            {
                error = $"Inventory grid cells root must have at least " +
                        $"{requiredCellCount} direct children, but it has " +
                        $"{_gridCellsRoot.childCount}.";
                return false;
            }

            Canvas.ForceUpdateCanvases();

            RectTransform first =
                _gridCellsRoot.GetChild(0) as RectTransform;
            RectTransform last =
                _gridCellsRoot.GetChild(requiredCellCount - 1) as RectTransform;

            if (first == null || last == null)
            {
                error = "Inventory grid children must use RectTransform.";
                return false;
            }

            Vector3 firstCenter = GetWorldCenter(first);
            Vector3 horizontalStep;
            Vector3 verticalStep;

            if (columns > 1)
            {
                RectTransform second =
                    _gridCellsRoot.GetChild(1) as RectTransform;

                if (second == null)
                {
                    error = "Inventory grid children must use RectTransform.";
                    return false;
                }

                horizontalStep = GetWorldCenter(second) - firstCenter;
            }
            else
            {
                horizontalStep = first.TransformVector(
                    Vector3.right * first.rect.width);
            }

            if (rows > 1)
            {
                RectTransform nextRow =
                    _gridCellsRoot.GetChild(columns) as RectTransform;

                if (nextRow == null)
                {
                    error = "Inventory grid children must use RectTransform.";
                    return false;
                }

                verticalStep = firstCenter - GetWorldCenter(nextRow);
            }
            else
            {
                verticalStep = first.TransformVector(
                    Vector3.up * first.rect.height);
            }

            if (!TryCreatePose(
                    horizontalStep,
                    verticalStep,
                    out worldRotation,
                    out worldCellSize,
                    out error))
            {
                return false;
            }

            worldCenter = (firstCenter + GetWorldCenter(last)) * 0.5f;
            error = null;
            return true;
        }

        private bool TryGetRectPose(
            RectTransform rectTransform,
            out Vector3 worldCenter,
            out Quaternion worldRotation,
            out float worldCellSize,
            out string error)
        {
            worldCenter = default;
            worldRotation = default;
            worldCellSize = 0f;

            if (rectTransform == null)
            {
                error = "Slot does not use RectTransform.";
                return false;
            }

            Canvas.ForceUpdateCanvases();

            Vector3 horizontal = rectTransform.TransformVector(
                Vector3.right * rectTransform.rect.width);

            Vector3 vertical = rectTransform.TransformVector(
                Vector3.up * rectTransform.rect.height);

            if (!TryCreatePose(
                    horizontal,
                    vertical,
                    out worldRotation,
                    out worldCellSize,
                    out error))
            {
                return false;
            }

            worldCenter = GetWorldCenter(rectTransform);
            error = null;
            return true;
        }

        private bool TryCreatePose(
            Vector3 horizontal,
            Vector3 vertical,
            out Quaternion worldRotation,
            out float worldCellSize,
            out string error)
        {
            worldRotation = default;
            worldCellSize = 0f;

            float horizontalSize = horizontal.magnitude;
            float verticalSize = vertical.magnitude;

            if (horizontalSize <= Mathf.Epsilon ||
                verticalSize <= Mathf.Epsilon)
            {
                error = "Layout cells must have a non-zero world size.";
                return false;
            }

            float aspectError = Mathf.Abs(horizontalSize - verticalSize) /
                                Mathf.Max(horizontalSize, verticalSize);

            if (aspectError > _maximumCellAspectError)
            {
                error = $"Layout cells must be approximately square. " +
                        $"Measured size is {horizontalSize:F5} x " +
                        $"{verticalSize:F5}.";
                return false;
            }

            Vector3 rightAxis = horizontal / horizontalSize;
            Vector3 upAxis = vertical / verticalSize;
            Vector3 depthAxis = Vector3.Cross(upAxis, rightAxis);

            if (depthAxis.sqrMagnitude <= Mathf.Epsilon)
            {
                error = "Layout horizontal and vertical axes overlap.";
                return false;
            }

            depthAxis.Normalize();
            upAxis = Vector3.Cross(rightAxis, depthAxis).normalized;
            worldRotation = Quaternion.LookRotation(upAxis, depthAxis);
            worldCellSize = (horizontalSize + verticalSize) * 0.5f;
            error = null;
            return true;
        }

        private static Vector3 GetWorldCenter(RectTransform rectTransform)
        {
            return rectTransform.TransformPoint(rectTransform.rect.center);
        }

        private static void ApplyWorldPose(
            Transform target,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float desiredWorldScale)
        {
            target.localScale = Vector3.one;
            target.SetPositionAndRotation(worldPosition, worldRotation);

            float xScale = GetRequiredLocalScale(
                target,
                Vector3.right,
                desiredWorldScale);

            float yScale = GetRequiredLocalScale(
                target,
                Vector3.up,
                desiredWorldScale);

            float zScale = GetRequiredLocalScale(
                target,
                Vector3.forward,
                desiredWorldScale);

            target.localScale = new Vector3(xScale, yScale, zScale);
        }

        private static float GetRequiredLocalScale(
            Transform target,
            Vector3 localAxis,
            float desiredWorldScale)
        {
            float currentWorldScale = target.TransformVector(localAxis).magnitude;

            return currentWorldScale > Mathf.Epsilon
                ? desiredWorldScale / currentWorldScale
                : desiredWorldScale;
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void OnValidate()
        {
            _validCellHighlights = null;
            _invalidCellHighlights = null;
            _quickSlotAssignedStates = null;
        }

        private void OnDisable()
        {
            HideGridCellHighlights();
        }

        private readonly struct ItemFootprintVisual
        {
            public RectTransform Root { get; }
            public Transform SelectedState { get; }

            public ItemFootprintVisual(
                RectTransform root,
                Transform selectedState)
            {
                Root = root;
                SelectedState = selectedState;
            }
        }
    }
}
