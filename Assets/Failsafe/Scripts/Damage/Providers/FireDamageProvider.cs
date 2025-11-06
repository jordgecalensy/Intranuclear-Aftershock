using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Health;

namespace Failsafe.Scripts.Damage.Providers
{
    public sealed class FireDamageProvider : IDamageProvider<FireDamage>
    {
        private readonly IHealth _health;
        public FireDamageProvider(IHealth health) => _health = health;
        public void Provide(FireDamage damage)
        {
            if (_health.IsDead) return;
            _health.AddHealth(-damage.DamagePerTick * UnityEngine.Time.deltaTime);
        }
    }
}