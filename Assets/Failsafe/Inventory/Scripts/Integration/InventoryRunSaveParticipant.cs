using System;
using Cysharp.Threading.Tasks;
using Failsafe.Inventory.Core;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer;
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
        private readonly InventoryRestorePlanner _restorePlanner;

        private IDisposable _registration;

        public string ParticipantId => Id;
        public int RestoreOrder => InventoryRestoreOrder;

        public InventoryRunSaveParticipant(
            InventoryRuntimeController inventory,
            InventoryQuickSlotEquipService equipService,
            RunSaveParticipantRegistry participantRegistry)
            : this(
                inventory,
                equipService,
                participantRegistry,
                persistentObjectRegistry: null)
        {
        }

        [Inject]
        public InventoryRunSaveParticipant(
            InventoryRuntimeController inventory,
            InventoryQuickSlotEquipService equipService,
            RunSaveParticipantRegistry participantRegistry,
            RunPersistentObjectRegistry persistentObjectRegistry)
        {
            _inventory = inventory ??
                throw new ArgumentNullException(nameof(inventory));
            _equipService = equipService ??
                throw new ArgumentNullException(nameof(equipService));
            _participantRegistry = participantRegistry ??
                throw new ArgumentNullException(nameof(participantRegistry));
            _restorePlanner = new InventoryRestorePlanner(
                inventory,
                persistentObjectRegistry);
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

            EnsureInventoryInitialized();
            InventoryRestorePlanner.RestorePlan plan =
                _restorePlanner.Build(state);

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

        private void RestoreItems(
            InventoryRestorePlanner.RestorePlan plan)
        {
            for (int index = 0; index < plan.Items.Count; index++)
            {
                InventoryRestorePlanner.RestoreItemPlan itemPlan =
                    plan.Items[index];
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

        private void RestoreQuickSlots(
            InventoryRestorePlanner.RestorePlan plan)
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
    }
}
