using UnityEngine;

using Assets.Failsafe.Scripts.RandomGeneration;

namespace Failsafe.Scripts.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ScriptableObjects/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [field: SerializeField]
        public string MainMenuSceneName { get; private set; }

        [field: SerializeField]
        public string FirstGameplaySceneName { get; private set; }

        [Header("Engineer Generation")]
        [SerializeField]
        private EngineerGenerationConfig _engineerGenerationConfig;

        public EngineerGenerationConfig EngineerGenerationConfig =>
            _engineerGenerationConfig;

        [Header("Run Autosave")]
        [SerializeField, Min(10f)]
        private float _runAutosaveIntervalSeconds = 120f;

        [SerializeField, Min(0.25f)]
        private float _runAutosaveRetrySeconds = 2f;

        [SerializeField, Min(0.1f)]
        private float _checkpointGroundedSeconds = 1f;

        [SerializeField, Min(0.1f)]
        private float _checkpointCombatCooldownSeconds = 10f;

        [SerializeField, Min(0.1f)]
        private float _checkpointDotCooldownSeconds = 3f;

        public float RunAutosaveIntervalSeconds =>
            _runAutosaveIntervalSeconds > 0f
                ? _runAutosaveIntervalSeconds
                : 120f;

        public float RunAutosaveRetrySeconds =>
            _runAutosaveRetrySeconds > 0f
                ? _runAutosaveRetrySeconds
                : 2f;

        public float CheckpointGroundedSeconds =>
            _checkpointGroundedSeconds > 0f
                ? _checkpointGroundedSeconds
                : 1f;

        public float CheckpointCombatCooldownSeconds =>
            _checkpointCombatCooldownSeconds > 0f
                ? _checkpointCombatCooldownSeconds
                : 10f;

        public float CheckpointDotCooldownSeconds =>
            _checkpointDotCooldownSeconds > 0f
                ? _checkpointDotCooldownSeconds
                : 3f;
    }
}
