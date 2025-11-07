using Failsafe.Enemies;
using Failsafe.Player.Model;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Health;
using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Enemies
{
    /// <summary>
    /// Регистрация компонентов врага
    /// <para/>Дочерний скоуп к <see cref="Failsafe.GameSceneServices.GameSceneLifetimeScope"/>
    /// </summary>
    public class EnemyLifetimeScope : LifetimeScope
    {
        private Enemy_ScriptableObject _enemy_parameters;
        private DamageableComponent _damageable;

        protected override void Configure(IContainerBuilder builder)
        {
            _enemy_parameters = this.GetComponent<Enemy>().EnemyConfig;
            _damageable = this.GetComponent<DamageableComponent>();
            builder.RegisterComponent(_damageable);
            builder.RegisterInstance(_enemy_parameters);
            builder.Register<IHealth, PlayerHealth>(Lifetime.Singleton).AsSelf().WithParameter(_enemy_parameters.enemyHealth);
            builder.RegisterEntryPoint<PlayerDamageable>(Lifetime.Scoped);
        }
    }
}
