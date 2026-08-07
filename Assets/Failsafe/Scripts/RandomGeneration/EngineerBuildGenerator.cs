using System;
using System.Collections.Generic;
using System.Linq;
using Failsafe.Scripts.EffectSystem;

namespace Assets.Failsafe.Scripts.RandomGeneration
{
    public sealed class EngineerBuildGenerator
    {
        private const int MinimumNegativePerks = 1;

        private readonly RandomGenerator _randomGenerator;

        public EngineerBuildGenerator(RandomGenerator randomGenerator)
        {
            _randomGenerator = randomGenerator ??
                throw new ArgumentNullException(nameof(randomGenerator));
        }

        public bool TryGenerateForNewRun(
            EngineerGenerationConfig config,
            out EngineerGenerationResult result,
            out string error,
            int? seed = null)
        {
            result = null;

            if (!TryValidateConfig(
                    config,
                    out Dictionary<string, PerkDefinition> definitionsById,
                    out error))
            {
                return false;
            }

            _randomGenerator.BeginRun(seed);

            var engineers = new List<EngineerBuild>(config.EngineerCount);
            var buildSignatures = new HashSet<string>(StringComparer.Ordinal);
            var operatorCodes = new HashSet<string>(StringComparer.Ordinal);
            var operatorCodeRandom = new System.Random(_randomGenerator.Seed);

            for (int engineerIndex = 0;
                 engineerIndex < config.EngineerCount;
                 engineerIndex++)
            {
                string operatorCode = CreateUniqueOperatorCode(
                    operatorCodeRandom,
                    operatorCodes);

                if (!TryGenerateEngineer(
                        config,
                        definitionsById,
                        engineerIndex,
                        operatorCode,
                        buildSignatures,
                        out EngineerBuild engineer,
                        out error))
                {
                    return false;
                }

                engineers.Add(engineer);
            }

            result = new EngineerGenerationResult(
                _randomGenerator.Seed,
                engineers);
            error = null;
            return true;
        }

        private bool TryGenerateEngineer(
            EngineerGenerationConfig config,
            IReadOnlyDictionary<string, PerkDefinition> definitionsById,
            int engineerIndex,
            string operatorCode,
            ISet<string> buildSignatures,
            out EngineerBuild engineer,
            out string error)
        {
            engineer = null;

            for (int attempt = 0;
                 attempt < config.MaxAttemptsPerEngineer;
                 attempt++)
            {
                List<RandomizationItem> rolledItems = _randomGenerator.BlessRNG(
                    config.PerkPool,
                    config.MinTotalWeight,
                    config.MaxTotalWeight + 1,
                    config.MaxPerksPerEngineer,
                    out int totalWeight);

                if (!TryCreatePerks(
                        rolledItems,
                        definitionsById,
                        out List<EngineerPerk> perks))
                {
                    continue;
                }

                int spentWeight = perks.Sum(perk => perk.Cost);
                int negativePerkCount = perks.Count(perk => perk.IsNegative);

                if (spentWeight > totalWeight ||
                    negativePerkCount < MinimumNegativePerks ||
                    negativePerkCount > config.MaxNegativePerksPerEngineer ||
                    HasDuplicatePerks(perks) ||
                    HasExcludedPair(perks))
                {
                    continue;
                }

                string signature = CreateBuildSignature(perks);

                if (!buildSignatures.Add(signature))
                    continue;

                engineer = new EngineerBuild(
                    ResolveEngineerName(config.EngineerNames, engineerIndex),
                    operatorCode,
                    totalWeight,
                    spentWeight,
                    perks);
                error = null;
                return true;
            }

            error =
                $"Could not generate engineer {engineerIndex + 1} after " +
                $"{config.MaxAttemptsPerEngineer} attempts. Check the perk pool, " +
                "negative perks, exclusions and point limits.";
            return false;
        }

        private static bool TryCreatePerks(
            IReadOnlyList<RandomizationItem> rolledItems,
            IReadOnlyDictionary<string, PerkDefinition> definitionsById,
            out List<EngineerPerk> perks)
        {
            perks = new List<EngineerPerk>(rolledItems.Count);

            for (int i = 0; i < rolledItems.Count; i++)
            {
                RandomizationItem item = rolledItems[i];

                if (string.IsNullOrWhiteSpace(item.Name) ||
                    !definitionsById.TryGetValue(item.Name, out PerkDefinition definition))
                {
                    perks.Clear();
                    return false;
                }

                perks.Add(new EngineerPerk(item, definition));
            }

            return perks.Count > 0;
        }

