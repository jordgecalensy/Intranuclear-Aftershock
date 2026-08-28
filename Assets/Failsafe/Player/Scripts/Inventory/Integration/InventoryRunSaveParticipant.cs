using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Failsafe.Inventory.Core;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Inventory.Integration
{
    public sealed class InventoryRunSaveParticipant :
        IRunSaveParticipant,
        IInitializable,
        IDisposable
    {
        public const string Id = RunSaveParticipantIds.Inventory;

        private const int InventoryRestoreOrder = 600;
        private const string LogCategory = "INVENTORY";

        private readonly InventoryRuntimeController _inventory;
        private readonly InventoryQuickSlotEquipService _equipService;
        private readonly RunSaveParticipantRegistry _participantRegistry;

        private IDisposable _registration;

        public string ParticipantId => Id;
        public int RestoreOrder => InventoryRestoreOrder;

        public InventoryRunSaveParticipant(
            InventoryRuntimeController inventory,
            InventoryQuickSlotEquipService equipService,
            RunSaveParticipantRegistry participantRegistry)
        {
            _inventory = inventory ??
                throw new ArgumentNullException(nameof(inventory));
            _equipService = equipService ??
                throw new ArgumentNullException(nameof(equipService));
            _participantRegistry = participantRegistry ??
                throw new ArgumentNullException(nameof(participantRegistry));
        }

        public void Initialize()
        {
            if (_registration != null)
                return;

            _registration = _participantRegistry.Register(this);
        }

        public void Dispose()
        {
            _registration?.Dispose();
            _registration = null;
        }

        public void Capture(RunCheckpointData checkpoint)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            EnsureInventoryInitialized();

            InventoryStateData state = new InventoryStateData
            {
                hasState = true,
                activeQuickSlotIndex = _equipService.ActiveSlotIndex
            };

            for (int slotIndex = 0;
                 slotIndex < _inventory.QuickSlots.SlotCount;
                 slotIndex++)
            {
                state.quickSlotInstanceIds.Add(
                    _inventory.QuickSlots.GetAssignedInstanceId(slotIndex));
            }

            foreach (InventoryPlacement placement in
                     _inventory.Grid.Placements)
            {
                InventoryItemModel item = placement.Item;

                if (!_inventory.TryGetItemDataForInstance(
                        item.InstanceId,
                        out ItemData itemData) ||
                    itemData == null)
                {
                    throw new InvalidOperationException(
                        $"Inventory item '{item.InstanceId}' has no " +
                        "registered ItemData.");
                }

                InventoryItemStateData itemState =
                    new InventoryItemStateData
                    {
                        itemId = item.DefinitionId,
                        instanceId = item.InstanceId,
                        quantity = item.Quantity,
                        row = placement.Origin.Row,
                        column = placement.Origin.Column,
                        rotation = (int)item.Rotation
                    };

                CaptureWorldItemState(itemState);
                state.items.Add(itemState);
            }

            state.items.Sort(CompareItemStates);
            checkpoint.inventory = state;

            RunSaveLog.Info(
                LogCategory,
                $"Captured {state.items.Count} inventory items, " +
                $"{state.quickSlotInstanceIds.Count} quick slots and " +
                $"active slot {state.activeQuickSlotIndex}.");
        }

        public UniTask RestoreAsync(
            RunCheckpointData checkpoint,
            RunLoadContext context)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            InventoryStateData state = checkpoint.inventory;

            if (state == null || !state.hasState)
            {
                RunSaveLog.Info(
                    LogCategory,
                    "Checkpoint has no inventory state; current defaults were kept.");

                return UniTask.CompletedTask;
            }

            RestorePlan plan = BuildRestorePlan(state);

            if (!_equipService.TryPrepareForRestore(out string error))
            {
                throw new InvalidOperationException(
                    $"Could not prepare player hands for inventory " +
                    $"restore: {error}");
            }

            if (!_inventory.TryClearForRestore(out error))
            {
                throw new InvalidOperationException(
                    $"Could not clear the current inventory before " +
                    $"restore: {error}");
            }

            try
            {
                RestoreItems(plan);
                RestoreQuickSlots(plan);

                if (!_equipService.TryRestoreActiveSlot(
                        plan.ActiveQuickSlotIndex,
                        out error))
                {
                    throw new InvalidOperationException(
                        $"Could not restore active quick slot: {error}");
                }
            }
            catch
            {
                _inventory.TryClearForRestore(out _);
                throw;
            }

            RunSaveLog.Info(
                LogCategory,
                $"Restored {plan.Items.Count} inventory items and " +
                $"active slot {plan.ActiveQuickSlotIndex}.");

            return UniTask.CompletedTask;
        }

        private void CaptureWorldItemState(
            InventoryItemStateData itemState)
        {
            if (!_inventory.TryGetWorldItem(
                    itemState.instanceId,
                    out Item worldItem))
            {
                return;
            }

            InventoryWorldItemOwnership ownership =
                worldItem.GetComponent<InventoryWorldItemOwnership>();

            if (ownership == null || !ownership.IsInventoryOwned)
            {
                throw new InvalidOperationException(
                    $"World item '{worldItem.name}' is registered in the " +
                    "inventory but has no active inventory ownership marker.");
            }

            if (!ownership.IsRuntimeGenerated &&
                string.IsNullOrWhiteSpace(ownership.SourcePersistentId))
            {
                throw new InvalidOperationException(
                    $"Scene pickup '{worldItem.name}' cannot be saved in " +
                    "the inventory until RunPersistentObject is placed on " +
                    "the same GameObject as Item and has a stable ID.");
            }

            itemState.hasWorldItem = true;
            itemState.runtimeGeneratedWorldItem =
                ownership.IsRuntimeGenerated;
            itemState.worldSourcePersistentId =
                ownership.SourcePersistentId;
            itemState.energy = worldItem.EnergyAmountCurrent;
        }

        private RestorePlan BuildRestorePlan(InventoryStateData state)
        {
            EnsureInventoryInitialized();
            state.EnsureInitialized();

            if (state.quickSlotInstanceIds.Count !=
                _inventory.QuickSlots.SlotCount)
            {
                throw new InvalidOperationException(
                    $"Checkpoint contains " +
                    $"{state.quickSlotInstanceIds.Count} quick slots, but " +
                    $"the inventory requires " +
                    $"{_inventory.QuickSlots.SlotCount}.");
            }

            RestorePlan plan = new RestorePlan(
                state.activeQuickSlotIndex,
                new List<string>(state.quickSlotInstanceIds));
            InventoryGridModel validationGrid =
                new InventoryGridModel(
                    _inventory.Grid.Columns,
                    _inventory.Grid.Rows);
            HashSet<string> instanceIds =
                new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, RestoreItemPlan> itemsByInstanceId =
                new Dictionary<string, RestoreItemPlan>(
                    StringComparer.Ordinal);
            Dictionary<string, RunPersistentObject> persistentObjects =
                IndexPersistentObjects();

            for (int i = 0; i < state.items.Count; i++)
            {
                RestoreItemPlan itemPlan = BuildItemPlan(
                    state.items[i],
                    i,
                    validationGrid,
                    instanceIds,
                    persistentObjects);

                plan.Items.Add(itemPlan);
                itemsByInstanceId.Add(
                    itemPlan.InstanceId,
                    itemPlan);
            }

            ValidateQuickSlots(plan, itemsByInstanceId);
            return plan;
        }

        private RestoreItemPlan BuildItemPlan(
            InventoryItemStateData state,
            int savedIndex,
            InventoryGridModel validationGrid,
            HashSet<string> instanceIds,
            Dictionary<string, RunPersistentObject> persistentObjects)
        {
            if (state == null)
            {
                throw new InvalidOperationException(
                    $"Inventory item at save index {savedIndex} is null.");
            }

            string instanceId = state.instanceId?.Trim();

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new InvalidOperationException(
                    $"Inventory item at save index {savedIndex} has no " +
                    "instance ID.");
            }

            if (!instanceIds.Add(instanceId))
            {
                throw new InvalidOperationException(
                    $"Inventory instance ID '{instanceId}' occurs more " +
                    "than once in the checkpoint.");
            }

            if (!_inventory.TryResolveItemData(
                    state.itemId,
                    out ItemData itemData,
                    out string error))
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' cannot resolve its " +
                    $"definition: {error}");
            }

            if (!ItemDataInventoryAdapter.TryCreateModel(
                    itemData,
                    instanceId,
                    state.quantity,
                    out InventoryItemModel itemModel,
                    out error))
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' is invalid: {error}");
            }

            if (state.rotation != (int)InventoryItemRotation.Default &&
                state.rotation !=
                (int)InventoryItemRotation.Clockwise90)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' has unsupported " +
                    $"rotation value {state.rotation}.");
            }

            InventoryItemRotation rotation =
                (InventoryItemRotation)state.rotation;
            InventoryGridPosition origin =
                new InventoryGridPosition(state.column, state.row);
            InventoryOperationResult placementResult =
                validationGrid.TryPlace(
                    itemModel,
                    origin,
                    rotation);

            if (!placementResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' has invalid saved " +
                    $"placement {origin}: " +
                    $"{placementResult.FailureReason}.");
            }

            int energyAmount = ValidateSavedEnergy(
                state,
                instanceId,
                itemData);
            Item sourceWorldItem = ResolveSourceWorldItem(
                state,
                instanceId,
                itemData,
                persistentObjects);

            return new RestoreItemPlan(
                itemData,
                instanceId,
                state.quantity,
                origin,
                rotation,
                energyAmount,
                state.hasWorldItem,
                state.runtimeGeneratedWorldItem,
                state.worldSourcePersistentId?.Trim(),
                sourceWorldItem);
        }

        private static int ValidateSavedEnergy(
            InventoryItemStateData state,
            string instanceId,
            ItemData itemData)
        {
            if (float.IsNaN(state.energy) ||
                float.IsInfinity(state.energy))
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' has a non-finite " +
                    "energy value.");
            }

            int energyAmount = Mathf.RoundToInt(state.energy);

            if (!Mathf.Approximately(state.energy, energyAmount))
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' has non-integer " +
                    $"energy value {state.energy}.");
            }

            int maximum = itemData.UsesEnergy
                ? Mathf.Max(0, itemData.EnergyAmountMax)
                : 0;

            if (energyAmount < 0 || energyAmount > maximum)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' has energy " +
                    $"{energyAmount}, expected a value between 0 and " +
                    $"{maximum}.");
            }

            if (!state.hasWorldItem && energyAmount != 0)
            {
                throw new InvalidOperationException(
                    $"Inventory item '{instanceId}' has saved energy but " +
                    "no world item that can own this runtime state.");
            }

            return energyAmount;
        }

        private static Item ResolveSourceWorldItem(
            InventoryItemStateData state,
            string instanceId,
            ItemData itemData,
            Dictionary<string, RunPersistentObject> persistentObjects)
        {
            string sourcePersistentId =
                state.worldSourcePersistentId?.Trim();

            if (!state.hasWorldItem)
            {
                if (state.runtimeGeneratedWorldItem ||
                    !string.IsNullOrWhiteSpace(sourcePersistentId))
                {
                    throw new InvalidOperationException(
                        $"Inventory item '{instanceId}' has world origin " +
                        "metadata but is not marked as a world item.");
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(sourcePersistentId))
            {
                if (state.runtimeGeneratedWorldItem)
                {
                    throw new InvalidOperationException(
                        $"Inventory item '{instanceId}' is marked both as " +
                        "runtime-generated and scene-persistent.");
                }

                if (!persistentObjects.TryGetValue(
                        sourcePersistentId,
                        out RunPersistentObject persistentObject))
                {
                    throw new InvalidOperationException(
                        $"Inventory source object '{sourcePersistentId}' " +
                        $"for item '{instanceId}' is missing from the " +
                        "loaded scene.");
                }

                Item sourceItem = persistentObject.GetComponent<Item>();

                if (sourceItem == null)
                {
                    throw new InvalidOperationException(
                        $"RunPersistentObject '{sourcePersistentId}' must " +
                        "be on the same GameObject as Item.");
                }

                ValidateWorldItemDefinition(
                    sourceItem,
                    itemData,
                    instanceId,
                    "scene source");

                return sourceItem;
            }

            if (!state.runtimeGeneratedWorldItem)
            {
                throw new InvalidOperationException(
                    $"Inventory world item '{instanceId}' has no stable " +
                    "scene source and is not marked runtime-generated.");
            }

            if (itemData.WorldItemPrefab == null)
            {
                throw new InvalidOperationException(
                    $"ItemData '{itemData.name}' needs World Item Prefab " +
                    $"to restore runtime-generated inventory item " +
                    $"'{instanceId}'.");
            }

            ValidateWorldItemDefinition(
                itemData.WorldItemPrefab,
                itemData,
                instanceId,
                "World Item Prefab");

            return null;
        }

        private static void ValidateWorldItemDefinition(
            Item worldItem,
            ItemData expectedItemData,
            string instanceId,
            string sourceLabel)
        {
            string actualId =
                worldItem != null && worldItem.ItemData != null
                    ? worldItem.ItemData.InventoryDefinitionId?.Trim()
                    : null;
            string expectedId =
                expectedItemData.InventoryDefinitionId?.Trim();

            if (!string.Equals(
                    actualId,
                    expectedId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{sourceLabel} for inventory item '{instanceId}' " +
                    $"uses definition '{actualId ?? "<missing>"}' " +
                    $"instead of '{expectedId}'.");
            }
        }

        private static Dictionary<string, RunPersistentObject>
            IndexPersistentObjects()
        {
            RunPersistentObject[] runtimeObjects =
                UnityEngine.Object.FindObjectsByType<RunPersistentObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Dictionary<string, RunPersistentObject> result =
                new Dictionary<string, RunPersistentObject>(
                    StringComparer.Ordinal);

            for (int i = 0; i < runtimeObjects.Length; i++)
            {
                RunPersistentObject runtimeObject = runtimeObjects[i];
                string persistentId = runtimeObject.PersistentId?.Trim();

                if (string.IsNullOrWhiteSpace(persistentId))
                {
                    throw new InvalidOperationException(
                        $"Persistent object '{runtimeObject.name}' has an " +
                        "empty ID.");
                }

                if (!result.TryAdd(persistentId, runtimeObject))
                {
                    throw new InvalidOperationException(
                        $"Persistent object ID '{persistentId}' occurs " +
                        "more than once in the loaded scene.");
                }
            }

            return result;
        }

        private static void ValidateQuickSlots(
            RestorePlan plan,
            Dictionary<string, RestoreItemPlan> itemsByInstanceId)
        {
            HashSet<string> assignedInstanceIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int slotIndex = 0;
                 slotIndex < plan.QuickSlotInstanceIds.Count;
                 slotIndex++)
            {
                string instanceId =
                    plan.QuickSlotInstanceIds[slotIndex]?.Trim();
                plan.QuickSlotInstanceIds[slotIndex] = instanceId;

                if (string.IsNullOrWhiteSpace(instanceId))
                    continue;

                if (!itemsByInstanceId.TryGetValue(
                        instanceId,
                        out RestoreItemPlan itemPlan))
                {
                    throw new InvalidOperationException(
                        $"Quick slot {slotIndex + 1} points to missing " +
                        $"inventory item '{instanceId}'.");
                }

                if (!itemPlan.ItemData.CanAssignQuickSlot)
                {
                    throw new InvalidOperationException(
                        $"Inventory item '{instanceId}' cannot be " +
                        $"assigned to quick slot {slotIndex + 1}.");
                }

                if (!assignedInstanceIds.Add(instanceId))
                {
                    throw new InvalidOperationException(
                        $"Inventory item '{instanceId}' is assigned to " +
                        "more than one quick slot.");
                }
            }

            if (plan.ActiveQuickSlotIndex ==
                InventoryQuickSlotEquipService.NoActiveSlot)
            {
                return;
            }

            if (plan.ActiveQuickSlotIndex < 0 ||
                plan.ActiveQuickSlotIndex >=
                plan.QuickSlotInstanceIds.Count)
            {
                throw new InvalidOperationException(
                    $"Active quick-slot index " +
                    $"{plan.ActiveQuickSlotIndex} is out of range.");
            }

            string activeInstanceId =
                plan.QuickSlotInstanceIds[plan.ActiveQuickSlotIndex];

            if (string.IsNullOrWhiteSpace(activeInstanceId))
            {
                throw new InvalidOperationException(
                    $"Active quick slot " +
                    $"{plan.ActiveQuickSlotIndex + 1} is empty.");
            }

            if (!itemsByInstanceId[activeInstanceId].HasWorldItem)
            {
                throw new InvalidOperationException(
                    $"Active quick-slot item '{activeInstanceId}' has no " +
                    "world object and cannot be restored to the hand.");
            }
        }

        private void RestoreItems(RestorePlan plan)
        {
            for (int i = 0; i < plan.Items.Count; i++)
            {
                RestoreItemPlan itemPlan = plan.Items[i];
                Item worldItem = itemPlan.SourceWorldItem;
                bool instantiated = false;

                if (itemPlan.HasWorldItem && worldItem == null)
                {
                    worldItem = UnityEngine.Object.Instantiate(
                        itemPlan.ItemData.WorldItemPrefab);
                    worldItem.name =
                        $"{itemPlan.ItemData.WorldItemPrefab.name} " +
                        "(Inventory Restored)";
                    instantiated = true;
                }

                if (worldItem != null &&
                    !worldItem.TryRestoreEnergy(
                        itemPlan.EnergyAmount,
                        out string error))
                {
                    if (instantiated)
                        UnityEngine.Object.Destroy(worldItem.gameObject);

                    throw new InvalidOperationException(error);
                }

                InventoryOperationResult result = _inventory.RestoreItem(
                    itemPlan.ItemData,
                    itemPlan.InstanceId,
                    itemPlan.Quantity,
                    itemPlan.Origin,
                    itemPlan.Rotation,
                    worldItem,
                    itemPlan.RuntimeGeneratedWorldItem,
                    itemPlan.SourcePersistentId,
                    out error);

                if (result.IsSuccess)
                    continue;

                if (instantiated && worldItem != null)
                    UnityEngine.Object.Destroy(worldItem.gameObject);

                throw new InvalidOperationException(
                    $"Could not restore inventory item " +
                    $"'{itemPlan.InstanceId}': {error}");
            }
        }

        private void RestoreQuickSlots(RestorePlan plan)
        {
            for (int slotIndex = 0;
                 slotIndex < plan.QuickSlotInstanceIds.Count;
                 slotIndex++)
            {
                string instanceId =
                    plan.QuickSlotInstanceIds[slotIndex];

                if (string.IsNullOrWhiteSpace(instanceId))
                    continue;

                InventoryOperationResult result =
                    _inventory.AssignQuickSlot(slotIndex, instanceId);

                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Could not restore quick slot {slotIndex + 1}: " +
                        $"{result.FailureReason}.");
                }
            }
        }

        private void EnsureInventoryInitialized()
        {
            if (_inventory.IsInitialized)
                return;

            if (!_inventory.TryInitialize(out string error))
            {
                throw new InvalidOperationException(
                    $"Inventory runtime is not initialized: {error}");
            }
        }

        private static int CompareItemStates(
            InventoryItemStateData left,
            InventoryItemStateData right)
        {
            int rowComparison = left.row.CompareTo(right.row);

            if (rowComparison != 0)
                return rowComparison;

            int columnComparison = left.column.CompareTo(right.column);

            return columnComparison != 0
                ? columnComparison
                : string.CompareOrdinal(left.instanceId, right.instanceId);
        }

        private sealed class RestorePlan
        {
            public int ActiveQuickSlotIndex { get; }
            public List<string> QuickSlotInstanceIds { get; }
            public List<RestoreItemPlan> Items { get; } =
                new List<RestoreItemPlan>();

            public RestorePlan(
                int activeQuickSlotIndex,
                List<string> quickSlotInstanceIds)
            {
                ActiveQuickSlotIndex = activeQuickSlotIndex;
                QuickSlotInstanceIds = quickSlotInstanceIds;
            }
        }

        private sealed class RestoreItemPlan
        {
            public ItemData ItemData { get; }
            public string InstanceId { get; }
            public int Quantity { get; }
            public InventoryGridPosition Origin { get; }
            public InventoryItemRotation Rotation { get; }
            public int EnergyAmount { get; }
            public bool HasWorldItem { get; }
            public bool RuntimeGeneratedWorldItem { get; }
            public string SourcePersistentId { get; }
            public Item SourceWorldItem { get; }

            public RestoreItemPlan(
                ItemData itemData,
                string instanceId,
                int quantity,
                InventoryGridPosition origin,
                InventoryItemRotation rotation,
                int energyAmount,
                bool hasWorldItem,
                bool runtimeGeneratedWorldItem,
                string sourcePersistentId,
                Item sourceWorldItem)
            {
                ItemData = itemData;
                InstanceId = instanceId;
                Quantity = quantity;
                Origin = origin;
                Rotation = rotation;
                EnergyAmount = energyAmount;
                HasWorldItem = hasWorldItem;
                RuntimeGeneratedWorldItem =
                    runtimeGeneratedWorldItem;
                SourcePersistentId = sourcePersistentId;
                SourceWorldItem = sourceWorldItem;
            }
        }
    }
}
