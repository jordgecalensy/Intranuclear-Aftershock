using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Health;
using VContainer.Unity;

namespace Failsafe.Player.Model
{
    public class PlayerDamageable : IInitializable
    {
        private readonly PlayerHealth _health;
        private readonly DamageableComponent _damageableComponent;

        public PlayerDamageable(
            PlayerHealth health,
            DamageableComponent damageableComponent)
        {
            _health = health;
            _damageableComponent = damageableComponent;
        }

        public void Initialize()
        {
            // Логика урона теперь находится в DamageableComponent + DamageService.
            // Этот класс оставлен временно, чтобы не ломать регистрации в LifetimeScope.
        }
    }
}