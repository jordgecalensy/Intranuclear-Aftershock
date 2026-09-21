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

                Canvas.ForceUpdateCanvases();

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
