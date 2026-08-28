using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "GameplayEffectCatalog",
        menuName = "Failsafe/Effects/Gameplay Effect Catalog")]
    public sealed class GameplayEffectCatalog : ScriptableObject
    {
        [Header("Player Feedback")]
        [SerializeField] private EffectBundle _movementCameraShake;
        [SerializeField] private EffectBundle _damageFeedback;
        [SerializeField] private EffectBundle _playerEarthquake;

        [Header("Environment")]
        [SerializeField] private EffectBundle _earthquakeEnvironment;

        [Header("Player State")]
        [SerializeField] private EffectBundle _landingSlow;
        [SerializeField] private EffectBundle _lowHealth;
        [SerializeField] private EffectBundle _visor;

        public EffectBundle MovementCameraShake => _movementCameraShake;
        public EffectBundle DamageFeedback => _damageFeedback;
        public EffectBundle PlayerEarthquake => _playerEarthquake;
        public EffectBundle EarthquakeEnvironment => _earthquakeEnvironment;
        public EffectBundle LandingSlow => _landingSlow;
        public EffectBundle LowHealth => _lowHealth;
        public EffectBundle Visor => _visor;
    }
}
