using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    public sealed class InventoryGridPresenter3D : MonoBehaviour, IDisposable
    {
        public bool IsInitialized { get; private set; }
        public int ViewCount => _views.Count;
        public InventoryGridSpace3D GridSpace { get; private set; }

        private readonly Dictionary<string, InventoryItemView3D> _views =
            new Dictionary<string, InventoryItemView3D>(StringComparer.Ordinal);

        private InventoryGridModel _grid;
        private IInventoryItemViewDefinitionResolver _resolver;
        private InventoryPrototypeGridVisual3D _prototypeGridVisual;
        private InventoryRobotPresentationLayout3D _manualGridLayout;
        private bool _highlightErrorLogged;

        public void Initialize(
            InventoryGridModel grid,
            InventoryGridSpace3D gridSpace,
            IInventoryItemViewDefinitionResolver resolver)
        {
            if (IsInitialized)
                throw new InvalidOperationException("Inventory grid presenter is already initialized.");

            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

            if (grid.Columns != gridSpace.Columns || grid.Rows != gridSpace.Rows)
            {
                throw new ArgumentException(
                    "Presentation grid dimensions must match the inventory grid model.",
                    nameof(gridSpace));
            }

            GridSpace = gridSpace;

            try
            {
                CreatePrototypeGridVisual();
                Subscribe();

                foreach (InventoryPlacement placement in _grid.Placements)
                    CreateView(placement);

                IsInitialized = true;
            }
            catch
            {
                Unsubscribe();
                DestroyAllViews();
                DestroyPrototypeGridVisual();
                _grid = null;
                _resolver = null;
                throw;
            }
        }

        public bool TryGetView(
            string instanceId,
            out InventoryItemView3D view)
        {
            view = null;

            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _views.TryGetValue(instanceId, out view) &&
                   view != null;
        }

        public bool TryGetHitTarget(
            string instanceId,
            out InventoryItemHitTarget3D hitTarget)
        {
            hitTarget = null;

            return TryGetView(instanceId, out InventoryItemView3D view) &&
                   view.TryGetComponent(out hitTarget) &&
                   hitTarget != null;
        }

        public bool TryPreviewPlacement(
            string instanceId,
            InventoryGridPosition origin,
            InventoryGridSize footprint,
            InventoryItemRotation rotation)
        {
            if (!IsInitialized ||
                !TryGetView(instanceId, out InventoryItemView3D view) ||
                !TryGetHitTarget(instanceId, out InventoryItemHitTarget3D hitTarget))
            {
                return false;
            }

            try
            {
                view.ApplyPlacement(origin, footprint, rotation, GridSpace);
                hitTarget.ApplyFootprint(footprint);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        public bool TryPreviewFreePosition(
            string instanceId,
            Vector3 localPosition,
            InventoryGridSize footprint,
            InventoryItemRotation rotation)
        {
            if (!IsInitialized ||
                !TryGetView(instanceId, out InventoryItemView3D view) ||
                !TryGetHitTarget(instanceId, out InventoryItemHitTarget3D hitTarget))
            {
                return false;
            }

            view.ApplyFreePreview(localPosition, rotation, GridSpace);
            hitTarget.ApplyFootprint(footprint);
            return true;
        }

        public bool RestorePlacement(string instanceId)
        {
            if (!IsInitialized ||
                !_grid.TryGetPlacement(instanceId, out InventoryPlacement placement) ||
                !TryGetView(instanceId, out InventoryItemView3D view))
            {
                return false;
            }

            ApplyPlacement(view, placement);
            return true;
        }

        public bool ShowPlacementHighlight(
            InventoryGridPosition origin,
            InventoryGridSize footprint,
            bool isValidPlacement)
        {
            if (!IsInitialized || _manualGridLayout == null)
                return false;

            bool shown = _manualGridLayout.TryShowGridCellHighlights(
                origin,
                footprint,
                isValidPlacement,
                _grid.Columns,
                _grid.Rows,
                out string error);

            if (!shown && !_highlightErrorLogged)
            {
                Debug.LogWarning(
                    $"Manual inventory cell highlights are unavailable: " +
                    $"{error}",
                    this);

                _highlightErrorLogged = true;
            }

            return shown;
        }

        public void HidePlacementHighlight()
        {
            _manualGridLayout?.HideGridCellHighlights();
        }

        public void SetManualGridLayout(
            InventoryRobotPresentationLayout3D layout)
        {
            if (_manualGridLayout == layout)
                return;

            _manualGridLayout?.HideGridCellHighlights();
            _manualGridLayout = layout;
            _highlightErrorLogged = false;
        }

        public bool TryGetPrototypeGridVisual(
            out InventoryPrototypeGridVisual3D prototypeGridVisual)
        {
            prototypeGridVisual = _prototypeGridVisual;
            return prototypeGridVisual != null;
        }

        public void SetPrototypeGridVisible(bool visible)
        {
            if (_prototypeGridVisual != null)
                _prototypeGridVisual.gameObject.SetActive(visible);
        }

        public void Dispose()
        {
            if (_grid != null)
                Unsubscribe();

            HidePlacementHighlight();
            DestroyAllViews();
            DestroyPrototypeGridVisual();
            _grid = null;
            _resolver = null;
            _manualGridLayout = null;
            _highlightErrorLogged = false;
            IsInitialized = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void Subscribe()
        {
            _grid.ItemPlaced += HandleItemPlaced;
            _grid.PlacementChanged += HandlePlacementChanged;
            _grid.ItemRemoved += HandleItemRemoved;
        }

        private void Unsubscribe()
        {
            _grid.ItemPlaced -= HandleItemPlaced;
            _grid.PlacementChanged -= HandlePlacementChanged;
            _grid.ItemRemoved -= HandleItemRemoved;
        }

        private void HandleItemPlaced(InventoryPlacement placement)
        {
            try
            {
                CreateView(placement);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Failed to create inventory view for item " +
                    $"'{placement?.Item?.InstanceId ?? "<null>"}': {exception.Message}",
                    this);
            }
        }

        private void HandlePlacementChanged(InventoryPlacement placement)
        {
            if (placement == null || placement.Item == null)
                return;

            if (!TryGetView(placement.Item.InstanceId, out InventoryItemView3D view))
            {
                HandleItemPlaced(placement);
                return;
            }

            ApplyPlacement(view, placement);
        }

        private void HandleItemRemoved(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) ||
                !_views.TryGetValue(instanceId, out InventoryItemView3D view))
            {
                return;
            }

            _views.Remove(instanceId);
            DestroyObject(view.gameObject);
        }

        private void CreateView(InventoryPlacement placement)
        {
            if (placement == null || placement.Item == null)
                throw new ArgumentNullException(nameof(placement));

            string instanceId = placement.Item.InstanceId;

            if (_views.ContainsKey(instanceId))
            {
                throw new InvalidOperationException(
                    $"An inventory view for item '{instanceId}' already exists.");
            }

            if (!_resolver.TryResolve(
                    placement.Item,
                    out InventoryModelViewDefinition definition,
                    out string error))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"No 3D view definition is registered for item '{instanceId}'."
                        : error);
            }

            GameObject viewObject = new GameObject($"Inventory Item [{instanceId}]");
            viewObject.layer = gameObject.layer;
            viewObject.transform.SetParent(transform, false);

            try
            {
                InventoryItemView3D view = viewObject.AddComponent<InventoryItemView3D>();
                view.Initialize(
                    definition,
                    placement.Item.BaseFootprint,
                    GridSpace.CellSize);

                InventoryItemHitTarget3D hitTarget =
                    viewObject.AddComponent<InventoryItemHitTarget3D>();

                hitTarget.Initialize(
                    instanceId,
                    placement.Footprint,
                    GridSpace.CellSize);

                ApplyPlacement(view, placement);
                _views.Add(instanceId, view);
            }
            catch
            {
                DestroyObject(viewObject);
                throw;
            }
        }

        private void CreatePrototypeGridVisual()
        {
            GameObject visualObject = new GameObject(
                "Inventory Prototype Grid Visual");

            visualObject.layer = gameObject.layer;
            visualObject.transform.SetParent(transform, false);

            try
            {
                InventoryPrototypeGridVisual3D visual = visualObject
                    .AddComponent<InventoryPrototypeGridVisual3D>();

                visual.Initialize(GridSpace);
                _prototypeGridVisual = visual;
            }
            catch
            {
                DestroyObject(visualObject);
                throw;
            }
        }

        private void ApplyPlacement(
            InventoryItemView3D view,
            InventoryPlacement placement)
        {
            view.ApplyPlacement(
                placement.Origin,
                placement.Footprint,
                placement.Item.Rotation,
                GridSpace);

            if (view.TryGetComponent(
                    out InventoryItemHitTarget3D hitTarget))
            {
                hitTarget.ApplyFootprint(placement.Footprint);
            }
        }

        private void DestroyAllViews()
        {
            foreach (InventoryItemView3D view in _views.Values)
            {
                if (view != null)
                    DestroyObject(view.gameObject);
            }

            _views.Clear();
        }

        private void DestroyPrototypeGridVisual()
        {
            if (_prototypeGridVisual == null)
                return;

            DestroyObject(_prototypeGridVisual.gameObject);
            _prototypeGridVisual = null;
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
    }
}
