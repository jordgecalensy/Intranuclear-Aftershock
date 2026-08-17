using System;
using Failsafe.Scripts.Configs;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class RunAutosaveController :
        IInitializable,
        ITickable,
        IDisposable
    {
        private enum AutosaveTrigger
        {
            Periodic,
            ApplicationQuit
        }

        private readonly IRunSaveService _runSaveService;
        private readonly IRunCheckpointSafetyPolicy _safetyPolicy;
        private readonly GameConfig _gameConfig;

        private float _nextSaveAttemptAt;
        private RunCheckpointBlockReason _lastReportedBlockReason;
        private bool _isDisposed;

        public RunAutosaveController(
            IRunSaveService runSaveService,
            IRunCheckpointSafetyPolicy safetyPolicy,
            GameConfig gameConfig)
        {
            _runSaveService =
                runSaveService ??
                throw new ArgumentNullException(nameof(runSaveService));
            _safetyPolicy =
                safetyPolicy ??
                throw new ArgumentNullException(nameof(safetyPolicy));
            _gameConfig =
                gameConfig ??
                throw new ArgumentNullException(nameof(gameConfig));
        }

        public void Initialize()
        {
            _nextSaveAttemptAt =
                Time.realtimeSinceStartup + AutosaveIntervalSeconds;

            Application.quitting += HandleApplicationQuitting;
        }

        public void Tick()
        {
            if (!_runSaveService.IsRunActive ||
                _runSaveService.IsRestoring ||
                Time.realtimeSinceStartup < _nextSaveAttemptAt)
            {
                return;
            }

            TryCreateCheckpoint(AutosaveTrigger.Periodic);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Application.quitting -= HandleApplicationQuitting;
            _isDisposed = true;
        }

        private float AutosaveIntervalSeconds =>
            Mathf.Max(10f, _gameConfig.RunAutosaveIntervalSeconds);

        private float RetrySeconds =>
            Mathf.Max(0.25f, _gameConfig.RunAutosaveRetrySeconds);

        private void HandleApplicationQuitting()
        {
            if (!_runSaveService.IsRunActive)
                return;

            TryCreateCheckpoint(AutosaveTrigger.ApplicationQuit);
        }

        private void TryCreateCheckpoint(AutosaveTrigger trigger)
        {
            if (_runSaveService.IsRestoring)
            {
                HandleBlockedAttempt(
                    trigger,
                    RunCheckpointBlockReason.RestoreInProgress,
                    "checkpoint restoration is still in progress");
                return;
            }

            RunCheckpointSafetyDecision decision = _safetyPolicy.Evaluate();
            if (!decision.CanSave)
            {
                HandleBlockedAttempt(trigger, decision.Reason, decision.Message);
                return;
            }

            RunSaveFile currentSave = _runSaveService.CurrentSave;
            if (currentSave == null || !currentSave.IsActive)
                return;

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
                    RunSaveLog.Autosave,
                    $"{DescribeTrigger(trigger)} failed: {result.Error}");

                ScheduleRetry();
                return;
            }

            _lastReportedBlockReason = RunCheckpointBlockReason.None;
            _nextSaveAttemptAt =
                Time.realtimeSinceStartup + AutosaveIntervalSeconds;

            long revision = _runSaveService.CurrentSave?.saveRevision ?? 0;
            RunSaveLog.Info(
                RunSaveLog.Autosave,
                $"{DescribeTrigger(trigger)} completed. " +
                $"Scene: '{sceneId}', revision: {revision}.");
        }

        private void HandleBlockedAttempt(
            AutosaveTrigger trigger,
            RunCheckpointBlockReason reason,
            string message)
        {
            string normalizedMessage = string.IsNullOrWhiteSpace(message)
                ? "the checkpoint safety policy rejected the request"
                : message;

            if (trigger == AutosaveTrigger.ApplicationQuit)
            {
                RunSaveLog.Warning(
                    RunSaveLog.Autosave,
                    $"Exit checkpoint skipped: {normalizedMessage}. " +
                    "The last safe checkpoint remains available.");
                return;
            }

            if (_lastReportedBlockReason != reason)
            {
                RunSaveLog.Info(
                    RunSaveLog.Autosave,
                    $"Periodic checkpoint postponed: {normalizedMessage}.");

                _lastReportedBlockReason = reason;
            }

            ScheduleRetry();
        }

        private void ScheduleRetry()
        {
            _nextSaveAttemptAt =
                Time.realtimeSinceStartup + RetrySeconds;
        }

        private static string DescribeTrigger(AutosaveTrigger trigger)
        {
            return trigger == AutosaveTrigger.ApplicationQuit
                ? "Exit checkpoint"
                : "Periodic checkpoint";
        }
    }
}
