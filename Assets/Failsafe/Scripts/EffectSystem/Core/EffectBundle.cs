using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(menuName = "Effects/Effect Bundle")]
    public sealed class EffectBundle : ScriptableObject
    {
        [SerializeField] private EffectDefinition[] _effects;

        public EffectDefinition[] Effects => _effects;
    }
}