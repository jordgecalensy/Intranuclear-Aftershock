using System;
using Failsafe.Scripts.Health;

namespace Failsafe.Scripts.Damage.Providers
{
    public sealed class DamageInfoProvider : IDamageProvider
    {
        private readonly IHealth _health;

        public Type Type => typeof(DamageInfo);

        public DamageInfoProvider(IHealth health)
        {
            _health = health;
        }

        public void Provide(IDamage damage)
        {
            if (damage is not DamageInfo damageInfo)
                return;

            Provide(damageInfo);
        }

        private void Provide(DamageInfo damage)
        {
            if (_health == null)
                return;

            if (damage.Amount <= 0f)
                return;

            _health.AddHealth(-damage.Amount);
        }
    }
}