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

        public void AltMode() { }

        public void ParseItem(Item item_object) { }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }
}