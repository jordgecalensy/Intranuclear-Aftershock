using System;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    [Serializable]
    public sealed class PerkStartingItemGrant
    {
        [SerializeField] private ItemData _item;
        [SerializeField, Min(1)] private int _quantity = 1;

        public ItemData Item => _item;
        public int Quantity => Mathf.Max(1, _quantity);
    }

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

        [Header("Starting Items")]
        [SerializeField] private PerkStartingItemGrant[] _startingItems =
            Array.Empty<PerkStartingItemGrant>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public bool IsNegative => _isNegative;
        public EffectBundle EffectBundle => _effectBundle;
        public PerkStartingItemGrant[] StartingItems =>
            _startingItems ?? Array.Empty<PerkStartingItemGrant>();
    }
}
