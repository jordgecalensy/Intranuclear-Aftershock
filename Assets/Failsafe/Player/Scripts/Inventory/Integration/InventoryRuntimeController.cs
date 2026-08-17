using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using UnityEngine;

namespace Failsafe.Inventory.Integration
{
    [DisallowMultipleComponent]
    public sealed class InventoryRuntimeController : MonoBehaviour
    {
        [Header("Runtime Presentation")]
        [SerializeField] private Transform _presentationRoot;
        [SerializeField, Min(0.001f)] private float _cellSize = 0.2f;
        [SerializeField] private string _inventoryLayerName = "Inventory";
        [SerializeField] private bool _initializeOnAwake = true;

        public bool IsInitialized { get; private set; }
        public InventoryGridModel Grid { get; private set; }
        public InventoryQuickSlots QuickSlots { get; private set; }
        public InventoryGridPresenter3D Presenter { get; private set; }
        public int RegisteredWorldItemCount => _worldItemsByInstanceId.Count;
        public bool IsPresentationVisible =>
            _generatedPresentationRoot != null &&
            _generatedPresentationRoot.activeSelf;

        private readonly Dictionary<string, Item> _worldItemsByInstanceId =
            new Dictionary<string, Item>(StringComparer.Ordinal);

        private ItemDataInventoryViewResolver _viewResolver;
        private GameObject _generatedPresentationRoot;
        private GameObject _storedWorldItemsRoot;

        private void Awake()
        {
            if (_initializeOnAwake && !TryInitialize(out string error))
            {
                Debug.LogError(
                    $"Failed to initialize inventory runtime controller: {error}",
                    this);

                enabled = false;
            }
        }

        public bool TryInitialize(out string error)
        {
            if (IsInitialized)
            {
                error = null;
                return true;
            }

            if (_cellSize <= 0f)
            {
                error = "Inventory cell size must be greater than zero.";
                return false;
            }

            int inventoryLayer = LayerMask.NameToLayer(_inventoryLayerName);

            if (inventoryLayer < 0)
            {
                error = $"Unity layer '{_inventoryLayerName}' does not exist.";
                return false;
            }

            Grid = new InventoryGridModel();
            QuickSlots = new InventoryQuickSlots(Grid);
            _viewResolver = new ItemDataInventoryViewResolver();

            Transform parent = _presentationRoot != null
                ? _presentationRoot
                : transform;

            _generatedPresentationRoot = new GameObject("Inventory 3D Views");
            _generatedPresentationRoot.layer = inventoryLayer;
            _generatedPresentationRoot.transform.SetParent(parent, false);

            Presenter = _generatedPresentationRoot
                .AddComponent<InventoryGridPresenter3D>();

            try
            {
                Presenter.Initialize(
                    Grid,
                    new InventoryGridSpace3D(
                        Grid.Columns,
                        Grid.Rows,
                        _cellSize),
                    _viewResolver);
            }
            catch (Exception exception)
            {
                DisposeRuntime();
                error = exception.Message;
                return false;
            }

            _storedWorldItemsRoot = new GameObject(
                "Inventory Stored World Items");

            _storedWorldItemsRoot.transform.SetParent(transform, false);
            _storedWorldItemsRoot.SetActive(false);

            IsInitialized = true;
            error = null;
            return true;
        }

        public bool SetPresentationVisible(bool visible)
        {
            if (!IsInitialized || _generatedPresentationRoot == null)
                return false;

            _generatedPresentationRoot.SetActive(visible);
            return true;
        }

