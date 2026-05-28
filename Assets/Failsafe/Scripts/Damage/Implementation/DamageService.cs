using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.Damage.Implementation
{
    public class DamageService : IDamageService
    {
        private readonly Dictionary<Type, IDamageProvider> _damageProviders = new();

        public DamageService(IEnumerable<IDamageProvider> providers)
        {
            if (providers == null)
                return;

            foreach (var provider in providers)
                Register(provider);
        }

        public void Provide(IDamage damage)
        {
            if (damage == null)
                return;

            if (!_damageProviders.TryGetValue(damage.GetType(), out var provider))
            {
                Debug.LogWarning($"There is no damage provider for damage type {damage.GetType().Name}");
                return;
            }

            provider.Provide(damage);
        }

        public void Register(IDamageProvider provider)
        {
            if (provider == null)
                return;

            _damageProviders[provider.Type] = provider;
        }

        public void Unregister(IDamageProvider provider)
        {
            if (provider == null)
                return;

            if (_damageProviders.TryGetValue(provider.Type, out var current) && current == provider)
                _damageProviders.Remove(provider.Type);
        }
    }
}