using System;
using System.Collections.Generic;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;

namespace Failsafe.GameSceneServices.SpawnSystem
{
    public class EnemySpawnSystemBuilder : MonoBehaviour
    {
        [SerializeField] private EnemySpawnData[] _enemySpawnDatas;

        private readonly Dictionary<string, SpawnCandidate> _candidatesByName =
            new Dictionary<string, SpawnCandidate>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _enemyPrefabs =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        public void BuildSpawnSystem(EnemySpawnSystem spawnSystem)
        {
            Debug.Log("BuildSpawnSystem");

            if (spawnSystem == null)
                throw new ArgumentNullException(nameof(spawnSystem));

            _candidatesByName.Clear();
            _enemyPrefabs.Clear();

            if (_enemySpawnDatas == null)
                return;

            foreach (var enemySpawnData in _enemySpawnDatas)
            {
                EnemySpawnPrefabSO spawnPrefab = enemySpawnData.EnemySpawnPrefab;
                if (spawnPrefab == null)
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            "EnemySpawnSystemBuilder contains an empty EnemySpawnPrefab reference."));
                }

                if (string.IsNullOrWhiteSpace(spawnPrefab.Name))
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            "EnemySpawnPrefabSO has an empty Name."));
                }

                if (spawnPrefab.EnemyPrefab == null)
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            $"EnemySpawnPrefabSO '{spawnPrefab.Name}' has no enemy prefab."));
                }

                if (_enemyPrefabs.TryGetValue(spawnPrefab.Name, out GameObject existingPrefab))
                {
                    if (existingPrefab != spawnPrefab.EnemyPrefab)
                    {
                        throw new InvalidOperationException(
                            RunSaveLog.Format(
                                RunSaveLog.Enemy,
                                $"Spawn name '{spawnPrefab.Name}' is assigned to multiple prefabs."));
                    }

                    continue;
                }

                _enemyPrefabs.Add(spawnPrefab.Name, spawnPrefab.EnemyPrefab);
            }

            foreach (var enemySpawnData in _enemySpawnDatas)
            {
                EnemySpawnPrefabSO spawnPrefab = enemySpawnData.EnemySpawnPrefab;
                if (_candidatesByName.ContainsKey(spawnPrefab.Name))
                    continue;

                GameObject enemyPrefab = _enemyPrefabs[spawnPrefab.Name];
                Enemy enemy = enemyPrefab.GetComponent<Enemy>();
                if (enemy == null)
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            $"Enemy prefab '{enemyPrefab.name}' has no Enemy component."));
                }

                Enemy_ScriptableObject enemyConfig = enemy.EnemyConfig;
                if (enemyConfig == null)
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            $"Enemy prefab '{enemyPrefab.name}' has no EnemyConfig."));
                }

                string archetypeId = enemyConfig.PersistenceArchetypeId?.Trim();
                if (string.IsNullOrWhiteSpace(archetypeId))
                {
                    throw new InvalidOperationException(
                        RunSaveLog.Format(
                            RunSaveLog.Enemy,
                            $"Enemy config '{enemyConfig.name}' has no persistence archetype ID."));
                }

                var candidate = new SpawnCandidate(
                    spawnPrefab.Name,
                    archetypeId,
                    enemyPrefab,
                    enemyConfig,
                    enemySpawnData.Weight,
                    enemySpawnData.SpawnPointType,
                    enemySpawnData.SpecificSpawnPoints);

                _candidatesByName.Add(spawnPrefab.Name, candidate);
                spawnSystem.AddSpawnCandidate(candidate);
            }

            foreach (var enemySpawnData in _enemySpawnDatas)
            {
                var candidate = _candidatesByName[enemySpawnData.EnemySpawnPrefab.Name];
                var conditions = ConstructConditions(enemySpawnData, candidate, spawnSystem);
                var agent = new SpawnAgent(new And(conditions), candidate, enemySpawnData.MaxCount);
                spawnSystem.AddSpawnAgent(agent);
            }
        }

        private ISpawnCondition[] ConstructConditions(EnemySpawnData entity, SpawnCandidate candidate, EnemySpawnSystem spawnSystem)
        {
            var innerConditions = new List<ISpawnCondition>();

            if (entity.Random >= 0)
            {
                innerConditions.Add(new Random(entity.Random / 100));
            }
            if (entity.Timer >= 0 && entity.Timer <= 100)
            {
                innerConditions.Add(new Timer(entity.Timer / 100));
            }
            if (!string.IsNullOrEmpty(entity.OtherEnemyName) && _candidatesByName.TryGetValue(entity.OtherEnemyName, out var otherCandidate))
            {
                innerConditions.Add(new OtherEnemySpawned(spawnSystem.SpawnedEnemies, otherCandidate));
            }
            if (entity.UseWeightSystem)
            {
                innerConditions.Add(new Trigger(() => spawnSystem.WeightMeter.CanSpawn(candidate)));

            }
            return innerConditions.ToArray();
        }
    }
}
