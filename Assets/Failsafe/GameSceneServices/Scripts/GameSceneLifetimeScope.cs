using Failsafe.GameSceneServices.SpawnSystem;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.SaveSystem;
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
            builder.Register<EnemyRuntimeRegistry>(Lifetime.Scoped)
                .AsSelf();

            builder.RegisterComponent(_enemySpawnSystemBuilder);

            builder.RegisterComponentInHierarchy<SignalManager>();

            builder.RegisterEntryPoint<PlacedEnemyRegistrationService>(Lifetime.Scoped);

            builder.RegisterEntryPoint<EnemySpawnSystem>()
                .As<IEnemySpawnSystem>()
                .AsSelf();

            builder.RegisterEntryPoint<WorldRunSaveParticipant>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EnemyRunSaveParticipant>(Lifetime.Singleton);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterEntryPoint<RunSaveDebugHotkey>(Lifetime.Scoped);
#endif

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
