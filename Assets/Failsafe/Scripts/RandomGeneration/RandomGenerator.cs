using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Assets.Failsafe.Scripts.RandomGeneration
{
    [Serializable]
    public class RandomGenerator
    {
        private List<RandomizationItem> _inputList;
        private int _totalWeight;
        private bool _removeItem;
        private System.Random _rnd = new System.Random();
        private Array _rarityList = Enum.GetValues(typeof(ItemRarity));

        public List<RandomizationItem> BlessRNG(RandomGeneratorInput input, int minWeight, int maxWeight)
        {
            _inputList = new List<RandomizationItem>();
            _totalWeight = _rnd.Next(minWeight, maxWeight);
            foreach (RandomizationItem rndItem in input.GetItems)
                _inputList.Add(rndItem);
            _inputList.Shuffle(_rnd);
            _removeItem = input.GetRemoveItem;

            List<ItemRarity> rarityList = CreateRarityList();

            List<RandomizationItem> properList = CreateProperList(_inputList, rarityList, _totalWeight, _removeItem);

            return properList;
        }

        private List<ItemRarity> CreateRarityList() 
        {
            List<ItemRarity> rarityList = new List<ItemRarity>();
            foreach (ItemRarity rarity in _rarityList) 
            {
                for (int extraItems = 0; extraItems < (int)rarity; extraItems++) 
                {
                    rarityList.Add(rarity);
                }
            }
            rarityList.Shuffle(_rnd);
            return rarityList;
        }

        private List<RandomizationItem> CreateProperList(List<RandomizationItem> itemList, List<ItemRarity> rarityList, int weight, bool removeItem) 
        {
            List<RandomizationItem> properList = new List<RandomizationItem>();
            int counter = 0;
            while (counter < weight) 
            {
                //Select rarity
                int idx = _rnd.Next(0, rarityList.Count-1);
                ItemRarity selectedRarity = rarityList[idx];
                //Debug.Log("Selected rarity: " + selectedRarity);

                //Get first item which rarity matches selected
                RandomizationItem selectedItem = itemList.Find(item => item.Rarity == selectedRarity);
                Debug.Log("Selected item: " + selectedItem.Name);

                //Remove item and excluded if needed
                foreach (RandomizationItem item in itemList) 
                {
                    if (removeItem && item.Name == selectedItem.Name || Array.Exists(selectedItem.Exclude, name => name == item.Name))
                    {
                        itemList.Remove(item);
                        Debug.Log("Removing item");
                    }
                }

                properList.Add(selectedItem);

                counter += selectedItem.Weight;
            }

            return properList;
        }
    }
}