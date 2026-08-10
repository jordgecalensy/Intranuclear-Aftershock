using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Failsafe.GameSceneServices.SpawnSystem;
using Failsafe.Scripts.Health;
using UnityEngine;
using UnityEngine.AI;
using VContainer.Unity;

namespace Failsafe.Scripts.SaveSystem
{
    public sealed class EnemyRunSaveParticipant :
        IRunSaveParticipant,
        IInitializable,
        IDisposable
    {
        public const string Id = RunSaveParticipantIds.Enemies;

        private const int EnemyRestoreOrder = 400;

        private readonly EnemyRuntimeRegistry _enemyRegistry;
        private readonly IEnemySpawnSystem _spawnSystem;
        private readonly RunSaveParticipantRegistry _participantRegistry;

        private IDisposable _registration;

        public string ParticipantId => Id;
        public int RestoreOrder => EnemyRestoreOrder;

        public EnemyRunSaveParticipant(
            EnemyRuntimeRegistry enemyRegistry,
            IEnemySpawnSystem spawnSystem,
            RunSaveParticipantRegistry participantRegistry)
        {
            _enemyRegistry =
                enemyRegistry ?? throw new ArgumentNullException(nameof(enemyRegistry));
            _spawnSystem =
                spawnSystem ?? throw new ArgumentNullException(nameof(spawnSystem));
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

            List<EnemyStateData> states = new List<EnemyStateData>();
            HashSet<string> instanceIds =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (EnemyRuntimeEntry entry in _enemyRegistry.Entries)
            {
                EnemyStateData state = entry.CaptureState();
                NormalizeAndValidateState(state);

                if (!instanceIds.Add(state.instanceId))
                {
                    throw new InvalidOperationException(
                        $"Enemy instance ID '{state.instanceId}' occurs more than once.");
                }

                states.Add(state);
            }

            states.Sort(
                (left, right) =>
                    string.CompareOrdinal(left.instanceId, right.instanceId));

            checkpoint.enemies = states;

            RunSaveLog.Info(
                RunSaveLog.Enemy,
                $"Captured {states.Count} enemy states.");
        }

        public async UniTask RestoreAsync(
            RunCheckpointData checkpoint,
            RunLoadContext context)
        {
            if (checkpoint == null)
                throw new ArgumentNullException(nameof(checkpoint));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            List<EnemyStateData> savedStates = checkpoint.enemies;
            if (savedStates == null || savedStates.Count == 0)
            {
                RunSaveLog.Info(
                    RunSaveLog.Enemy,
                    "Checkpoint has no enemy states; scene defaults were kept for legacy compatibility.");
                return;
            }

            // Scene loading can finish before Unity invokes IStartable entry points.
            // The restore gate keeps the normal spawner paused during this frame.
            await UniTask.NextFrame();

            Dictionary<string, EnemyStateData> statesById =
                ValidateAndIndexStates(savedStates);

            RemoveRuntimeEnemiesMissingFromSave(statesById);

            for (int i = 0; i < savedStates.Count; i++)
            {
                EnemyStateData state = savedStates[i];

                if (EnemyRuntimeRegistry.IsSpawnedInstanceId(state.instanceId) &&
                    !_spawnSystem.TryRestoreSpawnHistory(
                        state.archetypeId,
                        out string historyError))
                {
                    throw CreateRestoreException(state, historyError);
                }

                RestoreEnemy(state);
            }

            RunSaveLog.Info(
                RunSaveLog.Enemy,
                $"Restored {savedStates.Count} enemy states.");
        }

        private Dictionary<string, EnemyStateData> ValidateAndIndexStates(
            List<EnemyStateData> savedStates)
        {
            Dictionary<string, EnemyStateData> statesById =
                new Dictionary<string, EnemyStateData>(StringComparer.Ordinal);

            for (int i = 0; i < savedStates.Count; i++)
            {
                EnemyStateData state = savedStates[i];
                NormalizeAndValidateState(state);

                if (!statesById.TryAdd(state.instanceId, state))
                {
                    throw new InvalidOperationException(
                        $"Enemy instance ID '{state.instanceId}' occurs more than once in the checkpoint.");
                }
            }

            return statesById;
        }

        private void RemoveRuntimeEnemiesMissingFromSave(
            Dictionary<string, EnemyStateData> statesById)
        {
            List<EnemyRuntimeEntry> runtimeEntries =
                new List<EnemyRuntimeEntry>(_enemyRegistry.Entries);

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                EnemyRuntimeEntry entry = runtimeEntries[i];
                if (statesById.ContainsKey(entry.InstanceId))
                    continue;

                if (entry.Enemy != null)
                    entry.Enemy.gameObject.SetActive(false);

                _enemyRegistry.Remove(entry.InstanceId);
            }
        }

        private void RestoreEnemy(EnemyStateData state)
        {
            if (_enemyRegistry.TryGet(
                    state.instanceId,
                    out EnemyRuntimeEntry entry))
            {
                EnsureArchetypeMatches(entry, state);

                if (state.isAlive)
                {
                    RestoreLivingEnemy(entry, state);
                    return;
                }

                RestoreDeadEnemy(entry, state);
                return;
            }

            if (state.isAlive)
            {
                RestoreMissingLivingEnemy(state);
                return;
            }

            RestoreMissingDeadEnemy(state);
        }

