using Failsafe.Scripts.Health;

namespace Failsafe.Scripts.Damage.Providers
{
    public sealed class DamageInfoProvider : IDamageProvider<DamageInfo>
    {
        private readonly IHealth _health;

        public DamageInfoProvider(IHealth health)
        {
            _health = health;
        }

        public void Provide(DamageInfo damage)
        {
            if (_health == null)
                return;

            if (_health.IsDead)
                return;

            if (damage.Amount <= 0f)
                return;

            _health.AddHealth(-damage.Amount);
        }
    }
}