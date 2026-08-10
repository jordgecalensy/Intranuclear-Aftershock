using UnityEditor;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;
using System.Collections.Generic;
namespace Assets.Failsafe.Scripts.RandomGeneration
{
    public enum ItemRarity
    {
        Unique = 95,
        Rare = 85,
        Uncommon = 60,
        Common = 0
    }
    [System.Serializable]
    public struct RandomizationItem
    {
        [SerializeField]
        private string _name;
        public string Name => _name;
        [SerializeField]
        private ItemRarity _rarity;
        public ItemRarity Rarity => _rarity;
        [SerializeField]
        private int _weight;
        public int Weight => _weight;
        [SerializeField]
        private string[] _exclude;
        public string[] Exclude => _exclude;
    }
    [CreateAssetMenu(fileName = "RandomGeneratorList", menuName = "ScriptableObjects/RandomGeneratorList")]
    public class RandomGeneratorInput : ScriptableObject
    {
        [SerializeField]
        private bool _removeItemAfterSelection = false;
        public bool GetRemoveItem => _removeItemAfterSelection;
        [SerializeField]
        private List<RandomizationItem> _items;
        public List<RandomizationItem> GetItems => _items;
    }
}