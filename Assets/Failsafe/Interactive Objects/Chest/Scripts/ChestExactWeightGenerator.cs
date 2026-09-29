using System;
using System.Collections.Generic;
using System.Text;
using Assets.Failsafe.Scripts.RandomGeneration;

namespace Failsafe.Chests
{
    public sealed class ChestExactWeightGenerator
    {
        private const int MaximumSearchNodes = 100000;
        private const int MaximumWeightTargets = 10001;

        private readonly Random _random;
        private bool _searchCancelled;
        private bool _searchLimitReached;
        private int _visitedNodes;

        public int Seed { get; }

        public ChestExactWeightGenerator(int? seed = null)
        {
            Seed = seed ?? Guid.NewGuid().GetHashCode();
            _random = new Random(Seed);
        }

        public bool TryGenerate(
            ChestLootTable table,
            int exactWeight,
            int maximumItems,
            Func<IReadOnlyList<ChestLootEntry>, bool> finalSelectionValidator,
            out List<ChestLootEntry> selection,
            out string error)
        {
            return TryGenerate(
                table,
                exactWeight,
                0,
                maximumItems,
                finalSelectionValidator,
                null,
                out selection,
                out _,
                out error);
        }

        public bool TryGenerate(
            ChestLootTable table,
            int exactWeight,
            int maximumItems,
            Func<IReadOnlyList<ChestLootEntry>, bool> finalSelectionValidator,
            Func<bool> canContinueSearch,
            out List<ChestLootEntry> selection,
            out string error)
        {
            return TryGenerate(
                table,
                exactWeight,
                0,
                maximumItems,
                finalSelectionValidator,
                canContinueSearch,
                out selection,
                out _,
                out error);
        }

        public bool TryGenerate(
            ChestLootTable table,
            int targetWeight,
            int weightSpread,
            int maximumItems,
            Func<IReadOnlyList<ChestLootEntry>, bool> finalSelectionValidator,
            out List<ChestLootEntry> selection,
            out int generatedWeight,
            out string error)
        {
            return TryGenerate(
                table,
                targetWeight,
                weightSpread,
                maximumItems,
                finalSelectionValidator,
                canContinueSearch: null,
                out selection,
                out generatedWeight,
                out error);
        }

        public bool TryGenerate(
            ChestLootTable table,
            int targetWeight,
            int weightSpread,
            int maximumItems,
            Func<IReadOnlyList<ChestLootEntry>, bool> finalSelectionValidator,
            Func<bool> canContinueSearch,
            out List<ChestLootEntry> selection,
            out int generatedWeight,
            out string error)
        {
            selection = null;
            generatedWeight = 0;

            if (table == null)
            {
                error = "Chest loot table is not assigned.";
                return false;
            }

            if (!table.TryValidate(out error))
                return false;

            if (targetWeight <= 0)
            {
                error = "Chest target weight must be greater than zero.";
                return false;
            }

            if (weightSpread < 0)
            {
                error = "Chest weight spread cannot be negative.";
                return false;
            }

            if (maximumItems <= 0)
            {
                error = "Chest maximum item count must be greater than zero.";
                return false;
            }

            if (!TryBuildTargetWeights(
                    targetWeight,
                    weightSpread,
                    out List<int> targetWeights,
                    out int minimumWeight,
                    out int maximumWeight,
                    out error))
            {
                return false;
            }

            _visitedNodes = 0;
            _searchCancelled = false;
            _searchLimitReached = false;

            IReadOnlyList<ChestLootEntry> entries = table.Entries;

            for (int targetIndex = 0;
                 targetIndex < targetWeights.Count;
                 targetIndex++)
            {
                int candidateWeight = targetWeights[targetIndex];
                var selected = new List<ChestLootEntry>();
                var counts = new int[entries.Count];
                var failedStates = new HashSet<string>(StringComparer.Ordinal);

                bool found = TrySearch(
                    entries,
                    table.AllowDuplicateEntries,
                    candidateWeight,
                    maximumItems,
                    counts,
                    selected,
                    failedStates,
                    finalSelectionValidator,
                    canContinueSearch);

                if (found)
                {
                    selection = new List<ChestLootEntry>(selected);
                    generatedWeight = candidateWeight;
                    error = null;
                    return true;
                }

                if (_searchCancelled || _searchLimitReached)
                    break;
            }

            if (_searchCancelled)
            {
                error =
                    "Chest loot search stopped because its shared layout " +
                    "search budget was exhausted.";
                return false;
            }

            if (_searchLimitReached)
            {
                error =
                    $"Chest loot search exceeded {MaximumSearchNodes} " +
                    "candidate states. Reduce the budget, spread, copy limits, " +
                    "or loot table size.";
                return false;
            }

            error = weightSpread == 0
                ? $"Chest loot table '{table.name}' cannot produce exactly " +
                  $"{targetWeight} weight within {maximumItems} items and the " +
                  "configured layout constraints."
                : $"Chest loot table '{table.name}' cannot produce a weight " +
                  $"from {minimumWeight} to {maximumWeight} within " +
                  $"{maximumItems} items and the configured layout constraints.";
            return false;
        }

