using System;
using System.Collections.Generic;
using System.Linq;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Failsafe.GameSceneServices.SpawnSystem
{
    /// <summary>
    /// Система спауна врагов
    /// </summary>
    public interface IEnemySpawnSystem
    {
        /// <summary>
        /// Отключение спауна врагов на определенное время
        /// </summary>
        /// <param name="duration"></param>
        public void Deactivate(float duration);

        bool TrySpawnRestoredEnemy(
            string instanceId,
            string archetypeId,
            Vector3 position,
            Quaternion rotation,
            out Enemy enemy,
            out string error);

        bool TrySpawnRestoredCorpse(
            string archetypeId,
            Vector3 position,
            Quaternion rotation,
            out GameObject corpse,
            out string error);

        bool TryRestoreSpawnHistory(string archetypeId, out string error);
    }

    /// <summary>
    /// Система спауна врагов
    /// </summary>
    public class EnemySpawnSystem : IStartable, ITickable, IEnemySpawnSystem
    {
        private SpawnPoint[] _spawnPoints;
        private List<SpawnCandidate> _spawnedEnemies = new List<SpawnCandidate>();
        private List<SpawnAgent> _spawnAgents = new List<SpawnAgent>();

        private List<SpawnCandidate> _spawnCandidates = new List<SpawnCandidate>();
        private readonly Dictionary<string, SpawnCandidate> _candidatesByArchetypeId =
            new Dictionary<string, SpawnCandidate>(StringComparer.Ordinal);
        private readonly Dictionary<string, Enemy_ScriptableObject> _configsByArchetypeId =
            new Dictionary<string, Enemy_ScriptableObject>(StringComparer.Ordinal);

        private bool OnDelay => _lastSpawnCheckAt + _spawnCheckDelay > Time.time;
        private float _spawnCheckDelay = 1;
        private float _lastSpawnCheckAt;

        private bool IsActive => _activateAt < Time.time;
        private float _activateAt;

        private WeightMeter _weightMeter = new WeightMeter();
        public WeightMeter WeightMeter => _weightMeter;

        private EnemySpawnSystemBuilder _enemySpawnSystemBuilder;
        private readonly IObjectResolver _objectResolver;
        private readonly EnemyRuntimeRegistry _enemyRuntimeRegistry;
        private readonly IRunSaveService _runSaveService;
        private bool _isBuilt;

        public List<SpawnCandidate> SpawnedEnemies => _spawnedEnemies;


        public EnemySpawnSystem(
            EnemySpawnSystemBuilder enemySpawnSystemBuilder,
            IObjectResolver objectResolver,
            EnemyRuntimeRegistry enemyRuntimeRegistry,
            IRunSaveService runSaveService)
        {
            _enemySpawnSystemBuilder = enemySpawnSystemBuilder;
            _objectResolver = objectResolver;
            _enemyRuntimeRegistry = enemyRuntimeRegistry;
            _runSaveService = runSaveService;
        }

        public void Deactivate(float duration)
        {
            _activateAt = duration;
        }

        public void AddSpawnAgent(SpawnAgent spawnAgent)
        {
            _spawnAgents.Add(spawnAgent);
        }

        public void AddSpawnCandidate(SpawnCandidate candidate)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            string archetypeId = candidate.ArchetypeId?.Trim();
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                throw new InvalidOperationException(
                    RunSaveLog.Format(
                        RunSaveLog.Enemy,
                        "A spawn candidate has an empty persistence archetype ID."));
            }

            if (candidate.EnemyPrefab == null)
            {
                throw new InvalidOperationException(
                    RunSaveLog.Format(
                        RunSaveLog.Enemy,
                        $"Enemy archetype '{archetypeId}' has no prefab."));
            }

            if (candidate.EnemyConfig == null)
            {
                throw new InvalidOperationException(
                    RunSaveLog.Format(
                        RunSaveLog.Enemy,
                        $"Enemy archetype '{archetypeId}' has no config."));
            }

            if (_candidatesByArchetypeId.TryGetValue(
                    archetypeId,
                    out SpawnCandidate existing))
            {
                if (!ReferenceEquals(existing, candidate))
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            $"Archetype ID '{archetypeId}' is assigned to multiple enemy prefabs."));
                }

                return;
            }

            if (!TryRegisterEnemyConfig(candidate.EnemyConfig, out string configError))
            {
                throw new InvalidOperationException(
                    RunSaveLog.Format(RunSaveLog.Enemy, configError));
            }

            _candidatesByArchetypeId.Add(archetypeId, candidate);
        }

        public void Start()
        {
            EnsureBuilt();
        }

        private bool HasSpawnCandidates()
        {
            bool hasCandidate = false;
            foreach (var agent in _spawnAgents)
            {
                if (agent.IsConditionTringered())
                {
                    _spawnCandidates.Add(agent.GetSpawnCandidate());
                    hasCandidate = true;
                }
            }
            return hasCandidate;
        }

        public void Tick()
        {
            if (_runSaveService.IsRestoring) return;
            EnsureBuilt();
            if (!IsActive) return;
            if (OnDelay) return;
            if (_spawnPoints.Length == 0) return;

            _lastSpawnCheckAt = Time.time;
            if (_spawnCandidates.Count == 0)
            {
                if (!HasSpawnCandidates()) return;
            }
            var (candidate, spawnPoint) = ChooseCandidateAndSpawnPoint();

            SpawnEnemy(candidate, spawnPoint);

            foreach (var agent in _spawnAgents)
            {
                if (agent.IsConditionTringered())
                    agent.Reset();
            }
        }

        public bool TrySpawnRestoredEnemy(
            string instanceId,
            string archetypeId,
            Vector3 position,
            Quaternion rotation,
            out Enemy enemy,
            out string error)
        {
            enemy = null;

            if (!TryGetCandidate(archetypeId, out SpawnCandidate candidate, out error))
                return false;

            GameObject enemyObject =
                _objectResolver.Instantiate(
                    candidate.EnemyPrefab,
                    position,
                    rotation);

            enemy = enemyObject.GetComponent<Enemy>();
            if (!_enemyRuntimeRegistry.TryRegisterSpawned(
                    instanceId,
                    enemy,
                    out _,
                    out error))
            {
                UnityEngine.Object.Destroy(enemyObject);
                enemy = null;
                return false;
            }

            error = null;
            return true;
        }

        public bool TrySpawnRestoredCorpse(
            string archetypeId,
            Vector3 position,
            Quaternion rotation,
            out GameObject corpse,
            out string error)
        {
            corpse = null;

            string normalizedArchetypeId = archetypeId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedArchetypeId))
            {
                error = "Enemy archetype ID is empty.";
                return false;
            }

            if (!_configsByArchetypeId.TryGetValue(
                    normalizedArchetypeId,
                    out Enemy_ScriptableObject enemyConfig))
            {
                if (!TryFindRegisteredEnemyConfig(
                        normalizedArchetypeId,
                        out enemyConfig,
                        out error))
                {
                    return false;
                }
            }

            if (enemyConfig.DummyPrefab == null)
            {
                error =
                    $"Enemy archetype '{normalizedArchetypeId}' has no DummyPrefab.";
                return false;
            }

            corpse =
                _objectResolver.Instantiate(
                    enemyConfig.DummyPrefab,
                    position,
                    rotation);

            error = null;
            return true;
        }

        public bool TryRestoreSpawnHistory(string archetypeId, out string error)
        {
            if (!TryGetCandidate(archetypeId, out SpawnCandidate candidate, out error))
                return false;

            _spawnedEnemies.Add(candidate);
            _weightMeter.AddWeight(candidate.Weight);
            candidate.SpawnAgent?.Spawned();

            error = null;
            return true;
        }

        private void SpawnEnemy(SpawnCandidate candidate, SpawnPoint spawnPoint)
        {
            Debug.Log($"[{nameof(EnemySpawnSystem)}] Try spawn enemy {candidate?.Name} at position {spawnPoint?.Position}");
            if (spawnPoint == null)
                return;
            GameObject enemyObject =
                _objectResolver.Instantiate(
                    candidate.EnemyPrefab,
                    spawnPoint.Position,
                    spawnPoint.Rotation);

            Enemy enemy = enemyObject.GetComponent<Enemy>();
            if (!_enemyRuntimeRegistry.TryRegisterSpawned(enemy, out _, out string error))
            {
                RunSaveLog.Error(
                    RunSaveLog.Enemy,
                    $"{nameof(EnemySpawnSystem)}: Spawned enemy was not registered: {error}",
                    enemyObject);

                UnityEngine.Object.Destroy(enemyObject);
                return;
            }

            _spawnedEnemies.Add(candidate);
            _weightMeter.AddWeight(candidate.Weight);
            candidate.SpawnAgent?.Spawned();
            spawnPoint.EnemySpawned();
            _spawnCandidates.Clear();
        }

        private bool TryGetCandidate(
            string archetypeId,
            out SpawnCandidate candidate,
            out string error)
        {
            candidate = null;
            EnsureBuilt();

            string normalizedArchetypeId = archetypeId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedArchetypeId))
            {
                error = "Enemy archetype ID is empty.";
                return false;
            }

            if (!_candidatesByArchetypeId.TryGetValue(
                    normalizedArchetypeId,
                    out candidate))
            {
                error = $"Unknown spawned enemy archetype ID '{normalizedArchetypeId}'.";
                return false;
            }

            error = null;
            return true;
        }

        private void EnsureBuilt()
        {
            if (_isBuilt)
                return;

            _spawnPoints = LifetimeScope.FindObjectsByType<SpawnPoint>(
                FindObjectsSortMode.None);

            if (_enemySpawnSystemBuilder != null)
                _enemySpawnSystemBuilder.BuildSpawnSystem(this);

            _isBuilt = true;
        }

        private bool TryFindRegisteredEnemyConfig(
            string archetypeId,
            out Enemy_ScriptableObject enemyConfig,
            out string error)
        {
            foreach (EnemyRuntimeEntry entry in _enemyRuntimeRegistry.Entries)
            {
                if (!string.Equals(
                        entry.ArchetypeId,
                        archetypeId,
                        StringComparison.Ordinal) ||
                    entry.EnemyConfig == null)
                {
                    continue;
                }

                enemyConfig = entry.EnemyConfig;
                if (!TryRegisterEnemyConfig(enemyConfig, out error))
                {
                    enemyConfig = null;
                    return false;
                }

                return true;
            }

            enemyConfig = null;
            error = $"Unknown enemy archetype ID '{archetypeId}'.";
            return false;
        }

        private bool TryRegisterEnemyConfig(
            Enemy_ScriptableObject enemyConfig,
            out string error)
        {
            if (enemyConfig == null)
            {
                error = "Enemy config is missing.";
                return false;
            }

            string archetypeId = enemyConfig.PersistenceArchetypeId?.Trim();
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                error =
                    $"Enemy config '{enemyConfig.name}' has no persistence archetype ID.";
                return false;
            }

            if (_configsByArchetypeId.TryGetValue(
                    archetypeId,
                    out Enemy_ScriptableObject existing))
            {
                if (!ReferenceEquals(existing, enemyConfig))
                {
                    error =
                        $"Archetype ID '{archetypeId}' is assigned to multiple enemy configs.";
                    return false;
                }

                error = null;
                return true;
            }

            _configsByArchetypeId.Add(archetypeId, enemyConfig);
            error = null;
            return true;
        }

        private (SpawnCandidate, SpawnPoint) ChooseCandidateAndSpawnPoint()
        {
            var spawnCandidate = GetRandom(_spawnCandidates);
            var spawnPoints = spawnCandidate.SpecificSpawnPoints.AsEnumerable()
                ?? _spawnPoints.Where(x => x.Type == spawnCandidate.SpawnPointType);
            var spawnPoint = GetRandom(spawnPoints.Where(x => x.Enabled));

            return (spawnCandidate, spawnPoint);
        }

        private static T GetRandom<T>(IEnumerable<T> list)
        {
            var countLength = list.Count();
            if (countLength <= 1) return list.FirstOrDefault();
            var i = UnityEngine.Random.Range(0, countLength);
            return list.ElementAt(i);
        }
    }

    /// <summary>
    /// Шкала веса противников на уровне
    /// </summary>
    public class WeightMeter
    {
        public int MaxWeight => 1000;
        public int CurrentWeight { get; private set; }

        public bool CanSpawn(SpawnCandidate candidate) => CurrentWeight + candidate.Weight <= MaxWeight;

        public void AddWeight(int weight)
        {
            CurrentWeight += weight;
        }
    }
}
