using System;
using System.Collections.Generic;
using Failsafe.Inventory.Core;
using Failsafe.Inventory.Integration;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;

namespace Failsafe.Chests
{
    public sealed class ChestStoredItem
    {
        public string InstanceId { get; }
        public ChestLootEntry LootEntry { get; }
        public ItemData ItemData => LootEntry.ItemData;
        public int Weight => LootEntry.Weight;

        internal ChestStoredItem(
            string instanceId,
            ChestLootEntry lootEntry)
        {
            InstanceId = instanceId;
            LootEntry = lootEntry;
        }
    }

    public readonly struct ChestDetachedItem
    {
        public string InstanceId { get; }
        public ChestLootEntry LootEntry { get; }
        public ItemData ItemData => LootEntry?.ItemData;
        public InventoryGridPosition Origin { get; }
        public InventoryItemRotation Rotation { get; }

        internal ChestDetachedItem(
            string instanceId,
            ChestLootEntry lootEntry,
            InventoryGridPosition origin,
            InventoryItemRotation rotation)
        {
            InstanceId = instanceId;
            LootEntry = lootEntry;
            Origin = origin;
            Rotation = rotation;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunPersistentObject))]
    public sealed class ChestInventoryController :
        MonoBehaviour,
        IRunPersistentStateProvider
    {
        private const string PersistentStateTypeId = "chest-inventory";
        private const int PersistentStateVersion = 1;
        private const int MaximumLayoutAttempts = 128;
        private const int MaximumLayoutSearchOperations = 250000;

        [Header("Loot")]
        [SerializeField] private ChestLootTable _lootTable;

        [SerializeField, Min(1)]
        [InspectorName("Target Weight")]
        [Tooltip("Preferred total item weight before applying Weight Spread.")]
        private int _exactWeightBudget = 5;

        [SerializeField, Min(0)]
        [Tooltip(
            "Allowed deviation below and above Target Weight. Zero requires " +
            "the exact target weight.")]
        private int _weightSpread;

        [Header("Grid")]
        [SerializeField, Min(1)] private int _columns = 6;
        [SerializeField, Min(1)] private int _rows = 3;

        private readonly Dictionary<string, ChestStoredItem> _storedItems =
            new Dictionary<string, ChestStoredItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, ChestDetachedItem> _pendingDetachedItems =
            new Dictionary<string, ChestDetachedItem>(StringComparer.Ordinal);

        private ItemDataInventoryViewResolver _viewResolver;

        public string StateTypeId => PersistentStateTypeId;
        public int StateVersion => PersistentStateVersion;

        public ChestLootTable LootTable => _lootTable;
        public int ExactWeightBudget => _exactWeightBudget;
        public int TargetWeight => _exactWeightBudget;
        public int WeightSpread => _weightSpread;
        public int Columns => _columns;
        public int Rows => _rows;
        public bool IsGenerated { get; private set; }
        public int GenerationSeed { get; private set; }
        public int GeneratedWeight { get; private set; }
        public int RemainingWeight { get; private set; }
        public int ItemCount => _storedItems.Count;
        public InventoryGridModel Grid { get; private set; }
        public ItemDataInventoryViewResolver ViewResolver => _viewResolver;
        public IReadOnlyCollection<ChestStoredItem> StoredItems =>
            _storedItems.Values;

        public event Action ContentsChanged;

        private void Awake()
        {
            CreateEmptyRuntime();
        }

        private void Start()
        {
            if (IsGenerated)
                return;

            if (!TryEnsureGenerated(out string error))
            {
                Debug.LogError(
                    $"Chest '{name}' could not generate its contents: {error}",
                    this);
            }
        }

        private void OnValidate()
        {
            _exactWeightBudget = Mathf.Max(1, _exactWeightBudget);
            _weightSpread = Mathf.Max(0, _weightSpread);
            _columns = Mathf.Max(1, _columns);
            _rows = Mathf.Max(1, _rows);
        }

        public InventoryOperationResult Relocate(
            string instanceId,
            InventoryGridPosition origin,
            InventoryItemRotation rotation)
        {
            if (!IsGenerated || Grid == null ||
                string.IsNullOrWhiteSpace(instanceId) || !_storedItems.ContainsKey(instanceId))
                return InventoryOperationResult.Failure(InventoryFailureReason.InvalidItem);

            InventoryOperationResult result = Grid.TryRelocate(instanceId, origin, rotation);
            if (result.IsSuccess)
                NotifyContentsChanged();
            return result;
        }

        public bool TryEnsureGenerated(out string error)
        {
            return TryEnsureGenerated(seed: null, out error);
        }

        public bool TryEnsureGenerated(int? seed, out string error)
        {
            EnsureRuntimeCreated();

            if (IsGenerated)
            {
                error = null;
                return true;
            }

            if (_lootTable == null)
            {
                error = "Chest loot table is not assigned.";
                return false;
            }

            if (!_lootTable.TryValidate(out error))
                return false;

            var generator = new ChestExactWeightGenerator(seed);
            List<ChestGridPlacement> packedPlacements = null;
            string lastPackingError = null;
            int layoutAttempts = 0;
            int remainingLayoutSearchOperations =
                MaximumLayoutSearchOperations;

            bool generated = generator.TryGenerate(
                _lootTable,
                _exactWeightBudget,
                _weightSpread,
                _columns * _rows,
                selection =>
                {
                    layoutAttempts++;

                    bool packed = ChestGridPackingSolver.TryPack(
                        selection,
                        _columns,
                        _rows,
                        ref remainingLayoutSearchOperations,
                        out List<ChestGridPlacement> candidatePlacements,
                        out string packingError);

                    if (packed)
                        packedPlacements = candidatePlacements;
                    else
                        lastPackingError = packingError;

                    return packed;
                },
                () =>
                    layoutAttempts < MaximumLayoutAttempts &&
                    remainingLayoutSearchOperations > 0,
                out List<ChestLootEntry> selection,
                out int generatedWeight,
                out string generationError);

            if (!generated)
            {
                error = string.IsNullOrWhiteSpace(lastPackingError)
                    ? generationError
                    : $"{generationError} Last layout error: {lastPackingError}";
                return false;
            }

            if (selection == null ||
                packedPlacements == null ||
                selection.Count != packedPlacements.Count)
            {
                error = "Chest generation returned an incomplete grid layout.";
                return false;
            }

            ClearRuntimeContents();

            for (int index = 0; index < packedPlacements.Count; index++)
            {
                ChestGridPlacement placement = packedPlacements[index];
                string instanceId = Guid.NewGuid().ToString("N");

                if (TryAddItem(
                        instanceId,
                        placement.Entry,
                        placement.Origin,
                        placement.Rotation,
                        notify: false,
                        out error))
                {
                    continue;
                }

                ClearRuntimeContents();
                IsGenerated = false;
                GenerationSeed = 0;
                GeneratedWeight = 0;
                RemainingWeight = 0;
                return false;
            }

            IsGenerated = true;
            GenerationSeed = generator.Seed;
            GeneratedWeight = generatedWeight;
            RemainingWeight = generatedWeight;
            NotifyContentsChanged();
            error = null;
            return true;
        }

        public bool TryGetStoredItem(
            string instanceId,
            out ChestStoredItem storedItem)
        {
            storedItem = null;

            return !string.IsNullOrWhiteSpace(instanceId) &&
                   _storedItems.TryGetValue(instanceId, out storedItem) &&
                   storedItem != null;
        }

        public bool TryGetItemData(
            string instanceId,
            out ItemData itemData)
        {
            itemData = null;

            if (!TryGetStoredItem(instanceId, out ChestStoredItem storedItem))
                return false;

            itemData = storedItem.ItemData;
            return itemData != null;
        }

        public bool TryDetach(
            string instanceId,
            out ChestDetachedItem detachedItem,
            out string error)
        {
            detachedItem = default;

            if (!TryGetStoredItem(instanceId, out ChestStoredItem storedItem))
            {
                error = $"Chest item '{instanceId}' was not found.";
                return false;
            }

            if (Grid == null ||
                !Grid.TryGetPlacement(
                    instanceId,
                    out InventoryPlacement placement))
            {
                error =
                    $"Chest item '{instanceId}' has no grid placement.";
                return false;
            }

            detachedItem = new ChestDetachedItem(
                instanceId,
                storedItem.LootEntry,
                placement.Origin,
                placement.Item.Rotation);

            if (_pendingDetachedItems.ContainsKey(instanceId))
            {
                detachedItem = default;
                error =
                    $"Chest item '{instanceId}' already has a pending transfer.";
                return false;
            }

            InventoryOperationResult result = Grid.TryRemove(instanceId);

            if (!result.IsSuccess)
            {
                detachedItem = default;
                error =
                    $"Chest item '{instanceId}' could not be removed: " +
                    $"{result.FailureReason}.";
                return false;
            }

            _storedItems.Remove(instanceId);
            _viewResolver?.Unregister(instanceId);
            _pendingDetachedItems.Add(instanceId, detachedItem);
            RemainingWeight = Mathf.Max(
                0,
                RemainingWeight - storedItem.Weight);
            NotifyContentsChanged();
            error = null;
            return true;
        }

        public bool TryRestoreDetached(
            ChestDetachedItem detachedItem,
            out string error)
        {
            if (!TryGetPendingDetachedItem(
                    detachedItem,
                    out ChestDetachedItem pendingItem,
                    out error))
                return false;

            if (TryAddItem(
                    pendingItem.InstanceId,
                    pendingItem.LootEntry,
                    pendingItem.Origin,
                    pendingItem.Rotation,
                    notify: false,
                    out error))
            {
                _pendingDetachedItems.Remove(pendingItem.InstanceId);
                RemainingWeight += pendingItem.LootEntry.Weight;
                NotifyContentsChanged();
                return true;
            }

            return false;
        }

        public bool TryCommitDetached(
            ChestDetachedItem detachedItem,
            out string error)
        {
            if (!TryGetPendingDetachedItem(
                    detachedItem,
                    out ChestDetachedItem pendingItem,
                    out error))
                return false;

            _pendingDetachedItems.Remove(pendingItem.InstanceId);
            error = null;
            return true;
        }

        public string CapturePersistentState()
        {
            EnsureRuntimeCreated();

            if (!IsGenerated && !TryEnsureGenerated(out string generationError))
            {
                throw new InvalidOperationException(
                    $"Chest contents could not be generated before saving: " +
                    $"{generationError}");
            }

            if (_pendingDetachedItems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Chest inventory cannot be saved while " +
                    $"{_pendingDetachedItems.Count} item transfer(s) are pending.");
            }

            int calculatedRemainingWeight = CalculateStoredWeight();

            if (calculatedRemainingWeight != RemainingWeight)
            {
                throw new InvalidOperationException(
                    $"Chest remaining weight is {RemainingWeight}, but stored items " +
                    $"add up to {calculatedRemainingWeight}.");
            }

            if (!IsWeightInsideConfiguredRange(GeneratedWeight))
            {
                throw new InvalidOperationException(
                    $"Chest generated weight is {GeneratedWeight}, but its " +
                    $"configured range is {GetMinimumGeneratedWeight()} to " +
                    $"{GetMaximumGeneratedWeight()}.");
            }

            var state = new ChestInventoryPersistentState
            {
                schemaVersion = PersistentStateVersion,
                isGenerated = IsGenerated,
                generationSeed = GenerationSeed,
                generatedWeight = GeneratedWeight,
                remainingWeight = RemainingWeight
            };

            if (Grid != null)
            {
                foreach (InventoryPlacement placement in Grid.Placements)
                {
                    if (!_storedItems.TryGetValue(
                            placement.Item.InstanceId,
                            out ChestStoredItem storedItem))
                    {
                        throw new InvalidOperationException(
                            $"Chest item '{placement.Item.InstanceId}' has no " +
                            "stored item metadata.");
                    }

                    state.items.Add(new ChestItemPersistentState
                    {
                        instanceId = storedItem.InstanceId,
                        lootEntryId = storedItem.LootEntry.Id,
                        definitionId =
                            storedItem.ItemData.InventoryDefinitionId.Trim(),
                        weight = storedItem.Weight,
                        column = placement.Origin.Column,
                        row = placement.Origin.Row,
                        rotation = (int)placement.Item.Rotation
                    });
                }
            }

            return JsonUtility.ToJson(state);
        }

        public void RestorePersistentState(
            string serializedState,
            int stateVersion)
        {
            if (_pendingDetachedItems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Chest inventory cannot be restored while " +
                    $"{_pendingDetachedItems.Count} item transfer(s) are pending.");
            }

            if (stateVersion != PersistentStateVersion)
            {
                throw new InvalidOperationException(
                    $"Chest inventory state version {stateVersion} is not " +
                    $"supported. Expected {PersistentStateVersion}.");
            }

            if (string.IsNullOrWhiteSpace(serializedState))
            {
                throw new InvalidOperationException(
                    "Saved chest inventory state is empty.");
            }

            ChestInventoryPersistentState state =
                JsonUtility.FromJson<ChestInventoryPersistentState>(
                    serializedState);

            if (state == null)
            {
                throw new InvalidOperationException(
                    "Saved chest inventory state is invalid.");
            }

            if (state.schemaVersion != PersistentStateVersion)
            {
                throw new InvalidOperationException(
                    $"Saved chest inventory payload version " +
                    $"{state.schemaVersion} is invalid. Expected " +
                    $"{PersistentStateVersion}.");
            }

            if (!state.isGenerated)
            {
                throw new InvalidOperationException(
                    "Saved chest inventory was not generated.");
            }

            if (state.generatedWeight <= 0 ||
                !IsWeightInsideConfiguredRange(state.generatedWeight))
            {
                throw new InvalidOperationException(
                    $"Saved chest generated weight {state.generatedWeight} is " +
                    $"outside the configured range " +
                    $"{GetMinimumGeneratedWeight()} to " +
                    $"{GetMaximumGeneratedWeight()}.");
            }

            if (state.remainingWeight < 0 ||
                state.remainingWeight > state.generatedWeight)
            {
                throw new InvalidOperationException(
                    $"Saved chest remaining weight {state.remainingWeight} must be " +
                    $"between zero and {state.generatedWeight}.");
            }

            if (_lootTable == null)
            {
                throw new InvalidOperationException(
                    "Saved chest contents cannot be restored: loot table is missing.");
            }

            if (!_lootTable.TryValidate(out string tableError))
            {
                throw new InvalidOperationException(
                    $"Saved chest contents cannot be restored: {tableError}");
            }

            if (state.items == null)
            {
                throw new InvalidOperationException(
                    "Saved chest item list is missing.");
            }

            if (!TryValidateRestoreItems(state, out string restoreValidationError))
            {
                throw new InvalidOperationException(
                    $"Saved chest contents cannot be restored: " +
                    $"{restoreValidationError}");
            }

            EnsureRuntimeCreated();
            ClearRuntimeContents();
            IsGenerated = false;
            GenerationSeed = 0;
            GeneratedWeight = 0;
            RemainingWeight = 0;

            try
            {
                for (int index = 0; index < state.items.Count; index++)
                {
                    ChestItemPersistentState savedItem = state.items[index];

                    if (!TryResolveSavedItem(
                            savedItem,
                            index,
                            out ChestLootEntry entry,
                            out InventoryGridPosition origin,
                            out InventoryItemRotation rotation,
                            out string resolveError))
                    {
                        throw new InvalidOperationException(
                            resolveError);
                    }

                    if (!TryAddItem(
                            savedItem.instanceId,
                            entry,
                            origin,
                            rotation,
                            notify: false,
                            out string addError))
                    {
                        throw new InvalidOperationException(
                            $"Saved chest item '{savedItem.instanceId}' could not " +
                            $"be restored: {addError}");
                    }
                }
            }
            catch
            {
                ClearRuntimeContents();
                throw;
            }

            int calculatedRemainingWeight = CalculateStoredWeight();

            if (calculatedRemainingWeight != state.remainingWeight)
            {
                ClearRuntimeContents();
                throw new InvalidOperationException(
                    $"Saved chest remaining weight is {state.remainingWeight}, " +
                    $"but restored items add up to {calculatedRemainingWeight}.");
            }

            IsGenerated = true;
            GenerationSeed = state.generationSeed;
            GeneratedWeight = state.generatedWeight;
            RemainingWeight = calculatedRemainingWeight;
            NotifyContentsChanged();
        }

        private bool TryValidateRestoreItems(
            ChestInventoryPersistentState state,
            out string error)
        {
            var validationGrid = new InventoryGridModel(_columns, _rows);
            var validationResolver = new ItemDataInventoryViewResolver();
            long calculatedRemainingWeight = 0;

            try
            {
                for (int index = 0; index < state.items.Count; index++)
                {
                    ChestItemPersistentState savedItem = state.items[index];

                    if (!TryResolveSavedItem(
                            savedItem,
                            index,
                            out ChestLootEntry entry,
                            out InventoryGridPosition origin,
                            out InventoryItemRotation rotation,
                            out error))
                    {
                        return false;
                    }

                    if (!ItemDataInventoryAdapter.TryCreateModel(
                            entry.ItemData,
                            savedItem.instanceId,
                            quantity: 1,
                            out InventoryItemModel model,
                            out string modelError))
                    {
                        error =
                            $"Saved chest item '{savedItem.instanceId}' is invalid: " +
                            $"{modelError}";
                        return false;
                    }

                    if (!validationResolver.TryRegister(
                            savedItem.instanceId,
                            entry.ItemData,
                            out string viewError))
                    {
                        error =
                            $"Saved chest item '{savedItem.instanceId}' has an " +
                            $"invalid inventory view: {viewError}";
                        return false;
                    }

                    InventoryOperationResult placementResult =
                        validationGrid.TryPlace(model, origin, rotation);

                    if (!placementResult.IsSuccess)
                    {
                        error =
                            $"Saved chest item '{savedItem.instanceId}' cannot be " +
                            $"placed in the configured grid: " +
                            $"{placementResult.FailureReason}.";
                        return false;
                    }

                    calculatedRemainingWeight += entry.Weight;
                }

                if (calculatedRemainingWeight != state.remainingWeight)
                {
                    error =
                        $"Saved chest remaining weight is {state.remainingWeight}, " +
                        $"but saved items add up to " +
                        $"{calculatedRemainingWeight}.";
                    return false;
                }

                error = null;
                return true;
            }
            finally
            {
                validationResolver.Clear();
            }
        }

        private bool TryResolveSavedItem(
            ChestItemPersistentState savedItem,
            int itemIndex,
            out ChestLootEntry entry,
            out InventoryGridPosition origin,
            out InventoryItemRotation rotation,
            out string error)
        {
            entry = null;
            origin = default;
            rotation = default;

            if (savedItem == null)
            {
                error = $"Saved chest item {itemIndex} is null.";
                return false;
            }

            if (!_lootTable.TryGetEntry(
                    savedItem.definitionId,
                    out entry))
            {
                error =
                    $"Saved chest item definition '{savedItem.definitionId}' " +
                    "does not exist in the configured loot table.";
                return false;
            }

            if (!string.Equals(
                    entry.ItemData.InventoryDefinitionId.Trim(),
                    savedItem.definitionId?.Trim(),
                    StringComparison.Ordinal))
            {
                error =
                    $"Saved chest item expects " +
                    $"item definition '{savedItem.definitionId}', but the " +
                    $"loot table provides " +
                    $"'{entry.ItemData.InventoryDefinitionId}'.";
                return false;
            }

            if (entry.Weight != savedItem.weight)
            {
                error =
                    $"Saved chest item '{savedItem.definitionId}' has weight " +
                    $"{savedItem.weight}, but the loot table now uses " +
                    $"{entry.Weight}.";
                return false;
            }

            if (!Enum.IsDefined(
                    typeof(InventoryItemRotation),
                    savedItem.rotation))
            {
                error =
                    $"Saved chest item '{savedItem.instanceId}' has invalid " +
                    $"rotation value {savedItem.rotation}.";
                return false;
            }

            origin = new InventoryGridPosition(
                savedItem.column,
                savedItem.row);
            rotation = (InventoryItemRotation)savedItem.rotation;
            error = null;
            return true;
        }

        private bool TryGetPendingDetachedItem(
            ChestDetachedItem detachedItem,
            out ChestDetachedItem pendingItem,
            out string error)
        {
            pendingItem = default;

            if (string.IsNullOrWhiteSpace(detachedItem.InstanceId) ||
                detachedItem.LootEntry == null)
            {
                error = "Detached chest item is invalid.";
                return false;
            }

            if (!_pendingDetachedItems.TryGetValue(
                    detachedItem.InstanceId,
                    out pendingItem) ||
                pendingItem.LootEntry != detachedItem.LootEntry ||
                !pendingItem.Origin.Equals(detachedItem.Origin) ||
                pendingItem.Rotation != detachedItem.Rotation)
            {
                pendingItem = default;
                error =
                    $"Chest item '{detachedItem.InstanceId}' is not a pending " +
                    "transfer for this chest.";
                return false;
            }

            error = null;
            return true;
        }

        private int CalculateStoredWeight()
        {
            int weight = 0;

            foreach (ChestStoredItem storedItem in _storedItems.Values)
                weight += storedItem.Weight;

            return weight;
        }

        private bool IsWeightInsideConfiguredRange(int weight)
        {
            return weight >= GetMinimumGeneratedWeight() &&
                   weight <= GetMaximumGeneratedWeight();
        }

        private int GetMinimumGeneratedWeight()
        {
            return (int)Math.Max(
                1L,
                (long)_exactWeightBudget - _weightSpread);
        }

        private int GetMaximumGeneratedWeight()
        {
            return (int)Math.Min(
                int.MaxValue,
                (long)_exactWeightBudget + _weightSpread);
        }

        private void NotifyContentsChanged()
        {
            Action handlers = ContentsChanged;

            if (handlers == null)
                return;

            Delegate[] subscribers = handlers.GetInvocationList();

            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action)subscribers[index]).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private bool TryAddItem(
            string instanceId,
            ChestLootEntry entry,
            InventoryGridPosition origin,
            InventoryItemRotation rotation,
            bool notify,
            out string error)
        {
            EnsureRuntimeCreated();

            if (entry?.ItemData == null)
            {
                error = "Chest loot entry has no ItemData.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Chest item instance ID is empty.";
                return false;
            }

            if (_storedItems.ContainsKey(instanceId))
            {
                error = $"Chest item instance ID '{instanceId}' already exists.";
                return false;
            }

            if (!ItemDataInventoryAdapter.TryCreateModel(
                    entry.ItemData,
                    instanceId,
                    quantity: 1,
                    out InventoryItemModel model,
                    out error))
            {
                return false;
            }

            if (!_viewResolver.TryRegister(
                    instanceId,
                    entry.ItemData,
                    out error))
            {
                return false;
            }

            InventoryOperationResult placementResult = Grid.TryPlace(
                model,
                origin,
                rotation);

            if (!placementResult.IsSuccess)
            {
                _viewResolver.Unregister(instanceId);
                error =
                    $"Chest grid placement failed: " +
                    $"{placementResult.FailureReason}.";
                return false;
            }

            _storedItems.Add(
                instanceId,
                new ChestStoredItem(instanceId, entry));

            if (notify)
                ContentsChanged?.Invoke();

            error = null;
            return true;
        }

        private void EnsureRuntimeCreated()
        {
            if (Grid == null || _viewResolver == null)
                CreateEmptyRuntime();
        }

        private void CreateEmptyRuntime()
        {
            _columns = Mathf.Max(1, _columns);
            _rows = Mathf.Max(1, _rows);
            Grid = new InventoryGridModel(_columns, _rows);
            _viewResolver = new ItemDataInventoryViewResolver();
            _storedItems.Clear();
            _pendingDetachedItems.Clear();
            IsGenerated = false;
            GenerationSeed = 0;
            GeneratedWeight = 0;
            RemainingWeight = 0;
        }

        private void ClearRuntimeContents()
        {
            if (Grid != null)
            {
                var instanceIds = new List<string>();

                foreach (InventoryPlacement placement in Grid.Placements)
                    instanceIds.Add(placement.Item.InstanceId);

                for (int index = 0; index < instanceIds.Count; index++)
                    Grid.TryRemove(instanceIds[index]);
            }

            _viewResolver?.Clear();
            _storedItems.Clear();
            _pendingDetachedItems.Clear();
        }

        [Serializable]
        private sealed class ChestInventoryPersistentState
        {
            public int schemaVersion;
            public bool isGenerated;
            public int generationSeed;
            public int generatedWeight;
            public int remainingWeight;
            public List<ChestItemPersistentState> items =
                new List<ChestItemPersistentState>();
        }

        [Serializable]
        private sealed class ChestItemPersistentState
        {
            public string instanceId;
            public string lootEntryId;
            public string definitionId;
            public int weight;
            public int column;
            public int row;
            public int rotation;
        }
    }
}