        private bool TryBuildTargetWeights(
            int targetWeight,
            int weightSpread,
            out List<int> targetWeights,
            out int minimumWeight,
            out int maximumWeight,
            out string error)
        {
            long minimum = Math.Max(
                1L,
                (long)targetWeight - weightSpread);
            long maximum = Math.Min(
                int.MaxValue,
                (long)targetWeight + weightSpread);
            long targetCount = maximum - minimum + 1L;

            minimumWeight = (int)minimum;
            maximumWeight = (int)maximum;
            targetWeights = null;

            if (targetCount > MaximumWeightTargets)
            {
                error =
                    $"Chest weight range contains {targetCount} possible " +
                    $"targets. Reduce Weight Spread so the range contains no " +
                    $"more than {MaximumWeightTargets} values.";
                return false;
            }

            int count = (int)targetCount;
            int rolledWeight = minimumWeight + _random.Next(count);
            targetWeights = new List<int>(count) { rolledWeight };

            for (int distance = 1;
                 targetWeights.Count < count;
                 distance++)
            {
                int lower = rolledWeight - distance;
                int upper = rolledWeight + distance;
                bool hasLower = lower >= minimumWeight;
                bool hasUpper = upper <= maximumWeight;

                if (hasLower && hasUpper && _random.Next(2) == 0)
                {
                    targetWeights.Add(upper);
                    targetWeights.Add(lower);
                }
                else
                {
                    if (hasLower)
                        targetWeights.Add(lower);

                    if (hasUpper)
                        targetWeights.Add(upper);
                }
            }

            error = null;
            return true;
        }

