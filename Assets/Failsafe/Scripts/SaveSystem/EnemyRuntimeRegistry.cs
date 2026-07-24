using System;
using System.Collections.Generic;
using Failsafe.Scripts.Health;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    public enum EnemyRuntimeSource
    {
        Placed,
        Spawned
    }

    public sealed class EnemyRuntimeEntry : IDisposable
    {
        private IHealth _health;
        private EnemyStateData _deathState;

        public string InstanceId { get; }
        public string ArchetypeId { get; }
        public Enemy Enemy { get; }
        public Enemy_ScriptableObject EnemyConfig { get; }
        public EnemyRuntimeSource Source { get; }
        public bool HasObservedDeath => _deathState != null;

        internal EnemyRuntimeEntry(
            string instanceId,
            string archetypeId,
            Enemy enemy,
            IHealth health,
            EnemyRuntimeSource source)
        {
            InstanceId = instanceId;
            ArchetypeId = archetypeId;
            Enemy = enemy;
            EnemyConfig = enemy.EnemyConfig;
            _health = health;
            Source = source;

            _health.OnDeath += HandleDeath;

            if (_health.IsDead)
                CaptureDeathState();
        }

        internal EnemyRuntimeEntry(
            EnemyStateData deathState,
            EnemyRuntimeSource source)
        {
            InstanceId = deathState.instanceId.Trim();
            ArchetypeId = deathState.archetypeId.Trim();
            Source = source;
            _deathState = deathState.DeepCopy();
        }

        public EnemyStateData CaptureState()
        {
            if (_deathState != null)
                return _deathState.DeepCopy();

            if (Enemy == null)
            {
                throw new InvalidOperationException(
                    RunSaveLog.Format(
                        RunSaveLog.Enemy,
                        $"Enemy '{InstanceId}' was destroyed without a recorded death."));
            }

            if (_health.IsDead)
            {
                CaptureDeathState();
                return _deathState.DeepCopy();
            }

            Transform enemyTransform = Enemy.transform;
            return new EnemyStateData
            {
                instanceId = InstanceId,
                archetypeId = ArchetypeId,
                isAlive = true,
                health = _health.CurrentHealth,
                position = enemyTransform.position,
                rotation = enemyTransform.rotation
            };
        }

        public void ApplyRestoredDeath(EnemyStateData deathState)
        {
            if (deathState == null)
                throw new ArgumentNullException(nameof(deathState));

            if (deathState.isAlive)
                throw new ArgumentException("A living state cannot be applied as a death.", nameof(deathState));

            if (!string.Equals(
                    InstanceId,
                    deathState.instanceId?.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Enemy state ID '{deathState.instanceId}' does not match runtime ID '{InstanceId}'.");
            }

            if (!string.Equals(
                    ArchetypeId,
                    deathState.archetypeId?.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Enemy '{InstanceId}' changed archetype from '{ArchetypeId}' " +
                    $"to '{deathState.archetypeId}'.");
            }

            DetachHealthEvents();
            _deathState = deathState.DeepCopy();
        }

        public void Dispose()
        {
            DetachHealthEvents();
        }

        private void HandleDeath()
        {
            if (_deathState == null)
                CaptureDeathState();
        }

        private void CaptureDeathState()
        {
            if (Enemy == null)
            {
                throw new InvalidOperationException(
                    RunSaveLog.Format(
                        RunSaveLog.Enemy,
                        $"Enemy '{InstanceId}' was destroyed before its death state could be recorded."));
            }

            Transform enemyTransform = Enemy.transform;
            _deathState = new EnemyStateData
            {
                instanceId = InstanceId,
                archetypeId = ArchetypeId,
                isAlive = false,
                health = 0f,
                position = enemyTransform.position,
                rotation = enemyTransform.rotation
            };
        }

        private void DetachHealthEvents()
        {
            if (_health == null)
                return;

            _health.OnDeath -= HandleDeath;
            _health = null;
        }
    }

    public sealed class EnemyRuntimeRegistry : IDisposable
    {
        private const string PlacedPrefix = "placed:";
        private const string SpawnedPrefix = "spawned:";

        private readonly Dictionary<string, EnemyRuntimeEntry> _entries =
            new Dictionary<string, EnemyRuntimeEntry>(StringComparer.Ordinal);

        public int Count => _entries.Count;
        public IEnumerable<EnemyRuntimeEntry> Entries => _entries.Values;

        public bool TryRegisterSpawned(
            Enemy enemy,
            out EnemyRuntimeEntry entry,
            out string error)
        {
            string instanceId = $"spawned:{Guid.NewGuid():N}";
            return TryRegisterSpawned(instanceId, enemy, out entry, out error);
        }

        public bool TryRegisterSpawned(
            string instanceId,
            Enemy enemy,
            out EnemyRuntimeEntry entry,
            out string error)
        {
            return TryRegister(
                instanceId,
                enemy,
                EnemyRuntimeSource.Spawned,
                out entry,
                out error);
        }

        public bool TryRegisterPlaced(
            PlacedEnemySaveIdentity identity,
            out EnemyRuntimeEntry entry,
            out string error)
        {
            if (identity == null)
            {
                entry = null;
                error = "Placed enemy identity is missing.";
                return false;
            }

            return TryRegister(
                identity.InstanceId,
                identity.GetComponent<Enemy>(),
                EnemyRuntimeSource.Placed,
                out entry,
                out error);
        }

        public bool TryGet(string instanceId, out EnemyRuntimeEntry entry)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                entry = null;
                return false;
            }

            return _entries.TryGetValue(instanceId.Trim(), out entry);
        }

        public bool TryRegisterTombstone(
            EnemyStateData deathState,
            out EnemyRuntimeEntry entry,
            out string error)
        {
            entry = null;

            if (deathState == null)
            {
                error = "Enemy death state is missing.";
                return false;
            }

            if (deathState.isAlive)
            {
                error = "A living enemy state cannot be registered as a tombstone.";
                return false;
            }

            string instanceId = deathState.instanceId?.Trim();
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Enemy instance ID is empty.";
                return false;
            }

            string archetypeId = deathState.archetypeId?.Trim();
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                error = $"Enemy '{instanceId}' has an empty archetype ID.";
                return false;
            }

            if (!TryResolveSource(instanceId, out EnemyRuntimeSource source))
            {
                error =
                    $"Enemy instance ID '{instanceId}' has an unsupported prefix. " +
                    $"Expected '{PlacedPrefix}' or '{SpawnedPrefix}'.";
                return false;
            }

            if (_entries.TryGetValue(instanceId, out EnemyRuntimeEntry existing))
            {
                if (existing.HasObservedDeath &&
                    string.Equals(
                        existing.ArchetypeId,
                        archetypeId,
                        StringComparison.Ordinal))
                {
                    entry = existing;
                    error = null;
                    return true;
                }

                error = $"Enemy instance ID '{instanceId}' is already registered.";
                return false;
            }

            EnemyStateData normalizedState = deathState.DeepCopy();
            normalizedState.instanceId = instanceId;
            normalizedState.archetypeId = archetypeId;

            entry = new EnemyRuntimeEntry(normalizedState, source);
            _entries.Add(instanceId, entry);
            error = null;
            return true;
        }

        public bool Remove(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return false;

            string normalizedInstanceId = instanceId.Trim();
            if (!_entries.TryGetValue(
                    normalizedInstanceId,
                    out EnemyRuntimeEntry entry))
            {
                return false;
            }

            entry.Dispose();
            return _entries.Remove(normalizedInstanceId);
        }

        public static bool IsSpawnedInstanceId(string instanceId)
        {
            return !string.IsNullOrWhiteSpace(instanceId) &&
                   instanceId.Trim().StartsWith(
                       SpawnedPrefix,
                       StringComparison.Ordinal);
        }

        public void Dispose()
        {
            foreach (EnemyRuntimeEntry entry in _entries.Values)
                entry.Dispose();

            _entries.Clear();
        }

        private bool TryRegister(
            string instanceId,
            Enemy enemy,
            EnemyRuntimeSource source,
            out EnemyRuntimeEntry entry,
            out string error)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                error = "Enemy instance ID is empty.";
                return false;
            }

            if (enemy == null)
            {
                error = "Enemy component is missing on the registered GameObject.";
                return false;
            }

            Enemy_ScriptableObject enemyConfig = enemy.EnemyConfig;
            if (enemyConfig == null)
            {
                error = $"Enemy '{enemy.name}' has no EnemyConfig.";
                return false;
            }

            IHealth health = enemy.Health;
            if (health == null)
            {
                error = $"Enemy '{enemy.name}' has no injected health state.";
                return false;
            }

            string archetypeId = enemyConfig.PersistenceArchetypeId?.Trim();
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                error =
                    $"Enemy config '{enemyConfig.name}' has no persistence archetype ID.";
                return false;
            }

            string normalizedInstanceId = instanceId.Trim();
            if (!HasExpectedPrefix(normalizedInstanceId, source))
            {
                string expectedPrefix =
                    source == EnemyRuntimeSource.Placed
                        ? PlacedPrefix
                        : SpawnedPrefix;

                error =
                    $"Enemy instance ID '{normalizedInstanceId}' must start with " +
                    $"'{expectedPrefix}'.";
                return false;
            }

            if (_entries.TryGetValue(normalizedInstanceId, out EnemyRuntimeEntry existing))
            {
                bool isSameRegistration =
                    ReferenceEquals(existing.Enemy, enemy) &&
                    existing.Source == source &&
                    string.Equals(existing.ArchetypeId, archetypeId, StringComparison.Ordinal);

                if (isSameRegistration)
                {
                    entry = existing;
                    error = null;
                    return true;
                }

                string existingEnemyName =
                    existing.Enemy != null ? existing.Enemy.name : "a destroyed enemy";

                error =
                    $"Enemy instance ID '{normalizedInstanceId}' is already registered by " +
                    $"'{existingEnemyName}'.";
                return false;
            }

            entry = new EnemyRuntimeEntry(
                normalizedInstanceId,
                archetypeId,
                enemy,
                health,
                source);

            _entries.Add(normalizedInstanceId, entry);
            error = null;
            return true;
        }

        private static bool HasExpectedPrefix(
            string instanceId,
            EnemyRuntimeSource source)
        {
            string expectedPrefix =
                source == EnemyRuntimeSource.Placed
                    ? PlacedPrefix
                    : SpawnedPrefix;

            return instanceId.StartsWith(expectedPrefix, StringComparison.Ordinal);
        }

        private static bool TryResolveSource(
            string instanceId,
            out EnemyRuntimeSource source)
        {
            if (instanceId.StartsWith(PlacedPrefix, StringComparison.Ordinal))
            {
                source = EnemyRuntimeSource.Placed;
                return true;
            }

            if (instanceId.StartsWith(SpawnedPrefix, StringComparison.Ordinal))
            {
                source = EnemyRuntimeSource.Spawned;
                return true;
            }

            source = default;
            return false;
        }
    }
}
