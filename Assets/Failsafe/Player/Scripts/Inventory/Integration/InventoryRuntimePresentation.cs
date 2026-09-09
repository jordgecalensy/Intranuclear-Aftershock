using System;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using UnityEngine;

namespace Failsafe.Inventory.Integration
{
    internal sealed class InventoryRuntimePresentation
    {
        public InventoryGridPresenter3D GridPresenter { get; private set; }
        public InventoryQuickBarPresenter3D QuickBarPresenter
        {
            get;
            private set;
        }

        public bool IsVisible =>
            _gridRoot != null &&
            _gridRoot.activeSelf;

        private GameObject _gridRoot;
        private GameObject _quickBarRoot;
        private InventoryGridModel _grid;
        private InventoryQuickSlots _quickSlots;
        private float _cellSize;

        public bool TryInitialize(
            Transform owner,
            Transform configuredParent,
            int inventoryLayer,
            float cellSize,
            InventoryGridModel grid,
            InventoryQuickSlots quickSlots,
            ItemDataInventoryViewResolver viewResolver,
            out string error)
        {
            if (owner == null)
            {
                error = "Inventory presentation owner is not assigned.";
                return false;
            }

            if (grid == null || quickSlots == null || viewResolver == null)
            {
                error = "Inventory presentation dependencies are incomplete.";
                return false;
            }

            Transform parent = configuredParent != null
                ? configuredParent
                : owner;

            _grid = grid;
            _quickSlots = quickSlots;
            _cellSize = cellSize;

            _gridRoot = new GameObject("Inventory 3D Views");
            _gridRoot.layer = inventoryLayer;
            _gridRoot.transform.SetParent(parent, false);
            GridPresenter = _gridRoot
                .AddComponent<InventoryGridPresenter3D>();

            _quickBarRoot = new GameObject("Inventory Quick Bar 3D");
            _quickBarRoot.layer = inventoryLayer;
            _quickBarRoot.transform.SetParent(parent, false);
            QuickBarPresenter = _quickBarRoot
                .AddComponent<InventoryQuickBarPresenter3D>();

            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(
                grid.Columns,
                grid.Rows,
                cellSize);

            try
            {
                GridPresenter.Initialize(
                    grid,
                    gridSpace,
                    viewResolver);

                QuickBarPresenter.Initialize(
                    grid,
                    quickSlots,
                    gridSpace,
                    viewResolver);
            }
            catch (Exception exception)
            {
                Dispose();
                error = exception.Message;
                return false;
            }

            error = null;
            return true;
        }

        public bool SetVisible(bool visible)
        {
            if (_gridRoot == null)
                return false;

            _gridRoot.SetActive(visible);
            QuickBarPresenter?.SetInventoryOpen(visible);
            return true;
        }

        public bool TryBindRobotLayout(
            InventoryRobotPresentationLayout3D layout,
            out string error)
        {
            if (_gridRoot == null ||
                GridPresenter == null ||
                QuickBarPresenter == null ||
                _grid == null ||
                _quickSlots == null)
            {
                error = "Inventory presentation is not initialized.";
                return false;
            }

            if (layout == null)
            {
                error = "Robot inventory presentation layout is null.";
                return false;
            }

            if (!layout.TryValidate(
                    _grid.Columns,
                    _grid.Rows,
                    _quickSlots.SlotCount,
                    out error))
            {
                return false;
            }

            if (!layout.TryApplyGridPose(
                    _gridRoot.transform,
                    _grid.Columns,
                    _grid.Rows,
                    _cellSize,
                    out error))
            {
                return false;
            }

            if (!QuickBarPresenter.TrySetExternalOpenLayout(
                    layout,
                    out error))
            {
                return false;
            }

            GridPresenter.SetManualGridLayout(layout);
            GridPresenter.SetPrototypeGridVisible(false);
            error = null;
            return true;
        }

        public bool TryBindClosedQuickBarLayout(
            InventoryQuickBarPresentationLayout3D layout,
            out string error)
        {
            if (QuickBarPresenter == null)
            {
                error = "Inventory presentation is not initialized.";
                return false;
            }

            if (layout == null)
            {
                error = "Closed quick-bar presentation layout is null.";
                return false;
            }

            return QuickBarPresenter.TrySetExternalClosedLayout(
                layout,
                out error);
        }

        public void Dispose()
        {
            if (QuickBarPresenter != null)
                QuickBarPresenter.Dispose();

            if (GridPresenter != null)
                GridPresenter.Dispose();

            DestroyUnityObject(_gridRoot);
            DestroyUnityObject(_quickBarRoot);

            GridPresenter = null;
            QuickBarPresenter = null;
            _gridRoot = null;
            _quickBarRoot = null;
            _grid = null;
            _quickSlots = null;
            _cellSize = 0f;
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
