using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "StatusReactionProfile",
        menuName = "Failsafe/Effects/Statuses/Reaction Profile")]
    public class StatusReactionProfile : ScriptableObject
    {
        [Header("Wet + Cold => Frozen")]
        [SerializeField] private int _minColdStageForFrozen = 2;

        [Tooltip("Применяется, когда Wet + Cold дают Frozen.")]
        [SerializeField] private EffectBundle _frozenReactionBundle;

        [Header("Wet + Shock => Stun")]
        [Tooltip("Применяется, когда Wet + Shock дают Stun.")]
        [SerializeField] private EffectBundle _stunReactionBundle;

        public int MinColdStageForFrozen => Mathf.Max(1, _minColdStageForFrozen);
        public EffectBundle FrozenReactionBundle => _frozenReactionBundle;
        public EffectBundle StunReactionBundle => _stunReactionBundle;
    }
}