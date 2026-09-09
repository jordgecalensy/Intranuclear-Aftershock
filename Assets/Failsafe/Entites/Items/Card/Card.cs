using Failsafe.Player.Model;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Modifiebles;
using System;
using System.Collections;
using UnityEngine;

namespace Failsafe.Items
{
    public class Card : IUsable
    {
        private Item _item;

        public ItemUseResult Use()
        {
            return new ItemUseResult { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
        }

        public ItemUseResult AltMode()
        {
            return new ItemUseResult() { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
        }

        public void ParseItem(Item item_object)
        {
            _item = item_object;
        }
        public void GetItemUseDelays(out float startDelay, out float useDelay, out float startAltUseDelay, out float altUseDelay)
        {
            if (_item == null || _item.ItemData == null)
            {
                startDelay = 0f;
                useDelay = 0f;
                startAltUseDelay = 0f;
                altUseDelay = 0f;
                return;
            }

            startDelay = Mathf.Max(0f, _item.ItemData.StartUseDelay);
            useDelay = Mathf.Max(0f, _item.ItemData.UseDelay);
            startAltUseDelay = Mathf.Max(0f, _item.ItemData.StartAltUseDelay);
            altUseDelay = Mathf.Max(0f, _item.ItemData.UseAltDelay);
        }
    }
}