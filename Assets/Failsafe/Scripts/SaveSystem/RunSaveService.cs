using System;
using Cysharp.Threading.Tasks;

namespace Failsafe.Scripts.SaveSystem
{
    public readonly struct RunSaveOperationResult
    {
        public bool Succeeded { get; }
        public string Error { get; }
        public bool LoadedFromBackup { get; }

        private RunSaveOperationResult(bool succeeded, string error, bool loadedFromBackup)
        {
            Succeeded = succeeded;
            Error = error;
            LoadedFromBackup = loadedFromBackup;
        }

        public static RunSaveOperationResult Success(bool loadedFromBackup = false)
        {
            return new RunSaveOperationResult(true, null, loadedFromBackup);
        }

        public static RunSaveOperationResult Failure(string error)
        {
            return new RunSaveOperationResult(false, error, false);
        }
    }

    public interface IRunSaveService
    {
        RunSaveFile CurrentSave { get; }
        bool HasLoadedRun { get; }
        bool HasCheckpoint { get; }
        bool IsRunActive { get; }
        bool IsRestoring { get; }

        RunSaveOperationResult BeginNewRun();
        RunSaveOperationResult BeginNewRunWithCheckpoint(
            string sceneId,
            int floorIndex,
            int dungeonSeed);
        RunSaveOperationResult LoadRun();
        RunSaveOperationResult SaveCheckpoint(string sceneId, int floorIndex, int dungeonSeed);
        RunSaveOperationResult PrepareCheckpointRestore();
        void CancelCheckpointRestore();
        UniTask<RunSaveOperationResult> RestoreCheckpointAsync();
        RunSaveOperationResult EndRun(string endReason);
        RunSaveOperationResult RecordDeath(DeathRecordData deathRecord);
        int GetDeathCount(string causeId);
        RunSaveOperationResult DeleteRun();
    }

    public sealed class RunSaveService : IRunSaveService
    {
        private readonly IRunSaveRepository _repository;
        private readonly RunSaveParticipantRegistry _participantRegistry;

        private RunSaveFile _currentSave;
        private bool _operationInProgress;

        public RunSaveFile CurrentSave => _currentSave?.DeepCopy();
        public bool HasLoadedRun => _currentSave != null;
        public bool HasCheckpoint => _currentSave?.checkpoint != null && _currentSave.checkpoint.hasCheckpoint;
        public bool IsRunActive => _currentSave != null && _currentSave.IsActive;
        public bool IsRestoring { get; private set; }

        public RunSaveService(
            IRunSaveRepository repository,
            RunSaveParticipantRegistry participantRegistry)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _participantRegistry = participantRegistry ?? throw new ArgumentNullException(nameof(participantRegistry));
        }