        private static bool HasDuplicatePerks(IReadOnlyList<EngineerPerk> perks)
        {
            var perkIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < perks.Count; i++)
            {
                if (!perkIds.Add(perks[i].Definition.Id))
                    return true;
            }

            return false;
        }

        private static bool HasExcludedPair(IReadOnlyList<EngineerPerk> perks)
        {
            for (int firstIndex = 0; firstIndex < perks.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                     secondIndex < perks.Count;
                     secondIndex++)
                {
                    RandomizationItem first = perks[firstIndex].RandomizationItem;
                    RandomizationItem second = perks[secondIndex].RandomizationItem;

                    if (ContainsId(first.Exclude, second.Name) ||
                        ContainsId(second.Exclude, first.Name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsId(string[] ids, string expectedId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(expectedId))
                return false;

            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], expectedId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string CreateBuildSignature(IReadOnlyList<EngineerPerk> perks)
        {
            return string.Join(
                "|",
                perks
                    .Select(perk => perk.Definition.Id)
                    .OrderBy(id => id, StringComparer.Ordinal));
        }

        private static string CreateUniqueOperatorCode(
            System.Random random,
            ISet<string> usedCodes)
        {
            string operatorCode;

            do
            {
                operatorCode =
                    $"{random.Next(1, 100):D2}-" +
                    $"{random.Next(1, 1000):D3}";
            }
            while (!usedCodes.Add(operatorCode));

            return operatorCode;
        }

        private static string ResolveEngineerName(string[] names, int engineerIndex)
        {
            if (names != null &&
                engineerIndex < names.Length &&
                !string.IsNullOrWhiteSpace(names[engineerIndex]))
            {
                return names[engineerIndex];
            }

            return $"Engineer {engineerIndex + 1}";
        }

        private static bool TryValidateConfig(
            EngineerGenerationConfig config,
            out Dictionary<string, PerkDefinition> definitionsById,
            out string error)
        {
            definitionsById = null;

            if (config == null)
            {
                error = "Engineer generation config is null.";
                return false;
            }

            if (config.PerkPool == null ||
                config.PerkPool.GetItems == null ||
                config.PerkPool.GetItems.Count == 0)
            {
                error = "Perk pool is empty.";
                return false;
            }

            if (!config.PerkPool.GetRemoveItem)
            {
                error = "Perk pool must remove items after selection to prevent duplicate perks.";
                return false;
            }

            if (config.EngineerCount <= 0 ||
                config.MaxPerksPerEngineer <= 0 ||
                config.MaxNegativePerksPerEngineer < MinimumNegativePerks ||
                config.MaxNegativePerksPerEngineer > config.MaxPerksPerEngineer ||
                config.MaxAttemptsPerEngineer <= 0)
            {
                error =
                    "Engineer count and attempt limit must be positive. " +
                    "Negative perk limit must be between 1 and the total perk limit.";
                return false;
            }

            if (config.MinTotalWeight < 0 ||
                config.MinTotalWeight > config.MaxTotalWeight ||
                config.MaxTotalWeight == int.MaxValue)
            {
                error = "Engineer total weight range is invalid.";
                return false;
            }

            if (config.PerkDefinitions == null ||
                config.PerkDefinitions.Length == 0)
            {
                error = "Perk definitions are empty.";
                return false;
            }

            definitionsById = new Dictionary<string, PerkDefinition>(
                StringComparer.Ordinal);

            for (int i = 0; i < config.PerkDefinitions.Length; i++)
            {
                PerkDefinition definition = config.PerkDefinitions[i];

                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    error = $"Perk definition at index {i} has no ID.";
                    return false;
                }

                if (definitionsById.ContainsKey(definition.Id))
                {
                    error = $"Perk ID '{definition.Id}' is duplicated.";
                    return false;
                }

                definitionsById.Add(definition.Id, definition);
            }

            bool hasNegativePerk = false;

            for (int i = 0; i < config.PerkPool.GetItems.Count; i++)
            {
                RandomizationItem item = config.PerkPool.GetItems[i];

                if (string.IsNullOrWhiteSpace(item.Name) ||
                    !definitionsById.ContainsKey(item.Name))
                {
                    error =
                        $"Randomization item at index {i} must use an existing perk ID as its name.";
                    return false;
                }

                if (definitionsById[item.Name].IsNegative)
                    hasNegativePerk = true;
            }

            if (!hasNegativePerk)
            {
                error = "Perk pool must contain at least one negative perk.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
