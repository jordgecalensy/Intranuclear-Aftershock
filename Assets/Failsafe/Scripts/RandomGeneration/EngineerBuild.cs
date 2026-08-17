using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;

namespace Assets.Failsafe.Scripts.RandomGeneration
{
    public sealed class EngineerPerk
    {
        public EngineerPerk(RandomizationItem randomizationItem, PerkDefinition definition)
        {
            RandomizationItem = randomizationItem;
            Definition = definition;
        }

        public RandomizationItem RandomizationItem { get; }
        public PerkDefinition Definition { get; }
        public int Cost => RandomizationItem.Weight;
        public ItemRarity Rarity => RandomizationItem.Rarity;
        public bool IsNegative => Definition != null && Definition.IsNegative;
    }

    public sealed class EngineerBuild
    {
        public EngineerBuild(
            string name,
            int totalWeight,
            int spentWeight,
            IReadOnlyList<EngineerPerk> perks)
            : this(name, string.Empty, totalWeight, spentWeight, perks)
        {
        }

        public EngineerBuild(
            string name,
            string operatorCode,
            int totalWeight,
            int spentWeight,
            IReadOnlyList<EngineerPerk> perks)
        {
            Name = name;
            OperatorCode = operatorCode ?? string.Empty;
            TotalWeight = totalWeight;
            SpentWeight = spentWeight;
            Perks = perks;
        }

        public string Name { get; }
        public string OperatorCode { get; }
        public int TotalWeight { get; }
        public int SpentWeight { get; }
        public int RemainingWeight => TotalWeight - SpentWeight;
        public IReadOnlyList<EngineerPerk> Perks { get; }
    }

    public sealed class EngineerGenerationResult
    {
        public EngineerGenerationResult(int seed, IReadOnlyList<EngineerBuild> engineers)
        {
            Seed = seed;
            Engineers = engineers;
        }

        public int Seed { get; }
        public IReadOnlyList<EngineerBuild> Engineers { get; }
    }
}
