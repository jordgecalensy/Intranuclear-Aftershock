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
        private readonly IEffectManager _effectManager; // ← добавь это поле

        public Stimpack(PlayerHealth playerHealth, StimpackData data, IEffectManager effectManager)
        {
            _playerHealth = playerHealth;
            _data = data;
            _maxHealthModificator = new AdderFloat(_data.MaxHealthBonus);
            _effectManager = effectManager; // ← присваиваем
            _effect = new StimpackEffect(_data.Duration, _playerHealth, _maxHealthModificator, _data.HealAmount);
        }

        public ItemUseResult Use()
        {
            // Добавляем эффект стимпака
            _effectManager.ApplyEffect(_effect);
            return ItemUseResult.Consumed;
        }

        public void GetItemUseDelays(out float startUseDelay, out float useDelay)
        {
            startUseDelay = _data.StartUseDelay;
            useDelay = _data.UseDelay;
        }
    }
}