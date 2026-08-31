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

        [Header("Item Catalog")]
        [SerializeField] private ItemData[] _itemCatalog =
            Array.Empty<ItemData>();

        public bool IsInitialized { get; private set; }
        public InventoryGridModel Grid { get; private set; }
        public InventoryQuickSlots QuickSlots { get; private set; }
        public InventoryGridPresenter3D Presenter =>
            _runtimePresentation.GridPresenter;
        public InventoryQuickBarPresenter3D QuickBarPresenter =>
            _runtimePresentation.QuickBarPresenter;
        public int RegisteredWorldItemCount => _worldItems.Count;
        public bool IsPresentationVisible =>
            _runtimePresentation.IsVisible;

        private readonly InventoryItemCatalog _catalog =
            new InventoryItemCatalog();
        private readonly InventoryRuntimePresentation _runtimePresentation =
            new InventoryRuntimePresentation();
        private readonly InventoryWorldItemRegistry _worldItems =
            new InventoryWorldItemRegistry();

        private ItemDataInventoryViewResolver _viewResolver;

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

            if (!_catalog.TryBuild(_itemCatalog, out error))
            {
                DisposeRuntime();
                return false;
            }

            if (!_runtimePresentation.TryInitialize(
                    transform,
                    _presentationRoot,
                    inventoryLayer,
                    _cellSize,
                    Grid,
                    QuickSlots,
                    _viewResolver,
                    out error))
            {
                DisposeRuntime();
                return false;
            }

            if (!_worldItems.TryInitialize(transform, out error))
            {
                DisposeRuntime();
                return false;
            }

            IsInitialized = true;
            error = null;
            return true;
        }

        public bool SetPresentationVisible(bool visible)
        {
            return IsInitialized &&
                   _runtimePresentation.SetVisible(visible);
        }

        public bool TryBindRobotPresentationLayout(
            InventoryRobotPresentationLayout3D layout,
            out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            return _runtimePresentation.TryBindRobotLayout(
                layout,
                out error);
        }

        public bool TryBindClosedQuickBarLayout(
            InventoryQuickBarPresentationLayout3D layout,
            out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            return _runtimePresentation.TryBindClosedQuickBarLayout(
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

            if (!_catalog.TryRegister(itemData, out error))
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
                runtimeGenerated: false,
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
                runtimeGenerated: false,
                out instanceId,
                out error);
        }

        public InventoryOperationResult CreateAndStoreRuntimeItem(
            ItemData itemData,
            out string instanceId,
            out string error)
        {
            instanceId = null;

            if (!EnsureInitialized(out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (itemData == null)
            {
                error = "Runtime inventory ItemData is not assigned.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (itemData.WorldItemPrefab == null)
            {
                error =
                    $"ItemData '{itemData.name}' has no World Item Prefab.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            Item runtimeItem = Instantiate(itemData.WorldItemPrefab);
            runtimeItem.name =
                $"{itemData.WorldItemPrefab.name} (Starting Item)";

            if (!InventoryWorldItemRegistry.HasMatchingDefinition(
                    runtimeItem,
                    itemData))
            {
                error =
                    $"World Item Prefab '{itemData.WorldItemPrefab.name}' " +
                    $"does not use ItemData '{itemData.name}'.";
                InventoryWorldItemRegistry.Destroy(runtimeItem);

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            InventoryOperationResult result = TryAddWorldItem(
                runtimeItem,
                moveToStorage: true,
                runtimeGenerated: true,
                out instanceId,
                out error);

            if (!result.IsSuccess && runtimeItem != null)
                InventoryWorldItemRegistry.Destroy(runtimeItem);

            return result;
        }

        public bool TryGetWorldItem(
            string instanceId,
            out Item worldItem)
        {
            return _worldItems.TryGet(instanceId, out worldItem);
        }

        public bool TryGetWorldItemInstanceId(
            Item worldItem,
            out string instanceId)
        {
            return _worldItems.TryGetInstanceId(
                worldItem,
                out instanceId);
        }

        public bool TryMoveRegisteredWorldItemToStorage(
            string instanceId,
            out string error)
        {
            if (!EnsureInitialized(out error))
                return false;

            return _worldItems.TryMoveToStorage(instanceId, out error);
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

            if (!Grid.TryGetItem(
                    instanceId,
                    out InventoryItemModel inventoryItem))
            {
                worldItem = null;
                error = $"Inventory item '{instanceId}' was not found.";

                return InventoryOperationResult.Failure(
                    InventoryFailureReason.ItemNotFound);
            }

            if (inventoryItem.Quantity > 1)
            {
                if (!_worldItems.TryCreateStoredStackRepresentative(
                        worldItem.ItemData,
                        out Item replacement,
                        out error))
                {
                    worldItem = null;

                    return InventoryOperationResult.Failure(
                        InventoryFailureReason.InvalidItem,
                        inventoryItem.Quantity);
                }

                InventoryOperationResult quantityResult =
                    Grid.TryRemoveQuantity(instanceId, 1);

                if (!quantityResult.IsSuccess)
                {
                    InventoryWorldItemRegistry.Destroy(replacement);
                    worldItem = null;
                    error =
                        $"Inventory quantity removal failed: " +
                        $"{quantityResult.FailureReason}.";

                    return quantityResult;
                }

                _worldItems.Replace(instanceId, replacement);
                _worldItems.ReleaseToWorldIfStored(worldItem);

                error = null;
                return quantityResult;
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

            _worldItems.Remove(instanceId, out _);
            _worldItems.ReleaseToWorldIfStored(worldItem);

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
                if (!_worldItems.TryEnsureStorage(out error))
                {
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

                _worldItems.Store(worldItem);

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

        public InventoryOperationResult ValidateMergeStacks(
            string sourceInstanceId,
            string targetInstanceId)
        {
            return IsInitialized
                ? Grid.ValidateMerge(sourceInstanceId, targetInstanceId)
                : InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
        }

        public InventoryOperationResult MergeStacks(
            string sourceInstanceId,
            string targetInstanceId,
            bool preferSourceWorldItem = false)
        {
            if (!IsInitialized)
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            InventoryOperationResult validation = Grid.ValidateMerge(
                sourceInstanceId,
                targetInstanceId);

            if (!validation.IsSuccess)
                return validation;

            int sourceQuickSlot = FindAssignedQuickSlot(sourceInstanceId);
            int targetQuickSlot = FindAssignedQuickSlot(targetInstanceId);

            _worldItems.TryGet(
                sourceInstanceId,
                out Item sourceWorldItem);

            _worldItems.TryGet(
                targetInstanceId,
                out Item targetWorldItem);

            InventoryOperationResult result = Grid.TryMerge(
                sourceInstanceId,
                targetInstanceId);

            if (!result.IsSuccess || result.RemainingQuantity > 0)
                return result;

            _viewResolver.Unregister(sourceInstanceId);
            _worldItems.Remove(sourceInstanceId, out _);

            bool useSourceWorldItem =
                sourceWorldItem != null &&
                (preferSourceWorldItem || targetWorldItem == null);

            if (useSourceWorldItem)
            {
                _worldItems.Replace(targetInstanceId, sourceWorldItem);

                if (targetWorldItem != null &&
                    targetWorldItem != sourceWorldItem)
                {
                    _worldItems.RetireMerged(targetWorldItem);
                }
            }
            else if (sourceWorldItem != null &&
                     sourceWorldItem != targetWorldItem)
            {
                _worldItems.RetireMerged(sourceWorldItem);
            }

            if (sourceQuickSlot >= 0 && targetQuickSlot < 0)
                QuickSlots.Assign(sourceQuickSlot, targetInstanceId);

            return result;
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
                _worldItems.Remove(instanceId, out Item worldItem))
            {
                InventoryWorldItemRegistry.Destroy(worldItem);
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
            bool runtimeGenerated,
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

            if (!_catalog.TryRegister(worldItem.ItemData, out error))
            {
                return InventoryOperationResult.Failure(
                    InventoryFailureReason.InvalidItem);
            }

            if (TryFindAvailableStack(
                    worldItem.ItemData,
                    out InventoryItemModel availableStack))
            {
                InventoryOperationResult stackResult = Grid.TryAddQuantity(
                    availableStack.InstanceId,
                    1);

                if (!stackResult.IsSuccess)
                {
                    error =
                        $"Inventory stack update failed: " +
                        $"{stackResult.FailureReason}.";

                    return stackResult;
                }

                TryGetWorldItem(
                    availableStack.InstanceId,
                    out Item existingWorldItem);

                if (!moveToStorage || existingWorldItem == null)
                {
                    _worldItems.Register(
                        availableStack.InstanceId,
                        worldItem,
                        runtimeGenerated);

                    if (moveToStorage)
                        _worldItems.Store(worldItem);

                    if (existingWorldItem != null &&
                        existingWorldItem != worldItem)
                    {
                        _worldItems.RetireMerged(existingWorldItem);
                    }
                }
                else
                {
                    InventoryWorldItemRegistry.Claim(
                        worldItem,
                        runtimeGenerated);

                    _worldItems.RetireMerged(worldItem);
                }

                instanceId = availableStack.InstanceId;
                error = null;
                return stackResult;
            }

            InventoryOperationResult result = AddFirstAvailable(
                worldItem.ItemData,
                1,
                out string addedInstanceId,
                out error);

            if (!result.IsSuccess)
                return result;

            _worldItems.Register(
                addedInstanceId,
                worldItem,
                runtimeGenerated);

            if (moveToStorage)
                _worldItems.Store(worldItem);

            instanceId = addedInstanceId;
            error = null;
            return result;
        }

        private bool TryFindAvailableStack(
            ItemData itemData,
            out InventoryItemModel stack)
        {
            stack = null;

            if (itemData == null ||
                itemData.InventoryMaxStack <= 1 ||
                string.IsNullOrWhiteSpace(
                    itemData.InventoryDefinitionId))
            {
                return false;
            }

            string definitionId = itemData.InventoryDefinitionId.Trim();

            foreach (InventoryPlacement placement in Grid.Placements)
            {
                InventoryItemModel item = placement.Item;

                if (item.MaxStack <= 1 ||
                    item.Quantity >= item.MaxStack ||
                    !string.Equals(
                        item.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                stack = item;
                return true;
            }

            return false;
        }

        private int FindAssignedQuickSlot(string instanceId)
        {
            if (QuickSlots == null || string.IsNullOrWhiteSpace(instanceId))
                return -1;

            for (int index = 0; index < QuickSlots.SlotCount; index++)
            {
                if (string.Equals(
                        QuickSlots.GetAssignedInstanceId(index),
                        instanceId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        public bool TryResolveItemData(
            string definitionId,
            out ItemData itemData,
            out string error)
        {
            if (!EnsureInitialized(out error))
            {
                itemData = null;
                return false;
            }

            return _catalog.TryResolve(
                definitionId,
                out itemData,
                out error);
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

            if (!_catalog.TryRegister(itemData, out error) ||
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
                !InventoryWorldItemRegistry.HasMatchingDefinition(
                    worldItem,
                    itemData))
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
                _worldItems.AddRestored(
                    instanceId,
                    worldItem,
                    sourcePersistentId,
                    runtimeGeneratedWorldItem);
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

                if (_worldItems.Remove(
                        instanceId,
                        out Item worldItem))
                {
                    _worldItems.ReleaseOrDestroyForRestore(worldItem);
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
            _runtimePresentation.Dispose();

            if (QuickSlots != null)
                QuickSlots.Dispose();

            if (_viewResolver != null)
                _viewResolver.Clear();

            _worldItems.Dispose();
            _catalog.Clear();
            QuickSlots = null;
            Grid = null;
            _viewResolver = null;
            IsInitialized = false;
        }
    }
}
