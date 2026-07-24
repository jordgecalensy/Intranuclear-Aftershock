using System;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class RunSaveDebugHotkey : ITickable
    {
        private readonly IRunSaveService _runSaveService;

        public RunSaveDebugHotkey(IRunSaveService runSaveService)
        {
            _runSaveService =
                runSaveService ??
                throw new ArgumentNullException(nameof(runSaveService));
        }

        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f5Key.wasPressedThisFrame)
                return;

            SaveCheckpoint();
        }

        private void SaveCheckpoint()
        {
            RunSaveFile currentSave = _runSaveService.CurrentSave;
            if (currentSave == null || !currentSave.IsActive)
            {
                RunSaveLog.Warning(
                    RunSaveLog.DebugTools,
                    "F5 checkpoint was ignored because there is no active run.");
                return;
            }

            RunCheckpointData previousCheckpoint = currentSave.checkpoint;
            int floorIndex = previousCheckpoint?.floorIndex ?? 0;
            int dungeonSeed = previousCheckpoint?.dungeonSeed ?? 0;
            string sceneId = SceneManager.GetActiveScene().name;

            RunSaveOperationResult result = _runSaveService.SaveCheckpoint(
                sceneId,
                floorIndex,
                dungeonSeed);

            if (!result.Succeeded)
            {
                RunSaveLog.Error(
                    RunSaveLog.DebugTools,
                    $"F5 checkpoint failed: {result.Error}");
                return;
            }

            long saveRevision = _runSaveService.CurrentSave?.saveRevision ?? 0;
            RunSaveLog.Info(
                RunSaveLog.DebugTools,
                $"F5 checkpoint saved. Scene: '{sceneId}', revision: {saveRevision}.");
        }
    }
}
