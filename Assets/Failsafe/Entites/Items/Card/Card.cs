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
        public ItemUseResult Use()
        {
            return new ItemUseResult { ItemStateAfterUse = ItemState.Hold, UsageType = UsageType.ClickToUse };
        }

        public void AltMode() { }

        public void ParseItem(Item item_object) { }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = 0;
            useDelay = 0;
        }
    }
}