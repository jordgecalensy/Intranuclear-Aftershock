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
        bool IsRestoring { get; }

        RunSaveOperationResult BeginNewRun();
        RunSaveOperationResult LoadRun();
        RunSaveOperationResult SaveCheckpoint(string sceneId, int floorIndex, int dungeonSeed);
        UniTask<RunSaveOperationResult> RestoreCheckpointAsync();
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

        public RunSaveFile CurrentSave => _currentSave;
        public bool HasLoadedRun => _currentSave != null;
        public bool HasCheckpoint => _currentSave?.checkpoint != null && _currentSave.checkpoint.hasCheckpoint;
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
                if (!_repository.TryDelete(out string deleteError))
                    return RunSaveOperationResult.Failure(deleteError);

                RunSaveFile newSave = RunSaveFile.CreateNew();
                newSave.saveRevision = 1;

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

            if (string.IsNullOrWhiteSpace(sceneId))
                return RunSaveOperationResult.Failure("Checkpoint scene id cannot be empty.");

            _operationInProgress = true;

            try
            {
                RunSaveFile candidate = _currentSave.DeepCopy();
                RunCheckpointData checkpoint = new RunCheckpointData
                {
                    hasCheckpoint = true,
                    checkpointId = Guid.NewGuid().ToString("N"),
                    createdAtUnixMilliseconds = UtcNowMilliseconds(),
                    sceneId = sceneId,
                    floorIndex = floorIndex,
                    dungeonSeed = dungeonSeed
                };

                _participantRegistry.CaptureAll(checkpoint);
                checkpoint.EnsureInitialized();

                candidate.checkpoint = checkpoint;
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

        public async UniTask<RunSaveOperationResult> RestoreCheckpointAsync()
        {
            if (_operationInProgress)
                return BusyFailure();

            if (!HasCheckpoint)
                return RunSaveOperationResult.Failure("The active run does not contain a checkpoint.");

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
