using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Presentation;
using Failsafe.Scripts.SaveSystem;
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

        [Header("Item Catalog")]
        [SerializeField] private ItemData[] _itemCatalog =
            Array.Empty<ItemData>();

        public bool IsInitialized { get; private set; }
        public InventoryGridModel Grid { get; private set; }
        public InventoryQuickSlots QuickSlots { get; private set; }
        public InventoryGridPresenter3D Presenter { get; private set; }
        public InventoryQuickBarPresenter3D QuickBarPresenter { get; private set; }
        public int RegisteredWorldItemCount => _worldItemsByInstanceId.Count;
        public bool IsPresentationVisible =>
            _generatedPresentationRoot != null &&
            _generatedPresentationRoot.activeSelf;

        private readonly Dictionary<string, Item> _worldItemsByInstanceId =
            new Dictionary<string, Item>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemData> _itemDataByDefinitionId =
            new Dictionary<string, ItemData>(StringComparer.Ordinal);

        private ItemDataInventoryViewResolver _viewResolver;
        private GameObject _generatedPresentationRoot;
        private GameObject _generatedQuickBarRoot;
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

            if (!TryBuildItemCatalog(out error))
            {
                DisposeRuntime();
                return false;
            }

            Transform parent = _presentationRoot != null
                ? _presentationRoot
                : transform;

            _generatedPresentationRoot = new GameObject("Inventory 3D Views");
            _generatedPresentationRoot.layer = inventoryLayer;
            _generatedPresentationRoot.transform.SetParent(parent, false);

            Presenter = _generatedPresentationRoot
                .AddComponent<InventoryGridPresenter3D>();

            _generatedQuickBarRoot = new GameObject(
                "Inventory Quick Bar 3D");

            _generatedQuickBarRoot.layer = inventoryLayer;
            _generatedQuickBarRoot.transform.SetParent(parent, false);

            QuickBarPresenter = _generatedQuickBarRoot
                .AddComponent<InventoryQuickBarPresenter3D>();

            InventoryGridSpace3D gridSpace = new InventoryGridSpace3D(
                Grid.Columns,
                Grid.Rows,
                _cellSize);

            try
            {
                Presenter.Initialize(
                    Grid,
                    gridSpace,
                    _viewResolver);

                QuickBarPresenter.Initialize(
                    Grid,
                    QuickSlots,
                    gridSpace,
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
            QuickBarPresenter?.SetInventoryOpen(visible);
            return true;
        }

        public bool TryBindRobotPresentationLayout(
            InventoryRobotPresentationLayout3D layout,
            out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            if (layout == null)
            {
                error = "Robot inventory presentation layout is null.";
                return false;
            }

            if (!layout.TryValidate(
                    Grid.Columns,
                    Grid.Rows,
                    QuickSlots.SlotCount,
                    out error))
            {
                return false;
            }

            if (!layout.TryApplyGridPose(
                    _generatedPresentationRoot.transform,
                    Grid.Columns,
                    Grid.Rows,
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

            Presenter.SetManualGridLayout(layout);
            Presenter.SetPrototypeGridVisible(false);
            error = null;
            return true;
        }

        public bool TryBindClosedQuickBarLayout(
            InventoryQuickBarPresentationLayout3D layout,
            out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            if (layout == null)
            {
                error = "Closed quick-bar presentation layout is null.";
                return false;
            }

            return QuickBarPresenter.TrySetExternalClosedLayout(
                layout,
                out error);
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

            if (!TryRegisterCatalogItem(itemData, out error))
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

        public bool TryMoveRegisteredWorldItemToStorage(
            string instanceId,
            out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            if (_storedWorldItemsRoot == null)
            {
                error = "Inventory world-item storage is not initialized.";
                return false;
            }

            if (!TryGetWorldItem(instanceId, out Item worldItem))
            {
                error =
                    $"No world item is registered for inventory item " +
                    $"'{instanceId}'.";

                return false;
            }

            worldItem.ToInventoryState();
            worldItem.transform.SetParent(
                _storedWorldItemsRoot.transform,
                true);

            error = null;
            return true;
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

            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();
            ownership?.Release();

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

        public InventoryOperationResult ConsumeRegisteredWorldItem(
            string instanceId,
            out string error)
        {
            if (!EnsureInitialized(out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (!TryGetWorldItem(instanceId, out Item worldItem))
            {
                error =
                    $"No world item is registered for inventory item " +
                    $"'{instanceId}'.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);
            }

            if (!Grid.TryGetItem(
                    instanceId,
                    out InventoryItemModel inventoryItem))
            {
                error = $"Inventory item '{instanceId}' was not found.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);
            }

            if (inventoryItem.Quantity > 1)
            {
                if (_storedWorldItemsRoot == null)
                {
                    error =
                        "Inventory world-item storage is not initialized.";

                    return InventoryOperationResult.Failure(
                        InventoryFailureReason.InvalidItem,
                        inventoryItem.Quantity);
                }

                InventoryOperationResult quantityResult =
                    Grid.TryRemoveQuantity(instanceId, 1);

                if (!quantityResult.IsSuccess)
                {
                    error =
                        $"Could not consume one unit of inventory item " +
                        $"'{instanceId}': " +
                        $"{quantityResult.FailureReason}.";

                    return quantityResult;
                }

                worldItem.ToInventoryState();
                worldItem.transform.SetParent(
                    _storedWorldItemsRoot.transform,
                    true);

                error = null;
                return quantityResult;
            }

            InventoryOperationResult removeResult = Remove(instanceId);

            error = removeResult.IsSuccess
                ? null
                : $"Could not remove consumed inventory item " +
                  $"'{instanceId}': {removeResult.FailureReason}.";

            return removeResult;
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

            RunPersistentObject persistentObject =
                worldItem.GetComponent<RunPersistentObject>();
            ClaimWorldItem(
                worldItem,
                persistentObject != null
                    ? persistentObject.PersistentId
                    : null,
                runtimeGenerated: false);

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

        private bool TryBuildItemCatalog(out string error)
        {
            _itemDataByDefinitionId.Clear();

            if (_itemCatalog == null)
            {
                error = null;
                return true;
            }

            for (int i = 0; i < _itemCatalog.Length; i++)
            {
                if (!TryRegisterCatalogItem(_itemCatalog[i], out error))
                {
                    error = $"Item catalog entry {i}: {error}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryRegisterCatalogItem(
            ItemData itemData,
            out string error)
        {
            if (!ItemDataInventoryAdapter.TryValidateView(
                    itemData,
                    out error))
            {
                return false;
            }

            string definitionId = itemData.InventoryDefinitionId.Trim();

            if (_itemDataByDefinitionId.TryGetValue(
                    definitionId,
                    out ItemData registeredItemData))
            {
                if (registeredItemData == itemData)
                {
                    error = null;
                    return true;
                }

                error =
                    $"Inventory definition ID '{definitionId}' is used by " +
                    $"both '{registeredItemData.name}' and '{itemData.name}'.";

                return false;
            }

            _itemDataByDefinitionId.Add(definitionId, itemData);
            error = null;
            return true;
        }

        private static bool HasMatchingDefinition(
            Item worldItem,
            ItemData itemData)
        {
            return worldItem != null &&
                   worldItem.ItemData != null &&
                   itemData != null &&
                   string.Equals(
                       worldItem.ItemData.InventoryDefinitionId?.Trim(),
                       itemData.InventoryDefinitionId?.Trim(),
                       StringComparison.Ordinal);
        }

        private static void ClaimWorldItem(
            Item worldItem,
            string sourcePersistentId,
            bool runtimeGenerated)
        {
            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            if (ownership == null)
            {
                ownership = worldItem.gameObject
                    .AddComponent<InventoryWorldItemOwnership>();
            }

            ownership.Claim(sourcePersistentId, runtimeGenerated);
        }

        public bool TryResolveItemData(
            string definitionId,
            out ItemData itemData,
            out string error)
        {
            itemData = null;

            if (!EnsureInitialized(out error))
                return false;

            string normalizedId = definitionId?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                error = "Inventory definition ID cannot be empty.";
                return false;
            }

            if (!_itemDataByDefinitionId.TryGetValue(
                    normalizedId,
                    out itemData) ||
                itemData == null)
            {
                error =
                    $"Inventory ItemData catalog has no definition " +
                    $"'{normalizedId}'.";

                return false;
            }

            error = null;
            return true;
        }

        public bool TryGetItemDataForInstance(
            string instanceId,
            out ItemData itemData)
        {
            itemData = null;

            return IsInitialized &&
                   _viewResolver != null &&
                   _viewResolver.TryGetItemData(instanceId, out itemData);
        }

        public InventoryOperationResult RestoreItem(
            ItemData itemData,
            string instanceId,
            int quantity,
            InventoryGridPosition origin,
            InventoryItemRotation rotation,
            Item worldItem,
            bool runtimeGeneratedWorldItem,
            string sourcePersistentId,
            out string error)
        {
            if (!EnsureInitialized(out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (!TryRegisterCatalogItem(itemData, out error) ||
                !ItemDataInventoryAdapter.TryCreateModel(
                    itemData,
                    instanceId,
                    quantity,
                    out InventoryItemModel item,
                    out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (worldItem != null &&
                !HasMatchingDefinition(worldItem, itemData))
            {
                error =
                    $"World item '{worldItem.name}' does not match " +
                    $"inventory definition '{itemData.InventoryDefinitionId}'.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    quantity);
            }

            if (!_viewResolver.TryRegister(instanceId, itemData, out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    quantity);
            }

            InventoryOperationResult placementResult = Grid.TryPlace(
                item,
                origin,
                rotation);

            if (!placementResult.IsSuccess)
            {
                _viewResolver.Unregister(instanceId);
                error =
                    $"Saved inventory placement failed: " +
                    $"{placementResult.FailureReason}.";

                return placementResult;
            }

            if (!Presenter.TryGetView(instanceId, out _))
            {
                Grid.TryRemove(instanceId);
                _viewResolver.Unregister(instanceId);
                error =
                    "The restored inventory item was placed, but its " +
                    "3D view could not be created.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem,
                    quantity);
            }

            if (worldItem != null)
            {
                _worldItemsByInstanceId.Add(instanceId, worldItem);
                ClaimWorldItem(
                    worldItem,
                    sourcePersistentId,
                    runtimeGeneratedWorldItem);
                worldItem.gameObject.SetActive(true);
                worldItem.ToInventoryState();
                worldItem.transform.SetParent(
                    _storedWorldItemsRoot.transform,
                    true);
            }

            error = null;
            return placementResult;
        }

        public bool TryClearForRestore(out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            List<string> instanceIds = new List<string>();

            foreach (InventoryPlacement placement in Grid.Placements)
                instanceIds.Add(placement.Item.InstanceId);

            for (int i = 0; i < instanceIds.Count; i++)
            {
                string instanceId = instanceIds[i];

                if (_worldItemsByInstanceId.TryGetValue(
                        instanceId,
                        out Item worldItem) &&
                    worldItem != null)
                {
                    _worldItemsByInstanceId.Remove(instanceId);
                    InventoryWorldItemOwnership ownership =
                        worldItem.GetComponent<InventoryWorldItemOwnership>();

                    if (ownership != null &&
                        !ownership.IsRuntimeGenerated &&
                        !string.IsNullOrWhiteSpace(
                            ownership.SourcePersistentId))
                    {
                        ownership.Release();
                        worldItem.transform.SetParent(null, true);
                        worldItem.gameObject.SetActive(true);
                        worldItem.ToWorldState();
                    }
                    else
                    {
                        DestroyUnityObject(worldItem.gameObject);
                    }
                }

                InventoryOperationResult removeResult =
                    RemoveInventoryEntry(instanceId);

                if (!removeResult.IsSuccess)
                {
                    error =
                        $"Could not clear inventory item '{instanceId}': " +
                        $"{removeResult.FailureReason}.";

                    return false;
                }
            }

            error = null;
            return true;
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
            if (QuickBarPresenter != null)
                QuickBarPresenter.Dispose();

            if (Presenter != null)
                Presenter.Dispose();

            if (QuickSlots != null)
                QuickSlots.Dispose();

            if (_viewResolver != null)
                _viewResolver.Clear();

            _worldItemsByInstanceId.Clear();
            _itemDataByDefinitionId.Clear();

            if (_generatedPresentationRoot != null)
                DestroyUnityObject(_generatedPresentationRoot);

            if (_generatedQuickBarRoot != null)
                DestroyUnityObject(_generatedQuickBarRoot);

            if (_storedWorldItemsRoot != null)
                DestroyUnityObject(_storedWorldItemsRoot);

            Presenter = null;
            QuickBarPresenter = null;
            QuickSlots = null;
            Grid = null;
            _viewResolver = null;
            _generatedPresentationRoot = null;
            _generatedQuickBarRoot = null;
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
