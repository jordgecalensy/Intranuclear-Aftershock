using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Failsafe.Scripts.SaveSystem
{
    [Obsolete("Use IRunSaveService from gameplay code. This component is a temporary UnityEvent bridge.")]
    public sealed class SaveLoadManager : MonoBehaviour
    {
        private IRunSaveService _runSaveService;

        [Inject]
        public void Construct(IRunSaveService runSaveService)
        {
            _runSaveService = runSaveService;
        }

        public bool BeginNewRun()
        {
            return LogResult(_runSaveService?.BeginNewRun(), "New run started.");
        }

        public bool SaveCheckpoint(string sceneId, int floorIndex, int dungeonSeed)
        {
            return LogResult(
                _runSaveService?.SaveCheckpoint(sceneId, floorIndex, dungeonSeed),
                "Run checkpoint saved.");
        }

        public bool SaveCurrentCheckpoint()
        {
            RunCheckpointData checkpoint = _runSaveService?.CurrentSave?.checkpoint;
            if (checkpoint == null || !checkpoint.hasCheckpoint)
            {
                Debug.LogError(
                    "Cannot repeat the current checkpoint save before an initial checkpoint has been created.",
                    this);
                return false;
            }

            return SaveCheckpoint(checkpoint.sceneId, checkpoint.floorIndex, checkpoint.dungeonSeed);
        }

        public void LoadGame()
        {
            LoadAndRestoreAsync().Forget();
        }

        private async UniTask LoadAndRestoreAsync()
        {
            if (_runSaveService == null)
            {
                Debug.LogError("IRunSaveService was not injected into SaveLoadManager.", this);
                return;
            }

            RunSaveOperationResult loadResult = _runSaveService.LoadRun();
            if (!LogResult(loadResult, "Run save loaded."))
                return;

            RunSaveOperationResult restoreResult = await _runSaveService.RestoreCheckpointAsync();
            LogResult(restoreResult, "Run checkpoint restored.");
        }

        private bool LogResult(RunSaveOperationResult? result, string successMessage)
        {
            if (!result.HasValue)
            {
                Debug.LogError("IRunSaveService was not injected into SaveLoadManager.", this);
                return false;
            }

            if (!result.Value.Succeeded)
            {
                Debug.LogError(result.Value.Error, this);
                return false;
            }

            string backupSuffix = result.Value.LoadedFromBackup ? " The backup file was used." : string.Empty;
            Debug.Log(successMessage + backupSuffix, this);
            return true;
        }
    }
}
