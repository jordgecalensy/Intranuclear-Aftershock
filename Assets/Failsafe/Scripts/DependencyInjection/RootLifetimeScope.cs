using Assets.Failsafe.Scripts.RandomGeneration;
using Failsafe.Scripts.Bootstrap;
using Failsafe.Scripts.Configs;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.Scripts.DependencyInjection
{
    public class RootLifetimeScope : LifetimeScope
    {
        [SerializeField]
        private GameConfig _gameConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(_gameConfig)
                .As<GameConfig>();

            builder.Register<SceneLoader.SceneLoader>(Lifetime.Singleton)
                .As<ISceneLoader>();

            builder.Register<IRunSaveRepository>(
                _ => new RunSaveRepository(),
                Lifetime.Singleton);

            builder.Register<RunSaveParticipantRegistry>(Lifetime.Singleton)
                .AsSelf();

            builder.Register<RunSaveService>(Lifetime.Singleton)
                .As<IRunSaveService>();

            builder.Register<RunSessionCoordinator>(Lifetime.Singleton)
                .As<IRunSessionCoordinator>();

            builder.RegisterEntryPoint<Bootstrapper>();
            builder.Register<RandomGenerator>(Lifetime.Singleton);
            builder.Register<EngineerBuildGenerator>(Lifetime.Singleton);
            builder.Register<EngineerSelectionState>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EngineerRunSaveParticipant>(Lifetime.Singleton)
                .AsSelf();
        }
    }
}
