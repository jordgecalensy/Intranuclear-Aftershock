using Failsafe.Scripts.Configs;
using VContainer.Unity;

namespace Failsafe.Scripts.Bootstrap
{
    public class Bootstrapper : IStartable
    {
        private readonly ISceneLoader _sceneLoader;
        private readonly GameConfig _gameConfig;

        public Bootstrapper(ISceneLoader sceneLoader, GameConfig gameConfig)
        {
            _sceneLoader = sceneLoader;
            _gameConfig = gameConfig;
        }

        public async void Start()
        {
            //logic after container build & IInitializable

#if UNITY_EDITOR
            UnityEngine.SceneManagement.Scene activeScene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // RootLifetimeScope is created before scene entry points. Reloading the
            // same editor scene creates overlapping GameSceneLifetimeScopes and
            // duplicate run-save participants.
            if (activeScene.IsValid() &&
                !string.IsNullOrWhiteSpace(activeScene.name))
            {
                return;
            }
#endif

            await _sceneLoader.LoadSceneAsync(_gameConfig.MainMenuSceneName);
        }
    }
}
