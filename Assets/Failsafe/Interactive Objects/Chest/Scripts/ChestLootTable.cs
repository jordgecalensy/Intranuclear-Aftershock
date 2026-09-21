using System;
using System.Collections.Generic;
using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Inventory.Integration;
using UnityEngine;

namespace Failsafe.Chests
{
    [Serializable]
    public sealed class ChestLootEntry
    {
        [SerializeField]
        [Tooltip("Inventory item created when this entry is taken from the chest.")]
        private ItemData _itemData;

        [SerializeField]
        [Tooltip("Uses the same rarity tiers and thresholds as perk generation.")]
        private ItemRarity _rarity = ItemRarity.Common;

        [SerializeField, Min(1)]
        [Tooltip("How much of the chest's exact loot budget one copy consumes.")]
        private int _weight = 1;

        [SerializeField, Min(1)]
        [Tooltip("Maximum copies when duplicate entries are enabled on the table.")]
        private int _maxCopies = 1;

        [SerializeField]
        [Tooltip("ItemData assets that cannot appear together with this item.")]
        private ItemData[] _excludedItems = Array.Empty<ItemData>();

        public string Id => _itemData?.InventoryDefinitionId?.Trim();
        public ItemData ItemData => _itemData;
        public ItemRarity Rarity => _rarity;
        public int Weight => _weight;
        public int MaxCopies => _maxCopies;
        public IReadOnlyList<ItemData> ExcludedItems => _excludedItems;

        public ChestLootEntry(
            ItemData itemData,
            ItemRarity rarity,
            int weight,
            int maxCopies = 1,
            ItemData[] excludedItems = null)
        {
            _itemData = itemData;
            _rarity = rarity;
            _weight = weight;
            _maxCopies = maxCopies;
            _excludedItems = excludedItems ?? Array.Empty<ItemData>();
        }

        public bool Excludes(string itemDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(itemDefinitionId) ||
                _excludedItems == null)
            {
                return false;
            }

            string normalizedDefinitionId = itemDefinitionId.Trim();

            for (int index = 0; index < _excludedItems.Length; index++)
            {
                ItemData excludedItem = _excludedItems[index];

                if (string.Equals(
                        excludedItem?.InventoryDefinitionId?.Trim(),
                        normalizedDefinitionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [CreateAssetMenu(
        fileName = "ChestLootTable",
        menuName = "ScriptableObjects/Random Generation/Chest Loot Table")]
    public sealed class ChestLootTable : ScriptableObject
    {
        [SerializeField]
        [Tooltip("When disabled, every entry can be selected at most once.")]
        private bool _allowDuplicateEntries;

        [SerializeField]
        private List<ChestLootEntry> _entries = new List<ChestLootEntry>();

        public bool AllowDuplicateEntries => _allowDuplicateEntries;
        public IReadOnlyList<ChestLootEntry> Entries => _entries;

        public bool TryValidate(out string error)
        {
            if (_entries == null || _entries.Count == 0)
            {
                error = $"Chest loot table '{name}' has no entries.";
                return false;
            }

            var entriesByDefinitionId = new Dictionary<string, ChestLootEntry>(
                StringComparer.Ordinal);

            for (int index = 0; index < _entries.Count; index++)
            {
                ChestLootEntry entry = _entries[index];

                if (entry == null)
                {
                    error = $"Chest loot table '{name}' entry {index} is null.";
                    return false;
                }

                if (!ItemDataInventoryAdapter.TryValidateView(
                        entry.ItemData,
                        out string itemError))
                {
                    error =
                        $"Chest loot table '{name}' entry {index} is invalid: " +
                        itemError;
                    return false;
                }

                string definitionId = entry.Id;

                if (entriesByDefinitionId.TryGetValue(
                        definitionId,
                        out ChestLootEntry registeredEntry))
                {
                    error =
                        $"Chest loot table '{name}' contains more than one entry " +
                        $"for inventory definition ID '{definitionId}' " +
                        $"('{registeredEntry.ItemData.name}' and " +
                        $"'{entry.ItemData.name}'). Use Max Copies instead.";
                    return false;
                }

                if (entry.ItemData.WorldItemPrefab == null)
                {
                    error =
                        $"Chest loot table '{name}' entry '{definitionId}' is invalid: " +
                        $"ItemData '{entry.ItemData.name}' has no World Item Prefab.";
                    return false;
                }

                if (entry.ItemData.WorldItemPrefab.ItemData != entry.ItemData)
                {
                    error =
                        $"Chest loot table '{name}' entry '{definitionId}' is invalid: " +
                        $"World Item Prefab '{entry.ItemData.WorldItemPrefab.name}' must " +
                        $"reference the same ItemData '{entry.ItemData.name}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(ItemRarity), entry.Rarity))
                {
                    error =
                        $"Chest loot table '{name}' entry '{definitionId}' has an " +
                        $"unsupported rarity value '{entry.Rarity}'.";
                    return false;
                }

                if (entry.Weight <= 0)
                {
                    error =
                        $"Chest loot table '{name}' entry '{definitionId}' must have " +
                        "a positive weight.";
                    return false;
                }

                if (entry.MaxCopies <= 0)
                {
                    error =
                        $"Chest loot table '{name}' entry '{definitionId}' must allow " +
                        "at least one copy.";
                    return false;
                }

                entriesByDefinitionId.Add(definitionId, entry);
            }

            foreach (KeyValuePair<string, ChestLootEntry> pair in
                     entriesByDefinitionId)
            {
                IReadOnlyList<ItemData> exclusions = pair.Value.ExcludedItems;

                if (exclusions == null)
                    continue;

                var uniqueExclusions = new HashSet<string>(StringComparer.Ordinal);

                for (int index = 0; index < exclusions.Count; index++)
                {
                    ItemData excludedItem = exclusions[index];

                    if (excludedItem == null)
                    {
                        error =
                            $"Chest loot table '{name}' entry '{pair.Key}' has " +
                            $"an empty Excluded Items reference at index {index}.";
                        return false;
                    }

                    string excludedId =
                        excludedItem.InventoryDefinitionId?.Trim();

                    if (string.IsNullOrWhiteSpace(excludedId) ||
                        !entriesByDefinitionId.ContainsKey(excludedId))
                    {
                        error =
                            $"Chest loot table '{name}' entry '{pair.Key}' excludes " +
                            $"ItemData '{excludedItem.name}', which has no entry in " +
                            "this loot table.";
                        return false;
                    }

                    if (string.Equals(
                            pair.Key,
                            excludedId,
                            StringComparison.Ordinal))
                    {
                        error =
                            $"Chest loot table '{name}' entry '{pair.Key}' cannot " +
                            "exclude itself.";
                        return false;
                    }

                    if (!uniqueExclusions.Add(excludedId))
                    {
                        error =
                            $"Chest loot table '{name}' entry '{pair.Key}' lists " +
                            $"excluded ItemData '{excludedItem.name}' more than once.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        public bool TryGetEntry(
            string itemDefinitionId,
            out ChestLootEntry entry)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(itemDefinitionId) ||
                _entries == null)
            {
                return false;
            }

            string normalizedId = itemDefinitionId.Trim();

            for (int index = 0; index < _entries.Count; index++)
            {
                ChestLootEntry candidate = _entries[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.Id?.Trim(),
                        normalizedId,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
