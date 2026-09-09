using Failsafe.Player.Model;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Modifiebles;
using System;
using System.Collections;
using UnityEngine;

namespace Failsafe.Items
{
    public class Gorilla : IUsable
    {
        private GorillaData _data;
        private Item _item;

        private readonly IEffectManager _effectManager;
        private GorillaEffect _effect;
        public Gorilla(GorillaData data, PlayerModelParameters playerModelParameters, IEffectManager effectManager)
        {
            _data = data;
            _effectManager = effectManager;
            _effect = new GorillaEffect(_data.Duration, playerModelParameters, _data.ThrowPowerMultiplier);
        }


        public ItemUseResult Use()
        {
            _effectManager.ApplyEffect(_effect);
            return ItemUseResult.Consumed;
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