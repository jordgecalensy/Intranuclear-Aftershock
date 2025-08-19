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

            string sceneName;

#if UNITY_EDITOR
            // Загружается активная сцена. Если ее нет то загружется главное меню
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                ?? _gameConfig.MainMenuSceneName;
            await _sceneLoader.LoadSceneAsync(sceneName);
#else
            sceneName = _gameConfig.MainMenuSceneName;
            await _sceneLoader.LoadSceneAsync(sceneName);
#endif
        }
    }
}