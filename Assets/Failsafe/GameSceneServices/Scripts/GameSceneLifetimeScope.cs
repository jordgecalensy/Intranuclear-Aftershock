using Failsafe.Scripts.EffectSystem;
using Failsafe.GameSceneServices.SpawnSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.GameSceneServices
{
    /// <summary>
    /// Регистрация сервисов и компонентов игровой сцены, общих для объектов на сцене или не привязанных к конкретному объекту.
    /// Дочерний скоуп к Failsafe.Scripts.DependencyInjection.RootLifetimeScope.
    /// </summary>
    public class GameSceneLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private EnemySpawnSystemBuilder _enemySpawnSystemBuilder;
        [SerializeField]
        private StatusReactionProfile _statusReactionProfile;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_enemySpawnSystemBuilder);

            builder.RegisterComponentInHierarchy<SignalManager>();

            builder.RegisterEntryPoint<EnemySpawnSystem>().AsSelf();

            builder.RegisterEntryPoint<EffectManager>()
                .As<IEffectManager>()
                .AsSelf();

            builder.RegisterInstance(_statusReactionProfile);

            builder.Register<StatusReactionService>(Lifetime.Scoped)
                .As<IStatusReactionService>()
                .AsSelf();

            builder.RegisterEntryPoint<EffectApplicationService>(Lifetime.Scoped)
                .As<IEffectApplicationService>()
                .AsSelf();
            
            builder.RegisterEntryPoint<EarthquakeEnvironmentController>().AsSelf();
        }
    }
}