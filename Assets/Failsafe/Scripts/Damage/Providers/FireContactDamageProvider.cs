using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Health;

namespace Failsafe.Scripts.Damage.Providers
{
    public sealed class FireContactDamageProvider : IDamageProvider<FireContactDamage>
    {
        private readonly IHealth _health;
        public FireContactDamageProvider(IHealth health) => _health = health;

        public void Provide(FireContactDamage damage)
        {
            if (_health.IsDead) return;
            if (damage.Amount <= 0f) return;
            _health.AddHealth(-damage.Amount);
        }
    }
}