using System.Collections.Generic;
using System;
using UnityEngine;

namespace Assets.Failsafe.Scripts.RandomGeneration
{
    [Serializable]
    public class RandomGenerator
    {
        private List<RandomizationItem> _inputList;
        private int _totalWeight;
        private bool _removeItem;
        private static Int32 _rndSeed;
        private System.Random _rnd;

        public int Seed => _rndSeed;

        public RandomGenerator()
        {
            BeginRun();
        }

        public void BeginRun(int? seed = null)
        {
            _rndSeed = seed ?? (int)DateTime.Now.Ticks;
            _rnd = new System.Random(_rndSeed);
            Debug.Log("Seed: " + _rndSeed);
            //save seed here
        }

        public List<RandomizationItem> BlessRNG(RandomGeneratorInput input, int minWeight, int maxWeight)
        {
            return BlessRNG(input, minWeight, maxWeight, int.MaxValue, out _);
        }

        public List<RandomizationItem> BlessRNG(
            RandomGeneratorInput input,
            int minWeight,
            int maxWeight,
            int maxItems,
            out int totalWeight)
        {
            _inputList = new List<RandomizationItem>();
            _totalWeight = _rnd.Next(minWeight, maxWeight);
            totalWeight = _totalWeight;
            foreach (RandomizationItem rndItem in input.GetItems)
                _inputList.Add(rndItem);
            _inputList.Shuffle(_rnd);
            _removeItem = input.GetRemoveItem;

            List<RandomizationItem> properList = CreateProperList(
                _inputList,
                _totalWeight,
                _removeItem,
                maxItems);

            return properList;
        }

        private ItemRarity RollRarity()
        {
            int roll = _rnd.Next(100);
            switch (roll)
            {
                case >= (int)ItemRarity.Unique:
                    return ItemRarity.Unique;
                case >= (int)ItemRarity.Rare:
                    return ItemRarity.Rare;
                case >= (int)ItemRarity.Uncommon:
                    return ItemRarity.Uncommon;
                default:
                    return ItemRarity.Common;
            }
        }

        private List<RandomizationItem> CreateProperList(
            List<RandomizationItem> itemList,
            int weight,
            bool removeItem,
            int maxItems)
        {
            List<RandomizationItem> properList = new List<RandomizationItem>();
            int counter = 0;
            while (counter < weight &&
                   itemList.Count > 0 &&
                   properList.Count < maxItems)
            {
                //Select rarity
                ItemRarity selectedRarity = RollRarity();

                //Get first item which rarity matches selected
                int selectedItemIndex = itemList.FindIndex(item => item.Rarity == selectedRarity);

                //The rolled rarity may be absent after removals or exclusions.
                //The list is already shuffled, so the first remaining item is a safe fallback.
                if (selectedItemIndex < 0)
                    selectedItemIndex = 0;

                RandomizationItem selectedItem = itemList[selectedItemIndex];

                //Remove item and excluded if needed
                if (removeItem)
                    itemList.RemoveAt(selectedItemIndex);

                if (selectedItem.Exclude != null && selectedItem.Exclude.Length > 0)
                    foreach (String itemName in selectedItem.Exclude)
                    {
                        int excludedItemIndex = itemList.FindIndex(item => item.Name == itemName);

                        if (excludedItemIndex >= 0)
                            itemList.RemoveAt(excludedItemIndex);
                    }

                properList.Add(selectedItem);

                counter += selectedItem.Weight;
            }

            return properList;
        }
    }
}