        public InventoryOperationResult AddFirstAvailable(
            ItemData itemData,
            int quantity,
            out string instanceId,
            out string error)
        {
            instanceId = null;

            if (!EnsureInitialized(out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            string candidateInstanceId = Guid.NewGuid().ToString("N");

            if (!ItemDataInventoryAdapter.TryCreateModel(
                    itemData,
                    candidateInstanceId,
                    quantity,
                    out InventoryItemModel item,
                    out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (!_viewResolver.TryRegister(
                    candidateInstanceId,
                    itemData,
                    out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    item.Quantity);
            }

            InventoryOperationResult result = Grid.TryPlaceFirstAvailable(
                item,
                out _);

            if (!result.IsSuccess)
            {
                _viewResolver.Unregister(candidateInstanceId);
                error = $"Inventory placement failed: {result.FailureReason}.";
                return result;
            }

            if (!Presenter.TryGetView(candidateInstanceId, out _))
            {
                Grid.TryRemove(candidateInstanceId);
                _viewResolver.Unregister(candidateInstanceId);
                error = "The inventory item was placed, but its 3D view could not be created.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    item.Quantity);
            }

            instanceId = candidateInstanceId;
            error = null;
            return result;
        }

        public InventoryOperationResult RegisterEquippedWorldItem(
            Item worldItem,
            out string instanceId,
            out string error)
        {
            return TryAddWorldItem(
                worldItem,
                moveToStorage: false,
                out instanceId,
                out error);
        }

        public InventoryOperationResult StoreWorldItem(
            Item worldItem,
            out string instanceId,
            out string error)
        {
            return TryAddWorldItem(
                worldItem,
                moveToStorage: true,
                out instanceId,
                out error);
        }

        public bool TryGetWorldItem(
            string instanceId,
            out Item worldItem)
        {
            worldItem = null;

            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _worldItemsByInstanceId.TryGetValue(
                       instanceId,
                       out worldItem) &&
                   worldItem != null;
        }

        public bool TryGetWorldItemInstanceId(
            Item worldItem,
            out string instanceId)
        {
            instanceId = null;

            if (worldItem == null)
                return false;

            foreach (KeyValuePair<string, Item> pair in
                     _worldItemsByInstanceId)
            {
                if (pair.Value != worldItem)
                    continue;

                instanceId = pair.Key;
                return true;
            }

            return false;
        }

        public bool TryAssignFirstAvailableQuickSlot(
            string instanceId,
            out int slotIndex,
            out string error)
        {
            slotIndex = -1;

            if (!EnsureInitialized(out error))
                return false;

            if (!Grid.TryGetItem(
                    instanceId,
                    out InventoryItemModel item))
            {
                error = $"Inventory item '{instanceId}' was not found.";
                return false;
            }

            if (!item.CanAssignQuickSlot)
            {
                error =
                    $"Inventory item '{instanceId}' cannot be assigned " +
                    "to a quick slot.";

                return false;
            }

            for (int index = 0; index < QuickSlots.SlotCount; index++)
            {
                if (!string.Equals(
                        QuickSlots.GetAssignedInstanceId(index),
                        instanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                slotIndex = index;
                error = null;
                return true;
            }

            for (int index = 0; index < QuickSlots.SlotCount; index++)
            {
                if (QuickSlots.GetAssignedInstanceId(index) != null)
                    continue;

                InventoryOperationResult result =
                    QuickSlots.Assign(index, instanceId);

                if (!result.IsSuccess)
                {
                    error =
                        $"Quick slot assignment failed: " +
                        $"{result.FailureReason}.";

                    return false;
                }

                slotIndex = index;
                error = null;
                return true;
            }

            error = "All inventory quick slots are occupied.";
            return false;
        }

        public InventoryOperationResult DetachWorldItem(
            string instanceId,
            out Item worldItem,
            out string error)
        {
            worldItem = null;

            if (!EnsureInitialized(out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (!TryGetWorldItem(instanceId, out worldItem))
            {
                error =
                    $"No world item is registered for inventory item " +
                    $"'{instanceId}'.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);
            }

            InventoryOperationResult result =
                RemoveInventoryEntry(instanceId);

            if (!result.IsSuccess)
            {
                worldItem = null;
                error =
                    $"Inventory removal failed: {result.FailureReason}.";

                return result;
            }

            _worldItemsByInstanceId.Remove(instanceId);

            if (_storedWorldItemsRoot != null &&
                worldItem.transform.IsChildOf(
                    _storedWorldItemsRoot.transform))
            {
                worldItem.transform.SetParent(null, true);
                worldItem.ToWorldState();
            }

            error = null;
            return result;
        }

        public InventoryOperationResult Move(
            string instanceId,
            InventoryGridPosition targetOrigin)
        {
            return IsInitialized
                ? Grid.TryMove(instanceId, targetOrigin)
                : InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
        }

        public InventoryOperationResult Rotate(string instanceId)
        {
            return IsInitialized
                ? Grid.TryRotate(instanceId)
                : InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
        }

        public InventoryOperationResult Relocate(
            string instanceId,
            InventoryGridPosition targetOrigin,
            InventoryItemRotation targetRotation)
        {
            return IsInitialized
                ? Grid.TryRelocate(
                    instanceId,
                    targetOrigin,
                    targetRotation)
                : InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
        }

        public InventoryOperationResult Remove(string instanceId)
        {
            if (!IsInitialized)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            InventoryOperationResult result =
                RemoveInventoryEntry(instanceId);

            if (result.IsSuccess &&
                _worldItemsByInstanceId.TryGetValue(
                    instanceId,
                    out Item worldItem))
            {
                _worldItemsByInstanceId.Remove(instanceId);
                DestroyUnityObject(worldItem.gameObject);
            }

            return result;
        }

        public InventoryOperationResult AssignQuickSlot(
            int slotIndex,
            string instanceId)
        {
            return IsInitialized
                ? QuickSlots.Assign(slotIndex, instanceId)
                : InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
        }

        public InventoryOperationResult ClearQuickSlot(int slotIndex)
        {
            return IsInitialized
                ? QuickSlots.Clear(slotIndex)
                : InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
        }

        private bool EnsureInitialized(out string error)
        {
            if (IsInitialized)
            {
                error = null;
                return true;
            }

            error = "Inventory runtime controller is not initialized.";
            return false;
        }

        private InventoryOperationResult TryAddWorldItem(
            Item worldItem,
            bool moveToStorage,
            out string instanceId,
            out string error)
        {
            instanceId = null;

            if (worldItem == null)
            {
                error = "World item is not assigned.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (TryGetWorldItemInstanceId(
                    worldItem,
                    out string registeredInstanceId))
            {
                instanceId = registeredInstanceId;
                error =
                    $"World item '{worldItem.name}' is already registered " +
                    $"as inventory item '{registeredInstanceId}'.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.DuplicateInstanceId);
            }

            InventoryOperationResult result = AddFirstAvailable(
                worldItem.ItemData,
                1,
                out string addedInstanceId,
                out error);

            if (!result.IsSuccess)
                return result;

            _worldItemsByInstanceId.Add(
                addedInstanceId,
                worldItem);

            if (moveToStorage)
            {
                worldItem.ToInventoryState();
                worldItem.transform.SetParent(
                    _storedWorldItemsRoot.transform,
                    true);
            }

            instanceId = addedInstanceId;
            error = null;
            return result;
        }

        private InventoryOperationResult RemoveInventoryEntry(
            string instanceId)
        {
            InventoryOperationResult result = Grid.TryRemove(instanceId);

            if (result.IsSuccess)
                _viewResolver.Unregister(instanceId);

            return result;
        }

        private void OnDestroy()
        {
            DisposeRuntime();
        }

        private void DisposeRuntime()
        {
            if (Presenter != null)
                Presenter.Dispose();

            if (QuickSlots != null)
                QuickSlots.Dispose();

            if (_viewResolver != null)
                _viewResolver.Clear();

            _worldItemsByInstanceId.Clear();

            if (_generatedPresentationRoot != null)
                DestroyUnityObject(_generatedPresentationRoot);

            if (_storedWorldItemsRoot != null)
                DestroyUnityObject(_storedWorldItemsRoot);

            Presenter = null;
            QuickSlots = null;
            Grid = null;
            _viewResolver = null;
            _generatedPresentationRoot = null;
            _storedWorldItemsRoot = null;
            IsInitialized = false;
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
    }
}
