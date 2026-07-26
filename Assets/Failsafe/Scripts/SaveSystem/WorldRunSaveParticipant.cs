using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class WorldRunSaveParticipant :
        IRunSaveParticipant,
        IInitializable,
        IDisposable
    {
        public const string Id = RunSaveParticipantIds.World;

        private const int WorldRestoreOrder = 300;

        private readonly RunSaveParticipantRegistry _participantRegistry;
        private IDisposable _registration;

        public string ParticipantId => Id;
        public int RestoreOrder => WorldRestoreOrder;

        public WorldRunSaveParticipant(
            RunSaveParticipantRegistry participantRegistry)
        {
            _participantRegistry =
                participantRegistry ??
                throw new ArgumentNullException(nameof(participantRegistry));
        }

        public void Initialize()
        {
            _registration = _participantRegistry.Register(this);
        }

        public void Dispose()
        {
            _registration?.Dispose();
            _registration = null;
        }

        public void Capture(RunCheckpointData checkpoint)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (checkpoint.floor == null)
                checkpoint.floor = new FloorStateData();

            RunPersistentObject[] runtimeObjects = FindRuntimeObjects();
            List<PersistentObjectStateData> states =
                new List<PersistentObjectStateData>(runtimeObjects.Length);
            HashSet<string> persistentIds =
                new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < runtimeObjects.Length; i++)
            {
                PersistentObjectStateData state = runtimeObjects[i].CaptureState();

                if (!persistentIds.Add(state.persistentId))
                {
                    throw new InvalidOperationException(
                        $"Persistent object ID '{state.persistentId}' occurs more than once.");
                }

                states.Add(state);
            }

            states.Sort(
                (left, right) =>
                    string.CompareOrdinal(left.persistentId, right.persistentId));

            checkpoint.floor.objects = states;

            RunSaveLog.Info(
                RunSaveLog.World,
                $"Captured {states.Count} persistent world objects.");
        }

        public async UniTask RestoreAsync(
            RunCheckpointData checkpoint,
            RunLoadContext context)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            List<PersistentObjectStateData> savedStates =
                checkpoint.floor?.objects;

            if (savedStates == null || savedStates.Count == 0)
            {
                RunSaveLog.Info(
                    RunSaveLog.World,
                    "Checkpoint has no persistent world objects; scene defaults were kept.");
                return;
            }

            Dictionary<string, RunPersistentObject> runtimeObjects =
                IndexRuntimeObjects();
            HashSet<string> savedIds =
                new HashSet<string>(StringComparer.Ordinal);
            List<RestorePair> restorePairs =
                new List<RestorePair>(savedStates.Count);

            for (int i = 0; i < savedStates.Count; i++)
            {
                PersistentObjectStateData state = savedStates[i];
                string persistentId = NormalizeAndValidateSavedIdentity(state);

                if (!savedIds.Add(persistentId))
                {
                    throw new InvalidOperationException(
                        $"Persistent object ID '{persistentId}' occurs more than once in the checkpoint.");
                }

                if (runtimeObjects.TryGetValue(
                        persistentId,
                        out RunPersistentObject runtimeObject))
                {
                    restorePairs.Add(new RestorePair(runtimeObject, state));
                    continue;
                }

                string missingMessage =
                    $"Saved persistent object '{persistentId}' is missing from the loaded scene.";

                if (state.requiredOnRestore)
                    throw new InvalidOperationException(missingMessage);

                RunSaveLog.Warning(RunSaveLog.World, missingMessage);
            }

            int preparedCount = 0;

            try
            {
                for (int i = 0; i < restorePairs.Count; i++)
                {
                    restorePairs[i].RuntimeObject.PrepareRestore(
                        restorePairs[i].State);
                    preparedCount++;
                }

                for (int i = 0; i < restorePairs.Count; i++)
                {
                    restorePairs[i].RuntimeObject.RestoreCustomState(
                        restorePairs[i].State);
                }

                Physics.SyncTransforms();
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
            }
            finally
            {
                for (int i = 0; i < preparedCount; i++)
                {
                    restorePairs[i].RuntimeObject.CompleteRestore(
                        restorePairs[i].State);
                }

                Physics.SyncTransforms();
            }

            RunSaveLog.Info(
                RunSaveLog.World,
                $"Restored {restorePairs.Count} persistent world objects.");
        }

        private static RunPersistentObject[] FindRuntimeObjects()
        {
            return UnityEngine.Object.FindObjectsByType<RunPersistentObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static Dictionary<string, RunPersistentObject> IndexRuntimeObjects()
        {
            RunPersistentObject[] runtimeObjects = FindRuntimeObjects();
            Dictionary<string, RunPersistentObject> objectsById =
                new Dictionary<string, RunPersistentObject>(StringComparer.Ordinal);

            for (int i = 0; i < runtimeObjects.Length; i++)
            {
                RunPersistentObject runtimeObject = runtimeObjects[i];
                string persistentId = runtimeObject.PersistentId?.Trim();

                if (string.IsNullOrWhiteSpace(persistentId))
                {
                    throw new InvalidOperationException(
                        $"Persistent object '{runtimeObject.name}' has an empty ID.");
                }

                if (!objectsById.TryAdd(persistentId, runtimeObject))
                {
                    throw new InvalidOperationException(
                        $"Persistent object ID '{persistentId}' occurs more than once in the loaded scene.");
                }
            }

            return objectsById;
        }

        private static string NormalizeAndValidateSavedIdentity(
            PersistentObjectStateData state)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.persistentId))
                throw new InvalidOperationException("Checkpoint contains an unidentified persistent object.");

            state.persistentId = state.persistentId.Trim();
            return state.persistentId;
        }

        private sealed class RestorePair
        {
            public RunPersistentObject RuntimeObject { get; }
            public PersistentObjectStateData State { get; }

            public RestorePair(
                RunPersistentObject runtimeObject,
                PersistentObjectStateData state)
            {
                RuntimeObject = runtimeObject;
                State = state;
            }
        }
    }
}
