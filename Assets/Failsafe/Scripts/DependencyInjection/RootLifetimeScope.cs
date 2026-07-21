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

            builder.Register<RunSaveRepository>(Lifetime.Singleton)
                .As<IRunSaveRepository>();

            builder.Register<RunSaveParticipantRegistry>(Lifetime.Singleton)
                .AsSelf();

            builder.Register<RunSaveService>(Lifetime.Singleton)
                .As<IRunSaveService>();

            builder.RegisterEntryPoint<Bootstrapper>();
        }
    }
}
