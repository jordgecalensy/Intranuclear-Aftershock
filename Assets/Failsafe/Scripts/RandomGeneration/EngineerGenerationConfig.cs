using Failsafe.Scripts.EffectSystem;
using UnityEngine;

namespace Assets.Failsafe.Scripts.RandomGeneration
{
    [CreateAssetMenu(
        fileName = "EngineerGenerationConfig",
        menuName = "Failsafe/Perks/Engineer Generation Config")]
    public sealed class EngineerGenerationConfig : ScriptableObject
    {
        [Header("Perks")]
        [SerializeField] private RandomGeneratorInput _perkPool;
        [SerializeField] private PerkDefinition[] _perkDefinitions;

        [Header("Engineer builds")]
        [SerializeField, Min(1)] private int _engineerCount = 3;
        [SerializeField, Min(1)] private int _maxPerksPerEngineer = 3;
        [SerializeField, Min(1)] private int _maxNegativePerksPerEngineer = 1;
        [SerializeField] private int _minTotalWeight = 70;
        [SerializeField] private int _maxTotalWeight = 90;
        [SerializeField, Min(1)] private int _maxAttemptsPerEngineer = 100;
        [SerializeField] private string[] _engineerNames;

        public RandomGeneratorInput PerkPool => _perkPool;
        public PerkDefinition[] PerkDefinitions => _perkDefinitions;
        public int EngineerCount => _engineerCount;
        public int MaxPerksPerEngineer => _maxPerksPerEngineer;
        public int MaxNegativePerksPerEngineer => _maxNegativePerksPerEngineer;
        public int MinTotalWeight => _minTotalWeight;
        public int MaxTotalWeight => _maxTotalWeight;
        public int MaxAttemptsPerEngineer => _maxAttemptsPerEngineer;
        public string[] EngineerNames => _engineerNames;
    }
}
