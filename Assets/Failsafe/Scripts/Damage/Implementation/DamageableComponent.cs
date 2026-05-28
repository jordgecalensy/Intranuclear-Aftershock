using System;
using Failsafe.Scripts.Damage;
using UnityEngine;
using VContainer;

namespace Failsafe.Scripts.Damage.Implementation
{
    public class DamageableComponent : MonoBehaviour, IDamageable
    {
        public event Action<IDamage> OnTakeDamage = delegate { };

        private IDamageService _damageService;

        [Inject]
        public void Construct(IDamageService damageService)
        {
            _damageService = damageService;
        }

        public void TakeDamage(IDamage damage)
        {
            if (damage == null)
                return;

            OnTakeDamage?.Invoke(damage);
            _damageService?.Provide(damage);
        }
    }
}