using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.Modifiebles;
using UnityEngine;
using System.Collections;

namespace Failsafe.Items
{
    public class Stimpack : IUsable
    {
        private PlayerHealth _playerHealth;
        private StimpackData _data;
        private AdderFloat _maxHealthModificator;
        private StimpackEffect _effect;
        private Item _item;

        private readonly IEffectManager _effectManager; // ← добавь это поле

        public Stimpack(PlayerHealth playerHealth, StimpackData data, IEffectManager effectManager)
        {
            _playerHealth = playerHealth;
            _data = data;
            _maxHealthModificator = new AdderFloat(_data.MaxHealthBonus);
            _effectManager = effectManager;
            _effect = new StimpackEffect(_data.Duration, _playerHealth, _maxHealthModificator, _data.HealAmount);
        }

        public ItemUseResult Use()
        {
            // Добавляем эффект стимпака
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