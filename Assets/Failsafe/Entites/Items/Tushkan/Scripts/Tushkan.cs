
using Failsafe.PlayerMovements;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;

namespace Failsafe.Items
{
    public class Tushkan : IUsable
    {
        private TushkanData _data;
        private readonly IEffectManager _effectManager;
        private TushkanEffect _effect;
        private Item _item;

        public Tushkan(TushkanData data, PlayerMovementParameters playerMovementParameters, IEffectManager effectManager)
        {
            _data = data;
            _effectManager = effectManager;
            _effect = new TushkanEffect(_data.Duration, playerMovementParameters, _data.JumpMultiplier);
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
