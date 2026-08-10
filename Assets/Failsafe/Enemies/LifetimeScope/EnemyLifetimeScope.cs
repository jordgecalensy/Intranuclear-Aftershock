using Failsafe.Player.Model;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Damage.Providers;
using Failsafe.Scripts.Health;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Enemies
{
    public class EnemyLifetimeScope : LifetimeScope
    {
        private Enemy_ScriptableObject _enemyParameters;
        private DamageableComponent _damageable;

        protected override void Configure(IContainerBuilder builder)
        {
            Enemy enemy = GetComponent<Enemy>();

            if (enemy == null)
            {
                Debug.LogError("[EnemyLifetimeScope] Enemy component не найден.", this);
                return;
            }

            _enemyParameters = enemy.EnemyConfig;
            _damageable = GetComponent<DamageableComponent>();

            if (_damageable == null)
                Debug.LogError("[EnemyLifetimeScope] DamageableComponent не найден.", this);

            builder.RegisterComponent(_damageable);
            builder.RegisterInstance(_enemyParameters);

            builder.Register<IHealth, PlayerHealth>(Lifetime.Singleton)
                .AsSelf()
                .WithParameter(_enemyParameters.enemyHealth);

            builder.Register<FlatDamageProvider>(Lifetime.Scoped)
                .As<IDamageProvider>();

            builder.Register<DamageInfoProvider>(Lifetime.Scoped)
                .As<IDamageProvider>();

            builder.Register<DamageService>(Lifetime.Scoped)
                .As<IDamageService>()
                .AsSelf();

            builder.RegisterEntryPoint<PlayerDamageable>(Lifetime.Scoped);
        }
    }
}