        public RunSaveOperationResult BeginNewRun()
        {
            if (_operationInProgress)
                return BusyFailure();

            _operationInProgress = true;

            try
            {
                RunSaveFile newSave = RunSaveFile.CreateNew();
                newSave.saveRevision = 1;

                // TrySave commits through a temporary file and keeps the previous
                // primary save as a backup. Never delete the valid run first.
                if (!_repository.TrySave(newSave, out string saveError))
                    return RunSaveOperationResult.Failure(saveError);

                _currentSave = newSave;
                return RunSaveOperationResult.Success();
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public RunSaveOperationResult BeginNewRunWithCheckpoint(
            string sceneId,
            int floorIndex,
            int dungeonSeed)
        {
            if (_operationInProgress)
                return BusyFailure();

            string validationError = ValidateCheckpointCapture(sceneId);
            if (validationError != null)
                return RunSaveOperationResult.Failure(validationError);

            _operationInProgress = true;

            try
            {
                RunSaveFile newSave = RunSaveFile.CreateNew();
                newSave.checkpoint = CaptureCheckpoint(sceneId, floorIndex, dungeonSeed);
                newSave.saveRevision = 1;

                if (!_repository.TrySave(newSave, out string saveError))
                    return RunSaveOperationResult.Failure(saveError);

                _currentSave = newSave;
                return RunSaveOperationResult.Success();
            }
            catch (Exception exception)
            {
                return RunSaveOperationResult.Failure(
                    $"A save participant failed while capturing the initial checkpoint: {exception.Message}");
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public RunSaveOperationResult LoadRun()
        {
            if (_operationInProgress)
                return BusyFailure();

            _operationInProgress = true;

            try
            {
                if (!_repository.TryLoad(
                        out RunSaveFile loadedSave,
                        out bool loadedFromBackup,
                        out string loadError))
                {
                    return RunSaveOperationResult.Failure(loadError);
                }

                _currentSave = loadedSave;
                return RunSaveOperationResult.Success(loadedFromBackup);
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public RunSaveOperationResult SaveCheckpoint(string sceneId, int floorIndex, int dungeonSeed)
        {
            if (_operationInProgress)
                return BusyFailure();

            if (_currentSave == null)
            {
                return RunSaveOperationResult.Failure(
                    "No run is active. Begin a new run or load an existing run before saving a checkpoint.");
            }

            if (!_currentSave.IsActive)
                return RunSaveOperationResult.Failure("Cannot save a checkpoint for an ended run.");

            string validationError = ValidateCheckpointCapture(sceneId);
            if (validationError != null)
                return RunSaveOperationResult.Failure(validationError);

            _operationInProgress = true;

            try
            {
                RunSaveFile candidate = _currentSave.DeepCopy();
                candidate.checkpoint = CaptureCheckpoint(sceneId, floorIndex, dungeonSeed);
                candidate.saveRevision++;

                if (!_repository.TrySave(candidate, out string saveError))
                    return RunSaveOperationResult.Failure(saveError);

                _currentSave = candidate;
                return RunSaveOperationResult.Success();
            }
            catch (Exception exception)
            {
                return RunSaveOperationResult.Failure(
                    $"A save participant failed while capturing the checkpoint: {exception.Message}");
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public RunSaveOperationResult PrepareCheckpointRestore()
        {
            if (_operationInProgress || IsRestoring)
                return BusyFailure();

            if (!HasCheckpoint)
            {
                IsRestoring = false;
                return RunSaveOperationResult.Failure("The active run does not contain a checkpoint.");
            }

            if (!_currentSave.IsActive)
            {
                IsRestoring = false;
                return RunSaveOperationResult.Failure("An ended run cannot be continued.");
            }

            IsRestoring = true;
            return RunSaveOperationResult.Success();
        }

        public void CancelCheckpointRestore()
        {
            if (!_operationInProgress)
                IsRestoring = false;
        }

        public async UniTask<RunSaveOperationResult> RestoreCheckpointAsync()
        {
            if (_operationInProgress)
                return BusyFailure();

            if (!HasCheckpoint)
            {
                IsRestoring = false;
                return RunSaveOperationResult.Failure("The active run does not contain a checkpoint.");
            }

            if (!_currentSave.IsActive)
            {
                IsRestoring = false;
                return RunSaveOperationResult.Failure("An ended run cannot be continued.");
            }

            string participantValidationError = ValidateRequiredParticipants(_currentSave.checkpoint);
            if (participantValidationError != null)
            {
                IsRestoring = false;
                return RunSaveOperationResult.Failure(participantValidationError);
            }

            _operationInProgress = true;
            IsRestoring = true;

            try
            {
                RunSaveFile snapshot = _currentSave.DeepCopy();
                RunLoadContext context = new RunLoadContext(snapshot);
                await _participantRegistry.RestoreAllAsync(snapshot.checkpoint, context);
                return RunSaveOperationResult.Success();
            }
            catch (Exception exception)
            {
                return RunSaveOperationResult.Failure(
                    $"A save participant failed while restoring the checkpoint: {exception.Message}");
            }
            finally
            {
                IsRestoring = false;
                _operationInProgress = false;
            }
        }

        public RunSaveOperationResult EndRun(string endReason)
        {
            if (_operationInProgress)
                return BusyFailure();

            if (_currentSave == null)
                return RunSaveOperationResult.Failure("Cannot end a run when no run is active.");

            if (_currentSave.IsEnded)
                return RunSaveOperationResult.Success();

            if (!_currentSave.IsActive)
            {
                return RunSaveOperationResult.Failure(
                    $"Cannot end a run with lifecycle state '{_currentSave.lifecycleState}'.");
            }

            if (string.IsNullOrWhiteSpace(endReason))
                return RunSaveOperationResult.Failure("Run end reason cannot be empty.");

            _operationInProgress = true;

            try
            {
                RunSaveFile candidate = _currentSave.DeepCopy();
                candidate.lifecycleState = RunLifecycleStates.Ended;
                candidate.endedAtUnixMilliseconds = UtcNowMilliseconds();
                candidate.endReason = endReason.Trim();
                candidate.saveRevision++;

                bool markerSaved = _repository.TryMarkRunEnded(
                    candidate.runId,
                    candidate.endedAtUnixMilliseconds,
                    candidate.endReason,
                    out string markerError);

                bool saveSucceeded = _repository.TrySave(candidate, out string saveError);

                // Death is terminal for the current session even if the storage device
                // reports an error. The marker and the full snapshot are attempted
                // independently so either one can still prevent an ended run from loading.
                _currentSave = candidate;

                if (!markerSaved && !saveSucceeded)
                {
                    return RunSaveOperationResult.Failure(
                        $"Failed to end the run. Marker: {markerError} Save: {saveError}");
                }

                if (!markerSaved)
                {
                    return RunSaveOperationResult.Failure(
                        $"The ended run snapshot was saved, but its recovery marker failed: {markerError}");
                }

                if (!saveSucceeded)
                {
                    return RunSaveOperationResult.Failure(
                        $"The run was locked as ended, but its full snapshot failed to save: {saveError}");
                }

                return RunSaveOperationResult.Success();
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public RunSaveOperationResult RecordDeath(DeathRecordData deathRecord)
        {
            if (_operationInProgress)
                return BusyFailure();

            if (_currentSave == null)
                return RunSaveOperationResult.Failure("Cannot record a death without an active run.");

            if (deathRecord == null)
                return RunSaveOperationResult.Failure("Death record cannot be null.");

            if (string.IsNullOrWhiteSpace(deathRecord.causeId))
                return RunSaveOperationResult.Failure("Death cause id cannot be empty.");

            _operationInProgress = true;

            try
            {
                RunSaveFile candidate = _currentSave.DeepCopy();
                DeathRecordData recordCopy = deathRecord.DeepCopy();

                if (string.IsNullOrWhiteSpace(recordCopy.eventId))
                    recordCopy.eventId = Guid.NewGuid().ToString("N");

                if (recordCopy.occurredAtUnixMilliseconds <= 0)
                    recordCopy.occurredAtUnixMilliseconds = UtcNowMilliseconds();

                if (ContainsDeathEvent(candidate.journal, recordCopy.eventId))
                    return RunSaveOperationResult.Success();

                candidate.journal.deaths.Add(recordCopy);
                candidate.saveRevision++;

                if (!_repository.TrySave(candidate, out string saveError))
                    return RunSaveOperationResult.Failure(saveError);

                _currentSave = candidate;
                return RunSaveOperationResult.Success();
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        public int GetDeathCount(string causeId)
        {
            if (_currentSave?.journal?.deaths == null || string.IsNullOrWhiteSpace(causeId))
                return 0;

            int count = 0;
            for (int i = 0; i < _currentSave.journal.deaths.Count; i++)
            {
                DeathRecordData record = _currentSave.journal.deaths[i];
                if (record != null && string.Equals(record.causeId, causeId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public RunSaveOperationResult DeleteRun()
        {
            if (_operationInProgress)
                return BusyFailure();

            _operationInProgress = true;

            try
            {
                if (!_repository.TryDelete(out string error))
                    return RunSaveOperationResult.Failure(error);

                _currentSave = null;
                return RunSaveOperationResult.Success();
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        private static bool ContainsDeathEvent(RunJournalData journal, string eventId)
        {
            for (int i = 0; i < journal.deaths.Count; i++)
            {
                DeathRecordData existing = journal.deaths[i];
                if (existing != null && string.Equals(existing.eventId, eventId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private string ValidateRequiredParticipants(RunCheckpointData checkpoint)
        {
            if (checkpoint.engineer != null &&
                checkpoint.engineer.hasState &&
                !_participantRegistry.IsRegistered(RunSaveParticipantIds.Engineer))
            {
                return
                    $"Cannot restore checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Engineer}' is not registered.";
            }

            if (checkpoint.floor?.objects != null &&
                checkpoint.floor.objects.Count > 0 &&
                !_participantRegistry.IsRegistered(RunSaveParticipantIds.World))
            {
                return
                    $"Cannot restore checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.World}' is not registered.";
            }

            if (checkpoint.player != null &&
                checkpoint.player.hasState &&
                !_participantRegistry.IsRegistered(RunSaveParticipantIds.Player))
            {
                return
                    $"Cannot restore checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Player}' is not registered.";
            }

            if (checkpoint.inventory != null &&
                checkpoint.inventory.hasState &&
                !_participantRegistry.IsRegistered(
                    RunSaveParticipantIds.Inventory))
            {
                return
                    $"Cannot restore checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Inventory}' is not registered.";
            }

            if (checkpoint.enemies != null &&
                checkpoint.enemies.Count > 0 &&
                !_participantRegistry.IsRegistered(RunSaveParticipantIds.Enemies))
            {
                return
                    $"Cannot restore checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Enemies}' is not registered.";
            }

            return null;
        }

        private string ValidateCheckpointCapture(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId))
                return "Checkpoint scene id cannot be empty.";

            if (!_participantRegistry.IsRegistered(RunSaveParticipantIds.Engineer))
            {
                return
                    $"Cannot create checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Engineer}' is not registered.";
            }

            if (!_participantRegistry.IsRegistered(RunSaveParticipantIds.World))
            {
                return
                    $"Cannot create checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.World}' is not registered.";
            }

            if (!_participantRegistry.IsRegistered(RunSaveParticipantIds.Player))
            {
                return
                    $"Cannot create checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Player}' is not registered.";
            }

            if (!_participantRegistry.IsRegistered(
                    RunSaveParticipantIds.Inventory))
            {
                return
                    $"Cannot create checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Inventory}' is not registered.";
            }

            if (!_participantRegistry.IsRegistered(RunSaveParticipantIds.Enemies))
            {
                return
                    $"Cannot create checkpoint because the required save participant " +
                    $"'{RunSaveParticipantIds.Enemies}' is not registered.";
            }

            return null;
        }

        private RunCheckpointData CaptureCheckpoint(
            string sceneId,
            int floorIndex,
            int dungeonSeed)
        {
            RunCheckpointData checkpoint = new RunCheckpointData
            {
                hasCheckpoint = true,
                checkpointId = Guid.NewGuid().ToString("N"),
                createdAtUnixMilliseconds = UtcNowMilliseconds(),
                sceneId = sceneId.Trim(),
                floorIndex = floorIndex,
                dungeonSeed = dungeonSeed
            };

            _participantRegistry.CaptureAll(checkpoint);
            checkpoint.EnsureInitialized();
            return checkpoint;
        }

        private static long UtcNowMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static RunSaveOperationResult BusyFailure()
        {
            return RunSaveOperationResult.Failure("Another save or restore operation is already in progress.");
        }
    }
}
