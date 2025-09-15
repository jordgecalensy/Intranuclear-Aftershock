using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Health;

namespace Failsafe.Scripts.Damage.Providers
{
    public sealed class FireDotTickDamageProvider : IDamageProvider<FireDotTickDamage>
    {
        private readonly IHealth _health;
        public FireDotTickDamageProvider(IHealth health) => _health = health;

        public void Provide(FireDotTickDamage damage)
        {
            if (_health.IsDead) return;
            if (damage.Amount <= 0f) return;
            _health.AddHealth(-damage.Amount);
        }
    }
}