        private void RestoreMissingLivingEnemy(EnemyStateData state)
        {
            if (!EnemyRuntimeRegistry.IsSpawnedInstanceId(state.instanceId))
            {
                throw CreateRestoreException(
                    state,
                    "A manually placed living enemy is missing from the loaded scene.");
            }

            if (!_spawnSystem.TrySpawnRestoredEnemy(
                    state.instanceId,
                    state.archetypeId,
                    state.position,
                    state.rotation,
                    out Enemy enemy,
                    out string spawnError))
            {
                throw CreateRestoreException(state, spawnError);
            }

            if (!_enemyRegistry.TryGet(
                    state.instanceId,
                    out EnemyRuntimeEntry entry) ||
                !ReferenceEquals(entry.Enemy, enemy))
            {
                throw CreateRestoreException(
                    state,
                    "The spawned enemy was not registered with its saved instance ID.");
            }

            RestoreLivingEnemy(entry, state);
        }

        private void RestoreLivingEnemy(
            EnemyRuntimeEntry entry,
            EnemyStateData state)
        {
            if (entry.Enemy == null)
            {
                throw CreateRestoreException(
                    state,
                    "The saved enemy is alive, but its runtime object is missing.");
            }

            if (!(entry.Enemy.Health is IRestorableHealth restorableHealth))
            {
                throw CreateRestoreException(
                    state,
                    "The enemy health implementation does not support state restoration.");
            }

            if (state.health > restorableHealth.MaxHealth)
            {
                throw CreateRestoreException(
                    state,
                    $"Saved health {state.health} exceeds maximum health " +
                    $"{restorableHealth.MaxHealth}.");
            }

            RestoreTransform(
                entry.Enemy,
                state.position,
                NormalizeRotation(state.rotation));

            restorableHealth.RestoreState(state.health);
        }

        private void RestoreDeadEnemy(
            EnemyRuntimeEntry entry,
            EnemyStateData state)
        {
            entry.ApplyRestoredDeath(state);

            if (entry.Enemy != null)
                entry.Enemy.gameObject.SetActive(false);

            SpawnCorpse(state);
        }

        private void RestoreMissingDeadEnemy(EnemyStateData state)
        {
            if (!_enemyRegistry.TryRegisterTombstone(
                    state,
                    out _,
                    out string registrationError))
            {
                throw CreateRestoreException(state, registrationError);
            }

            SpawnCorpse(state);
        }

        private void SpawnCorpse(EnemyStateData state)
        {
            if (!_spawnSystem.TrySpawnRestoredCorpse(
                    state.archetypeId,
                    state.position,
                    NormalizeRotation(state.rotation),
                    out _,
                    out string corpseError))
            {
                throw CreateRestoreException(state, corpseError);
            }
        }

        private static void EnsureArchetypeMatches(
            EnemyRuntimeEntry entry,
            EnemyStateData state)
        {
            if (string.Equals(
                    entry.ArchetypeId,
                    state.archetypeId,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw CreateRestoreException(
                state,
                $"Runtime archetype is '{entry.ArchetypeId}'.");
        }

        private static void RestoreTransform(
            Enemy enemy,
            Vector3 position,
            Quaternion rotation)
        {
            NavMeshAgent navMeshAgent = enemy.GetComponent<NavMeshAgent>();
            if (navMeshAgent != null &&
                navMeshAgent.enabled &&
                navMeshAgent.isOnNavMesh)
            {
                if (!navMeshAgent.Warp(position))
                {
                    throw new InvalidOperationException(
                        $"NavMeshAgent failed to warp enemy '{enemy.name}' to its saved position.");
                }

                enemy.transform.rotation = rotation;
                return;
            }

            enemy.transform.SetPositionAndRotation(position, rotation);
        }

        private static void NormalizeAndValidateState(EnemyStateData state)
        {
            if (state == null)
                throw new InvalidOperationException("Checkpoint contains an empty enemy state.");

            state.instanceId = state.instanceId?.Trim();
            state.archetypeId = state.archetypeId?.Trim();

            if (string.IsNullOrWhiteSpace(state.instanceId))
                throw new InvalidOperationException("Enemy instance ID is empty.");

            if (!state.instanceId.StartsWith("placed:", StringComparison.Ordinal) &&
                !state.instanceId.StartsWith("spawned:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Enemy instance ID '{state.instanceId}' has an unsupported prefix.");
            }

            if (string.IsNullOrWhiteSpace(state.archetypeId))
            {
                throw new InvalidOperationException(
                    $"Enemy '{state.instanceId}' has an empty archetype ID.");
            }

            if (!IsFinite(state.health))
            {
                throw new InvalidOperationException(
                    $"Enemy '{state.instanceId}' has non-finite health.");
            }

            if (state.isAlive && state.health <= 0f)
            {
                throw new InvalidOperationException(
                    $"Living enemy '{state.instanceId}' has non-positive health.");
            }

            if (!state.isAlive && !Mathf.Approximately(state.health, 0f))
            {
                throw new InvalidOperationException(
                    $"Dead enemy '{state.instanceId}' has non-zero health.");
            }

            if (!IsFinite(state.position))
            {
                throw new InvalidOperationException(
                    $"Enemy '{state.instanceId}' has a non-finite position.");
            }

            if (!IsFinite(state.rotation) ||
                RotationMagnitudeSquared(state.rotation) <= Mathf.Epsilon)
            {
                throw new InvalidOperationException(
                    $"Enemy '{state.instanceId}' has an invalid rotation.");
            }
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(RotationMagnitudeSquared(rotation));
            return new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
        }

        private static float RotationMagnitudeSquared(Quaternion rotation)
        {
            return rotation.x * rotation.x +
                   rotation.y * rotation.y +
                   rotation.z * rotation.z +
                   rotation.w * rotation.w;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static InvalidOperationException CreateRestoreException(
            EnemyStateData state,
            string reason)
        {
            return new InvalidOperationException(
                RunSaveLog.Format(
                    RunSaveLog.Enemy,
                    $"Failed to restore enemy '{state?.instanceId ?? "<unknown>"}': " +
                    $"{reason}"));
        }
    }
}
