using System;
using Cysharp.Threading.Tasks;
using Failsafe.Scripts.Configs;

namespace Failsafe.Scripts.SaveSystem
{
    public interface IRunSessionCoordinator
    {
        bool IsBusy { get; }

        UniTask<RunSaveOperationResult> StartNewRunAsync();
        UniTask<RunSaveOperationResult> ContinueRunAsync();
        UniTask<RunSaveOperationResult> ReturnToMainMenuAsync();
    }

    public sealed class RunSessionCoordinator : IRunSessionCoordinator
    {
        private readonly IRunSaveService _runSaveService;
        private readonly ISceneLoader _sceneLoader;
        private readonly GameConfig _gameConfig;

        public bool IsBusy { get; private set; }

        public RunSessionCoordinator(
            IRunSaveService runSaveService,
            ISceneLoader sceneLoader,
            GameConfig gameConfig)
        {
            _runSaveService = runSaveService ?? throw new ArgumentNullException(nameof(runSaveService));
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _gameConfig = gameConfig ?? throw new ArgumentNullException(nameof(gameConfig));
        }

        public async UniTask<RunSaveOperationResult> StartNewRunAsync()
        {
            if (IsBusy)
                return BusyFailure();

            string firstSceneName = NormalizeSceneName(_gameConfig.FirstGameplaySceneName);
            string validationError = ValidateSceneName(firstSceneName, nameof(GameConfig.FirstGameplaySceneName));
            if (validationError != null)
                return RunSaveOperationResult.Failure(validationError);

            validationError = ValidateMainMenuSceneName();
            if (validationError != null)
                return RunSaveOperationResult.Failure(validationError);

            IsBusy = true;

            try
            {
                RunSaveOperationResult sceneLoadResult = await TryLoadSceneAsync(firstSceneName);
                if (!sceneLoadResult.Succeeded)
                    return sceneLoadResult;

                RunSaveOperationResult beginResult =
                    _runSaveService.BeginNewRunWithCheckpoint(firstSceneName, 0, 0);
                if (beginResult.Succeeded)
                    return beginResult;

                return await ReturnToMainMenuAfterFailureAsync(beginResult.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async UniTask<RunSaveOperationResult> ContinueRunAsync()
        {
            if (IsBusy)
                return BusyFailure();

            string validationError = ValidateMainMenuSceneName();
            if (validationError != null)
                return RunSaveOperationResult.Failure(validationError);

            IsBusy = true;
            bool restorePrepared = false;

            try
            {
                RunSaveOperationResult loadResult = _runSaveService.LoadRun();
                if (!loadResult.Succeeded)
                    return loadResult;

                RunSaveFile saveSnapshot = _runSaveService.CurrentSave;
                if (saveSnapshot != null && !saveSnapshot.IsActive)
                {
                    return RunSaveOperationResult.Failure(
                        "This run has ended and cannot be continued. Start a new run.");
                }

                if (saveSnapshot?.checkpoint == null || !saveSnapshot.checkpoint.hasCheckpoint)
                {
                    return RunSaveOperationResult.Failure(
                        "The run save does not contain a checkpoint to continue from.");
                }

                string checkpointSceneName = NormalizeSceneName(saveSnapshot.checkpoint.sceneId);
                validationError = ValidateSceneName(checkpointSceneName, "checkpoint scene id");
                if (validationError != null)
                    return RunSaveOperationResult.Failure(validationError);

                RunSaveOperationResult prepareResult =
                    _runSaveService.PrepareCheckpointRestore();
                if (!prepareResult.Succeeded)
                    return prepareResult;

                restorePrepared = true;

                RunSaveOperationResult sceneLoadResult = await TryLoadSceneAsync(checkpointSceneName);
                if (!sceneLoadResult.Succeeded)
                    return sceneLoadResult;

                RunSaveOperationResult restoreResult = await _runSaveService.RestoreCheckpointAsync();
                if (!restoreResult.Succeeded)
                    return await ReturnToMainMenuAfterFailureAsync(restoreResult.Error);

                return RunSaveOperationResult.Success(loadResult.LoadedFromBackup);
            }
            finally
            {
                if (restorePrepared)
                    _runSaveService.CancelCheckpointRestore();

                IsBusy = false;
            }
        }

        public async UniTask<RunSaveOperationResult> ReturnToMainMenuAsync()
        {
            if (IsBusy)
                return BusyFailure();

            string mainMenuSceneName =
                NormalizeSceneName(_gameConfig.MainMenuSceneName);
            string validationError =
                ValidateSceneName(
                    mainMenuSceneName,
                    nameof(GameConfig.MainMenuSceneName));

            if (validationError != null)
                return RunSaveOperationResult.Failure(validationError);

            IsBusy = true;

            try
            {
                return await TryLoadSceneAsync(mainMenuSceneName);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async UniTask<RunSaveOperationResult> TryLoadSceneAsync(string sceneName)
        {
            try
            {
                await _sceneLoader.LoadSceneAsync(sceneName);
                return RunSaveOperationResult.Success();
            }
            catch (Exception exception)
            {
                return RunSaveOperationResult.Failure(
                    $"Failed to load scene '{sceneName}': {exception.Message}");
            }
        }

        private async UniTask<RunSaveOperationResult> ReturnToMainMenuAfterFailureAsync(string operationError)
        {
            string mainMenuSceneName = NormalizeSceneName(_gameConfig.MainMenuSceneName);
            RunSaveOperationResult menuLoadResult = await TryLoadSceneAsync(mainMenuSceneName);

            if (menuLoadResult.Succeeded)
                return RunSaveOperationResult.Failure(operationError);

            return RunSaveOperationResult.Failure(
                $"{operationError} Returning to the main menu also failed: {menuLoadResult.Error}");
        }

        private string ValidateMainMenuSceneName()
        {
            return ValidateSceneName(
                NormalizeSceneName(_gameConfig.MainMenuSceneName),
                nameof(GameConfig.MainMenuSceneName));
        }

        private static string ValidateSceneName(string sceneName, string settingName)
        {
            return string.IsNullOrEmpty(sceneName)
                ? $"{settingName} is not configured in GameConfig."
                : null;
        }

        private static string NormalizeSceneName(string sceneName)
        {
            return sceneName?.Trim();
        }

        private static RunSaveOperationResult BusyFailure()
        {
            return RunSaveOperationResult.Failure(
                "Another run start or continue operation is already in progress.");
        }
    }
}
