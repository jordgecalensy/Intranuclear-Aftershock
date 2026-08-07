using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [CreateAssetMenu(
        fileName = "PerkDefinition",
        menuName = "Failsafe/Perks/Perk Definition")]
    public sealed class PerkDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private bool _isNegative;
        [SerializeField] private EffectBundle _effectBundle;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public bool IsNegative => _isNegative;
        public EffectBundle EffectBundle => _effectBundle;
    }
}
