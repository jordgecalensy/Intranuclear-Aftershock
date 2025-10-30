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
        private readonly IEffectManager _effectManager; // ← добавь это поле

        public Stimpack(PlayerHealth playerHealth, StimpackData data, IEffectManager effectManager)
        {
            _playerHealth = playerHealth;
            _data = data;
            _maxHealthModificator = new AdderFloat(_data.MaxHealthBonus);
            _effectManager = effectManager; // ← присваиваем
        }

        public ItemUseResult Use()
        {
            _playerHealth.AddHealth(_data.HealAmount);
            _playerHealth.ModifyMaxHealth(_maxHealthModificator);
            // Добавляем эффект стимпака
            _effectManager.ApplyEffect(new StimpackEffect(_data.Duration));
            return ItemUseResult.Consumed;
        }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }
}