        private bool TrySearch(
            IReadOnlyList<ChestLootEntry> entries,
            bool allowDuplicates,
            int remainingWeight,
            int maximumItems,
            int[] counts,
            List<ChestLootEntry> selected,
            HashSet<string> failedStates,
            Func<IReadOnlyList<ChestLootEntry>, bool> finalSelectionValidator,
            Func<bool> canContinueSearch)
        {
            if (canContinueSearch != null && !canContinueSearch())
            {
                _searchCancelled = true;
                return false;
            }

            _visitedNodes++;

            if (_visitedNodes > MaximumSearchNodes)
            {
                _searchLimitReached = true;
                return false;
            }

            string stateKey = BuildStateKey(remainingWeight, counts);

            if (failedStates.Contains(stateKey))
                return false;

            if (remainingWeight == 0)
            {
                bool isValid = finalSelectionValidator == null ||
                               finalSelectionValidator(selected);

                if (!isValid &&
                    canContinueSearch != null &&
                    !canContinueSearch())
                {
                    _searchCancelled = true;
                }

                if (!isValid && !_searchCancelled)
                    failedStates.Add(stateKey);

                return isValid;
            }

            if (selected.Count >= maximumItems)
            {
                failedStates.Add(stateKey);
                return false;
            }

            var candidateIndices = new List<int>();

            for (int index = 0; index < entries.Count; index++)
            {
                ChestLootEntry candidate = entries[index];
                int copyLimit = allowDuplicates ? candidate.MaxCopies : 1;

                if (counts[index] >= copyLimit ||
                    candidate.Weight > remainingWeight ||
                    ConflictsWithSelection(candidate, entries, counts))
                {
                    continue;
                }

                candidateIndices.Add(index);
            }

            if (candidateIndices.Count == 0)
            {
                failedStates.Add(stateKey);
                return false;
            }

            Shuffle(candidateIndices);
            ItemRarity preferredRarity = RollRarity();

            // This deliberately mirrors RandomGenerator: roll a preferred
            // rarity, then fall back to another remaining item when that tier
            // cannot satisfy the current exact-weight state.
            MovePreferredRarityToFront(
                candidateIndices,
                entries,
                preferredRarity);

            for (int candidateIndex = 0;
                 candidateIndex < candidateIndices.Count;
                 candidateIndex++)
            {
                int entryIndex = candidateIndices[candidateIndex];
                ChestLootEntry entry = entries[entryIndex];

                counts[entryIndex]++;
                selected.Add(entry);

                if (TrySearch(
                        entries,
                        allowDuplicates,
                        remainingWeight - entry.Weight,
                        maximumItems,
                        counts,
                        selected,
                        failedStates,
                        finalSelectionValidator,
                        canContinueSearch))
                {
                    return true;
                }

                selected.RemoveAt(selected.Count - 1);
                counts[entryIndex]--;

                if (_searchLimitReached || _searchCancelled)
                    return false;
            }

            failedStates.Add(stateKey);
            return false;
        }

        private static bool ConflictsWithSelection(
            ChestLootEntry candidate,
            IReadOnlyList<ChestLootEntry> entries,
            int[] counts)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (counts[index] <= 0)
                    continue;

                ChestLootEntry selected = entries[index];

                if (candidate.Excludes(selected.Id) ||
                    selected.Excludes(candidate.Id))
                {
                    return true;
                }
            }

            return false;
        }

        private ItemRarity RollRarity()
        {
            int roll = _random.Next(100);

            switch (roll)
            {
                case >= (int)ItemRarity.Unique:
                    return ItemRarity.Unique;

                case >= (int)ItemRarity.Rare:
                    return ItemRarity.Rare;

                case >= (int)ItemRarity.Uncommon:
                    return ItemRarity.Uncommon;

                default:
                    return ItemRarity.Common;
            }
        }

        private static void MovePreferredRarityToFront(
            List<int> candidateIndices,
            IReadOnlyList<ChestLootEntry> entries,
            ItemRarity preferredRarity)
        {
            int writeIndex = 0;

            for (int readIndex = 0;
                 readIndex < candidateIndices.Count;
                 readIndex++)
            {
                int entryIndex = candidateIndices[readIndex];

                if (entries[entryIndex].Rarity != preferredRarity)
                    continue;

                int previous = candidateIndices[writeIndex];
                candidateIndices[writeIndex] = entryIndex;
                candidateIndices[readIndex] = previous;
                writeIndex++;
            }
        }

        private void Shuffle(List<int> values)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = _random.Next(index + 1);
                int value = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        private static string BuildStateKey(
            int remainingWeight,
            int[] counts)
        {
            var builder = new StringBuilder();
            builder.Append(remainingWeight);

            for (int index = 0; index < counts.Length; index++)
            {
                builder.Append('|');
                builder.Append(counts[index]);
            }

            return builder.ToString();
        }
    }
}
