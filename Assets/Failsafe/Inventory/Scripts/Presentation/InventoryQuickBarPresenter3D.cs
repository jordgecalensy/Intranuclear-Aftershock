using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Failsafe.Inventory.Presentation
{
    [DisallowMultipleComponent]
    public sealed class InventoryQuickBarPresenter3D :
        MonoBehaviour,
        IDisposable
    {
        public const int NoActiveSlot = -1;

        private const float SlotSizeInCells = 0.92f;
        private const float BorderWidthInCells = 0.035f;
        private const float BackgroundThicknessInCells = 0.025f;
        private const float BackgroundDepthInCells = -0.75f;
        private const float BorderDepthInCells = -0.70f;
        private const float TextDepthInCells = 0.44f;
        private const float OpenHorizontalGapInCells = 0.30f;
        private const float ClosedVerticalGapInCells = 0.75f;
        private const float ClosedSlotSpacingInCells = 1.05f;
        private const float ModelCellScale = 0.84f;

        private static readonly InventoryGridSize QuickSlotFootprint =
            new InventoryGridSize(1, 1);

        private static readonly Color EmptyBackgroundColor =
            new Color(0.018f, 0.02f, 0.022f, 1f);

        private static readonly Color AssignedBackgroundColor =
            new Color(0.07f, 0.027f, 0.008f, 1f);

        private static readonly Color EmptyBorderColor =
            new Color(0.48f, 0.13f, 0.015f, 1f);

        private static readonly Color AssignedBorderColor =
            new Color(1f, 0.28f, 0f, 1f);

        private static readonly Color ActiveBorderColor =
            new Color(1f, 0.72f, 0.04f, 1f);

        public bool IsInitialized { get; private set; }
        public bool IsInventoryOpen { get; private set; }
        public int ActiveSlotIndex { get; private set; } = NoActiveSlot;
        public int SlotCount => _slots?.Length ?? 0;

        private InventoryGridModel _grid;
        private InventoryQuickSlots _quickSlots;
        private IInventoryItemViewDefinitionResolver _resolver;
        private InventoryGridSpace3D _gridSpace;
        private SlotVisual[] _slots;
        private InventoryRobotPresentationLayout3D _externalOpenLayout;
        private InventoryQuickBarPresentationLayout3D _externalClosedLayout;
        private Transform _defaultParent;
        private Vector3 _defaultLocalPosition;
        private Quaternion _defaultLocalRotation;
        private Vector3 _defaultLocalScale;

        private Material _emptyBackgroundMaterial;
        private Material _assignedBackgroundMaterial;
        private Material _emptyBorderMaterial;
        private Material _assignedBorderMaterial;
        private Material _activeBorderMaterial;

        public void Initialize(
            InventoryGridModel grid,
            InventoryQuickSlots quickSlots,
            InventoryGridSpace3D gridSpace,
            IInventoryItemViewDefinitionResolver resolver)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException(
                    "Inventory quick bar presenter is already initialized.");
            }

            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _quickSlots = quickSlots ??
                throw new ArgumentNullException(nameof(quickSlots));
            _resolver = resolver ??
                throw new ArgumentNullException(nameof(resolver));

            if (grid.Columns != gridSpace.Columns ||
                grid.Rows != gridSpace.Rows)
            {
                throw new ArgumentException(
                    "Quick bar grid space must match the inventory grid.",
                    nameof(gridSpace));
            }

            _gridSpace = gridSpace;
            _defaultParent = transform.parent;
            _defaultLocalPosition = transform.localPosition;
            _defaultLocalRotation = transform.localRotation;
            _defaultLocalScale = transform.localScale;

            try
            {
                CreateMaterials();
                CreateSlots();
                Subscribe();
                IsInitialized = true;

                for (int index = 0; index < _slots.Length; index++)
                {
                    RebuildSlot(
                        index,
                        _quickSlots.GetAssignedInstanceId(index));
                }

                SetInventoryOpen(false);
                SetActiveSlot(NoActiveSlot);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetInventoryOpen(bool isOpen)
        {
            if (!IsInitialized || _slots == null)
                return;

            IsInventoryOpen = isOpen;
            _externalClosedLayout?.SetVisible(false);

            if (isOpen && _externalOpenLayout != null)
            {
                if (TryApplyExternalOpenLayout(out string error))
                {
                    SetProceduralChromeVisible(false);
                    SetItemModelsVisible(true);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning(
                        $"Could not apply robot quick-slot layout: {error}. " +
                        "Using the procedural quick bar instead.",
                        this);
                }
            }

            if (!isOpen && _externalClosedLayout != null)
            {
                if (TryApplyExternalClosedLayout(out string error))
                {
                    SetProceduralChromeVisible(false);
                    SetItemModelsVisible(false);
                    _externalClosedLayout.SetVisible(true);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning(
                        $"Could not apply manual closed quick-bar layout: " +
                        $"{error}. Using the procedural quick bar instead.",
                        this);
                }
            }

            RestoreDefaultRootPose();
            SetProceduralChromeVisible(true);
            SetItemModelsVisible(true);

            float cellSize = _gridSpace.CellSize;

            for (int index = 0; index < _slots.Length; index++)
            {
                _slots[index].Root.localPosition = isOpen
                    ? GetOpenSlotPosition(index, cellSize)
                    : GetClosedSlotPosition(index, cellSize);
            }
        }

        public bool TrySetExternalOpenLayout(
            InventoryRobotPresentationLayout3D layout,
            out string error)
        {
            if (layout == null)
            {
                _externalOpenLayout = null;
                error = null;
                return true;
            }

            if (!IsInitialized || _slots == null)
            {
                error = "Quick-bar presenter is not initialized.";
                return false;
            }

            if (!layout.TryValidate(
                    _grid.Columns,
                    _grid.Rows,
                    _slots.Length,
                    out error))
            {
                return false;
            }

            _externalOpenLayout = layout;

            for (int index = 0; index < _slots.Length; index++)
            {
                ApplySlotState(index);
            }

            if (IsInventoryOpen)
                SetInventoryOpen(true);

            error = null;
            return true;
        }

        public bool TrySetExternalClosedLayout(
            InventoryQuickBarPresentationLayout3D layout,
            out string error)
        {
            if (layout == null)
            {
                _externalClosedLayout?.SetVisible(false);
                _externalClosedLayout = null;

                if (IsInitialized)
                    SetInventoryOpen(IsInventoryOpen);

                error = null;
                return true;
            }

            if (!IsInitialized || _slots == null)
            {
                error = "Quick-bar presenter is not initialized.";
                return false;
            }

            if (!layout.TryValidate(_slots.Length, out error))
                return false;

            if (_externalClosedLayout != layout)
                _externalClosedLayout?.SetVisible(false);

            _externalClosedLayout = layout;

            for (int index = 0; index < _slots.Length; index++)
            {
                ApplySlotIcon(index);
                ApplySlotState(index);
            }

            SetInventoryOpen(IsInventoryOpen);

            error = null;
            return true;
        }

        public void SetActiveSlot(int slotIndex)
        {
            if (!IsInitialized)
                return;

            if (slotIndex < NoActiveSlot || slotIndex >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));

            if (ActiveSlotIndex == slotIndex)
                return;

            int previousSlotIndex = ActiveSlotIndex;
            ActiveSlotIndex = slotIndex;

            if (previousSlotIndex >= 0 && previousSlotIndex < SlotCount)
                ApplySlotState(previousSlotIndex);

            if (ActiveSlotIndex >= 0)
                ApplySlotState(ActiveSlotIndex);

            _externalClosedLayout?.RequestReveal();
        }

        public bool TryGetSlotRoot(int slotIndex, out Transform slotRoot)
        {
            slotRoot = null;

            if (!IsInitialized ||
                slotIndex < 0 ||
                slotIndex >= SlotCount)
            {
                return false;
            }

            slotRoot = _slots[slotIndex].Root;
            return slotRoot != null;
        }

        public void Dispose()
        {
            Unsubscribe();
            _externalClosedLayout?.SetVisible(false);

            if (_slots != null)
            {
                foreach (SlotVisual slot in _slots)
                {
                    if (slot?.Root != null)
                        DestroyUnityObject(slot.Root.gameObject);
                }
            }

            _slots = null;
            DestroyMaterial(_emptyBackgroundMaterial);
            DestroyMaterial(_assignedBackgroundMaterial);
            DestroyMaterial(_emptyBorderMaterial);
            DestroyMaterial(_assignedBorderMaterial);
            DestroyMaterial(_activeBorderMaterial);

            _emptyBackgroundMaterial = null;
            _assignedBackgroundMaterial = null;
            _emptyBorderMaterial = null;
            _assignedBorderMaterial = null;
            _activeBorderMaterial = null;
            _grid = null;
            _quickSlots = null;
            _resolver = null;
            _externalOpenLayout = null;
            _externalClosedLayout = null;
            _defaultParent = null;
            ActiveSlotIndex = NoActiveSlot;
            IsInventoryOpen = false;
            IsInitialized = false;
        }

        private bool TryApplyExternalOpenLayout(out string error)
        {
            if (!_externalOpenLayout.TryAttachQuickBarRoot(
                    transform,
                    _slots.Length,
                    out error))
            {
                return false;
            }

            for (int index = 0; index < _slots.Length; index++)
            {
                if (!_externalOpenLayout.TryApplyQuickSlotPose(
                        index,
                        _slots[index].Root,
                        _gridSpace.CellSize,
                        out error))
                {
                    RestoreDefaultRootPose();
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryApplyExternalClosedLayout(out string error)
        {
            if (!_externalClosedLayout.TryValidate(
                    _slots.Length,
                    out error))
                return false;

            RestoreDefaultRootPose();

            for (int index = 0; index < _slots.Length; index++)
            {
                ApplySlotIcon(index);
                ApplySlotState(index);
            }

            error = null;
            return true;
        }

        private void RestoreDefaultRootPose()
        {
            transform.SetParent(_defaultParent, false);
            transform.localPosition = _defaultLocalPosition;
            transform.localRotation = _defaultLocalRotation;
            transform.localScale = _defaultLocalScale;

            if (_slots == null)
                return;

            foreach (SlotVisual slot in _slots)
            {
                if (slot?.Root == null)
                    continue;

                slot.Root.localRotation = Quaternion.identity;
                slot.Root.localScale = Vector3.one;
            }
        }

        private void SetProceduralChromeVisible(bool visible)
        {
            if (_slots == null)
                return;

            foreach (SlotVisual slot in _slots)
            {
                if (slot == null)
                    continue;

                if (slot.BackgroundRenderer != null)
                    slot.BackgroundRenderer.enabled = visible;

                if (slot.FrameRenderers != null)
                {
                    foreach (MeshRenderer renderer in slot.FrameRenderers)
                    {
                        if (renderer != null)
                            renderer.enabled = visible;
                    }
                }

                if (slot.NumberLabel != null)
                    slot.NumberLabel.gameObject.SetActive(visible);

                if (slot.QuantityLabel != null)
                    slot.QuantityLabel.gameObject.SetActive(visible);
            }
        }

        private void Subscribe()
        {
            _quickSlots.SlotChanged += HandleSlotChanged;
            _grid.QuantityChanged += HandleQuantityChanged;
        }

        private void Unsubscribe()
        {
            if (_quickSlots != null)
                _quickSlots.SlotChanged -= HandleSlotChanged;

            if (_grid != null)
                _grid.QuantityChanged -= HandleQuantityChanged;
        }

        private void HandleSlotChanged(int slotIndex, string instanceId)
        {
            if (!IsInitialized ||
                slotIndex < 0 ||
                slotIndex >= SlotCount)
            {
                return;
            }

            try
            {
                RebuildSlot(slotIndex, instanceId);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Failed to update quick slot {slotIndex + 1}: " +
                    exception.Message,
                    this);
            }
        }

        private void HandleQuantityChanged(InventoryItemModel item)
        {
            if (!IsInitialized || item == null)
                return;

            for (int index = 0; index < _slots.Length; index++)
            {
                if (string.Equals(
                        _slots[index].InstanceId,
                        item.InstanceId,
                        StringComparison.Ordinal))
                {
                    UpdateQuantityLabel(_slots[index], item);
                    _externalClosedLayout?.RequestReveal();
                }
            }
        }

        private void CreateMaterials()
        {
            Shader shader = FindUnlitShader();

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Inventory quick bar could not find a supported unlit shader.");
            }

            _emptyBackgroundMaterial = CreateMaterial(
                shader,
                "Quick Bar Empty Background (Runtime)",
                EmptyBackgroundColor);

            _assignedBackgroundMaterial = CreateMaterial(
                shader,
                "Quick Bar Assigned Background (Runtime)",
                AssignedBackgroundColor);

            _emptyBorderMaterial = CreateMaterial(
                shader,
                "Quick Bar Empty Border (Runtime)",
                EmptyBorderColor);

            _assignedBorderMaterial = CreateMaterial(
                shader,
                "Quick Bar Assigned Border (Runtime)",
                AssignedBorderColor);

            _activeBorderMaterial = CreateMaterial(
                shader,
                "Quick Bar Active Border (Runtime)",
                ActiveBorderColor);
        }

        private void CreateSlots()
        {
            _slots = new SlotVisual[_quickSlots.SlotCount];

            for (int index = 0; index < _slots.Length; index++)
                _slots[index] = CreateSlot(index);
        }

        private SlotVisual CreateSlot(int slotIndex)
        {
            float cellSize = _gridSpace.CellSize;
            float slotSize = cellSize * SlotSizeInCells;
            float halfSlotSize = slotSize * 0.5f;
            float borderWidth = cellSize * BorderWidthInCells;

            GameObject rootObject = new GameObject(
                $"Quick Slot [{slotIndex + 1}]");

            rootObject.layer = gameObject.layer;
            rootObject.transform.SetParent(transform, false);

            SlotVisual slot = new SlotVisual
            {
                Root = rootObject.transform,
                FrameRenderers = new List<MeshRenderer>(4)
            };

            slot.BackgroundRenderer = CreateCube(
                "Background",
                slot.Root,
                _emptyBackgroundMaterial,
                new Vector3(
                    0f,
                    cellSize * BackgroundDepthInCells,
                    0f),
                new Vector3(
                    slotSize,
                    cellSize * BackgroundThicknessInCells,
                    slotSize));

            AddBorder(
                slot,
                "Left Border",
                new Vector3(-halfSlotSize, 0f, 0f),
                new Vector3(borderWidth, 0f, slotSize + borderWidth));

            AddBorder(
                slot,
                "Right Border",
                new Vector3(halfSlotSize, 0f, 0f),
                new Vector3(borderWidth, 0f, slotSize + borderWidth));

            AddBorder(
                slot,
                "Top Border",
                new Vector3(0f, 0f, halfSlotSize),
                new Vector3(slotSize + borderWidth, 0f, borderWidth));

            AddBorder(
                slot,
                "Bottom Border",
                new Vector3(0f, 0f, -halfSlotSize),
                new Vector3(slotSize + borderWidth, 0f, borderWidth));

            float textPadding = cellSize * 0.08f;

            slot.NumberLabel = CreateLabel(
                "Slot Number",
                (slotIndex + 1).ToString(),
                slot.Root,
                TextAnchor.UpperRight,
                TextAlignment.Right,
                new Vector3(
                    halfSlotSize - textPadding,
                    cellSize * TextDepthInCells,
                    halfSlotSize - textPadding));

            slot.QuantityLabel = CreateLabel(
                "Stack Quantity",
                string.Empty,
                slot.Root,
                TextAnchor.LowerRight,
                TextAlignment.Right,
                new Vector3(
                    halfSlotSize - textPadding,
                    cellSize * TextDepthInCells,
                    -halfSlotSize + textPadding));

            return slot;
        }

        private void AddBorder(
            SlotVisual slot,
            string borderName,
            Vector3 localPosition,
            Vector3 localScale)
        {
            localPosition.y = _gridSpace.CellSize * BorderDepthInCells;
            localScale.y =
                _gridSpace.CellSize * BackgroundThicknessInCells;

            MeshRenderer renderer = CreateCube(
                borderName,
                slot.Root,
                _emptyBorderMaterial,
                localPosition,
                localScale);

            slot.FrameRenderers.Add(renderer);
        }

        private MeshRenderer CreateCube(
            string objectName,
            Transform parent,
            Material material,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject geometry = GameObject.CreatePrimitive(
                PrimitiveType.Cube);

            geometry.name = objectName;
            geometry.layer = gameObject.layer;
            geometry.transform.SetParent(parent, false);
            geometry.transform.localPosition = localPosition;
            geometry.transform.localScale = localScale;

            if (geometry.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
                DestroyUnityObject(collider);
            }

            MeshRenderer renderer = geometry.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private TextMesh CreateLabel(
            string objectName,
            string text,
            Transform parent,
            TextAnchor anchor,
            TextAlignment alignment,
            Vector3 localPosition)
        {
            GameObject labelObject = new GameObject(objectName);
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = anchor;
            label.alignment = alignment;
            label.fontSize = 64;
            label.characterSize = _gridSpace.CellSize * 0.16f;
            label.color = EmptyBorderColor;
            label.richText = false;

            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return label;
        }

        private void RebuildSlot(int slotIndex, string instanceId)
        {
            SlotVisual slot = _slots[slotIndex];
            _externalClosedLayout?.RequestReveal();

            if (slot.ItemView != null)
            {
                DestroyUnityObject(slot.ItemView.gameObject);
                slot.ItemView = null;
            }

            slot.InstanceId = null;
            slot.Icon = null;
            slot.QuantityLabel.text = string.Empty;

            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_quickSlots.TryGetAssignedItem(
                    slotIndex,
                    out InventoryItemModel item))
            {
                ApplySlotIcon(slotIndex);
                ApplySlotState(slotIndex);
                return;
            }

            slot.InstanceId = item.InstanceId;

            if (_resolver.TryResolve(
                    item,
                    out InventoryModelViewDefinition definition,
                    out string error))
            {
                slot.Icon = definition.Icon;

                if (slot.Icon == null)
                {
                    Debug.LogWarning(
                        $"Quick slot {slotIndex + 1} cannot display item " +
                        $"'{item.InstanceId}': InventoryIcon is not assigned.",
                        this);
                }

                GameObject viewObject = new GameObject(
                    $"Quick Slot Item [{item.InstanceId}]");

                viewObject.layer = gameObject.layer;
                viewObject.transform.SetParent(slot.Root, false);

                try
                {
                    InventoryItemView3D view = viewObject
                        .AddComponent<InventoryItemView3D>();

                    view.Initialize(
                        definition,
                        QuickSlotFootprint,
                        _gridSpace.CellSize * ModelCellScale);

                    slot.ItemView = view;
                }
                catch
                {
                    DestroyUnityObject(viewObject);
                    throw;
                }
            }
            else
            {
                Debug.LogWarning(
                    $"Quick slot {slotIndex + 1} cannot display item " +
                    $"'{item.InstanceId}': {error}",
                    this);
            }

            UpdateQuantityLabel(slot, item);
            ApplySlotIcon(slotIndex);
            ApplySlotState(slotIndex);
            SetItemModelsVisible(
                IsInventoryOpen || _externalClosedLayout == null);
        }

        private void UpdateQuantityLabel(
            SlotVisual slot,
            InventoryItemModel item)
        {
            slot.QuantityLabel.text = item.Quantity > 1
                ? item.Quantity.ToString()
                : string.Empty;
        }

        private void ApplySlotState(int slotIndex)
        {
            SlotVisual slot = _slots[slotIndex];
            bool isAssigned =
                !string.IsNullOrWhiteSpace(slot.InstanceId);
            bool isActive = ActiveSlotIndex == slotIndex;

            slot.BackgroundRenderer.sharedMaterial = isAssigned
                ? _assignedBackgroundMaterial
                : _emptyBackgroundMaterial;

            Material borderMaterial = isActive
                ? _activeBorderMaterial
                : isAssigned
                    ? _assignedBorderMaterial
                    : _emptyBorderMaterial;

            foreach (MeshRenderer renderer in slot.FrameRenderers)
                renderer.sharedMaterial = borderMaterial;

            Color labelColor = isActive
                ? ActiveBorderColor
                : isAssigned
                    ? AssignedBorderColor
                    : EmptyBorderColor;

            slot.NumberLabel.color = labelColor;
            slot.QuantityLabel.color = labelColor;

            _externalOpenLayout?.SetQuickSlotState(
                slotIndex,
                isAssigned);

            _externalClosedLayout?.SetSlotState(
                slotIndex,
                isAssigned && isActive);
        }

        private void ApplySlotIcon(int slotIndex)
        {
            if (_slots == null ||
                slotIndex < 0 ||
                slotIndex >= _slots.Length)
            {
                return;
            }

            Sprite icon = _slots[slotIndex].Icon;
            _externalClosedLayout?.SetSlotIcon(slotIndex, icon);
        }

        private void SetItemModelsVisible(bool visible)
        {
            if (_slots == null)
                return;

            foreach (SlotVisual slot in _slots)
            {
                if (slot?.ItemView != null)
                    slot.ItemView.gameObject.SetActive(visible);
            }
        }

        private Vector3 GetOpenSlotPosition(int slotIndex, float cellSize)
        {
            float gridHalfWidth =
                _gridSpace.Columns * cellSize * 0.5f;
            float quickBarHalfWidth = cellSize * 0.5f;
            float x = gridHalfWidth +
                      cellSize * OpenHorizontalGapInCells +
                      quickBarHalfWidth;

            float z =
                (_slots.Length * 0.5f - slotIndex - 0.5f) * cellSize;

            return new Vector3(x, 0f, z);
        }

        private Vector3 GetClosedSlotPosition(int slotIndex, float cellSize)
        {
            float centeredIndex =
                slotIndex - (_slots.Length - 1) * 0.5f;

            float x = centeredIndex *
                      cellSize * ClosedSlotSpacingInCells;
            float z = -_gridSpace.Rows * cellSize * 0.5f -
                      cellSize * ClosedVerticalGapInCells;

            return new Vector3(x, 0f, z);
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

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
                DestroyUnityObject(material);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private sealed class SlotVisual
        {
            public Transform Root;
            public MeshRenderer BackgroundRenderer;
            public List<MeshRenderer> FrameRenderers;
            public TextMesh NumberLabel;
            public TextMesh QuantityLabel;
            public InventoryItemView3D ItemView;
            public Sprite Icon;
            public string InstanceId;
        }
    }
}
