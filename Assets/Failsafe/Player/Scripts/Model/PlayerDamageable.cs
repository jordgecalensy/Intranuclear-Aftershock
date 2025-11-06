using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Damage.Providers;
using Failsafe.Scripts.Health;
using Failsafe.Player.View;
using VContainer.Unity;

namespace Failsafe.Player.Model
{
    /// <summary>
    /// Получение урона
    /// </summary>
    public class PlayerDamageable : IInitializable
    {
        private readonly PlayerHealth _health;
        private IDamageService _damageService = new DamageService();
        private DamageableComponent _damageableComponent;

        public PlayerDamageable(PlayerHealth health, DamageableComponent damageable)
        {
            _health = health;
            _damageableComponent = damageable;
        }

        public void Initialize()
        {
            _damageService.Register(new FlatDamageProvider(_health));
            _damageService.Register(new FireContactDamageProvider(_health));
            _damageService.Register(new FireDotTickDamageProvider(_health));
            _damageService.Register(new FireDamageProvider(_health));
            _damageableComponent.OnTakeDamage += OnTakeDamage;
        }

        private void OnTakeDamage(IDamage damage)
        {
            _damageService.Provide(damage);
        }
    }
}