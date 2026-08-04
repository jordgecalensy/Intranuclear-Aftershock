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

        public RandomGenerator()
        {
            _rndSeed = (int)DateTime.Now.Ticks;
            _rnd = new System.Random(_rndSeed);
            Debug.Log("Seed: " + _rndSeed);
            //save seed here
        }

        public List<RandomizationItem> BlessRNG(RandomGeneratorInput input, int minWeight, int maxWeight)
        {
            _inputList = new List<RandomizationItem>();
            _totalWeight = _rnd.Next(minWeight, maxWeight);
            foreach (RandomizationItem rndItem in input.GetItems)
                _inputList.Add(rndItem);
            _inputList.Shuffle(_rnd);
            _removeItem = input.GetRemoveItem;

            List<RandomizationItem> properList = CreateProperList(_inputList, _totalWeight, _removeItem);

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

        private List<RandomizationItem> CreateProperList(List<RandomizationItem> itemList, int weight, bool removeItem)
        {
            List<RandomizationItem> properList = new List<RandomizationItem>();
            int counter = 0;
            while (counter < weight) 
            {
                //Select rarity
                ItemRarity selectedRarity = RollRarity();

                //Get first item which rarity matches selected
                RandomizationItem selectedItem = itemList.Find(item => item.Rarity == selectedRarity);

                //Remove item and excluded if needed
                if (removeItem)
                    itemList.Remove(selectedItem);

                if (selectedItem.Exclude.Length > 0)
                    foreach (String itemName in selectedItem.Exclude)
                    {
                        RandomizationItem excludedItem = itemList.Find(item => item.Name == itemName);
                        itemList.Remove(excludedItem);
                    }

                properList.Add(selectedItem);

                counter += selectedItem.Weight;
            }

            return properList;
        }
    }
}