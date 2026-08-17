using System;
using System.Collections.Generic;
using UnityEngine;

namespace Failsafe.Scripts.SaveSystem
{
    public static class RunLifecycleStates
    {
        public const string Active = "active";
        public const string Ended = "ended";
    }

    public static class RunEndReasons
    {
        public const string PlayerDeath = "player_death";
    }

    [Serializable]
    public sealed class RunSaveFile
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long saveRevision;
        public string runId;
        public string lifecycleState = RunLifecycleStates.Active;
        public long endedAtUnixMilliseconds;
        public string endReason;
        public RunCheckpointData checkpoint = new RunCheckpointData();
        public RunJournalData journal = new RunJournalData();

        public bool IsActive =>
            string.Equals(lifecycleState, RunLifecycleStates.Active, StringComparison.Ordinal);

        public bool IsEnded =>
            string.Equals(lifecycleState, RunLifecycleStates.Ended, StringComparison.Ordinal);

        public static RunSaveFile CreateNew()
        {
            return new RunSaveFile
            {
                runId = Guid.NewGuid().ToString("N")
            };
        }

        public void EnsureInitialized()
        {
            if (schemaVersion <= 0)
                schemaVersion = CurrentSchemaVersion;

            if (string.IsNullOrWhiteSpace(runId))
                runId = Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(lifecycleState))
                lifecycleState = RunLifecycleStates.Active;

            if (checkpoint == null)
                checkpoint = new RunCheckpointData();

            if (journal == null)
                journal = new RunJournalData();

            checkpoint.EnsureInitialized();
            journal.EnsureInitialized();
        }

        public RunSaveFile DeepCopy()
        {
            EnsureInitialized();

            return new RunSaveFile
            {
                schemaVersion = schemaVersion,
                saveRevision = saveRevision,
                runId = runId,
                lifecycleState = lifecycleState,
                endedAtUnixMilliseconds = endedAtUnixMilliseconds,
                endReason = endReason,
                checkpoint = checkpoint.DeepCopy(),
                journal = journal.DeepCopy()
            };
        }
    }

    [Serializable]
    public sealed class RunCheckpointData
    {
        public bool hasCheckpoint;
        public string checkpointId;
        public long createdAtUnixMilliseconds;
        public string sceneId;
        public int floorIndex;
        public int dungeonSeed;
        public EngineerStateData engineer = new EngineerStateData();
        public PlayerStateData player = new PlayerStateData();
        public InventoryStateData inventory = new InventoryStateData();
        public List<QuestStateData> quests = new List<QuestStateData>();
        public FloorStateData floor = new FloorStateData();
        public List<EnemyStateData> enemies = new List<EnemyStateData>();

        public void EnsureInitialized()
        {
            if (engineer == null)
                engineer = new EngineerStateData();

            if (player == null)
                player = new PlayerStateData();

            if (inventory == null)
                inventory = new InventoryStateData();

            if (quests == null)
                quests = new List<QuestStateData>();

            if (floor == null)
                floor = new FloorStateData();

            if (enemies == null)
                enemies = new List<EnemyStateData>();

            engineer.EnsureInitialized();
            inventory.EnsureInitialized();
            floor.EnsureInitialized();

            for (int i = 0; i < quests.Count; i++)
                quests[i]?.EnsureInitialized();
        }

        public RunCheckpointData DeepCopy()
        {
            EnsureInitialized();

            RunCheckpointData copy = new RunCheckpointData
            {
                hasCheckpoint = hasCheckpoint,
                checkpointId = checkpointId,
                createdAtUnixMilliseconds = createdAtUnixMilliseconds,
                sceneId = sceneId,
                floorIndex = floorIndex,
                dungeonSeed = dungeonSeed,
                engineer = engineer.DeepCopy(),
                player = player.DeepCopy(),
                inventory = inventory.DeepCopy(),
                floor = floor.DeepCopy()
            };

            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i] != null)
                    copy.quests.Add(quests[i].DeepCopy());
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null)
                    copy.enemies.Add(enemies[i].DeepCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class EngineerStateData
    {
        public bool hasState;
        public string name;
        public string operatorCode;
        public int totalWeight;
        public int spentWeight;
        public List<string> perkIds = new List<string>();

        public void EnsureInitialized()
        {
            if (perkIds == null)
                perkIds = new List<string>();
        }

        public EngineerStateData DeepCopy()
        {
            EnsureInitialized();

            return new EngineerStateData
            {
                hasState = hasState,
                name = name,
                operatorCode = operatorCode,
                totalWeight = totalWeight,
                spentWeight = spentWeight,
                perkIds = new List<string>(perkIds)
            };
        }
    }

    [Serializable]
    public sealed class PlayerStateData
    {
        public bool hasState;
        public float health;
        public float stamina;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;

        public PlayerStateData DeepCopy()
        {
            return (PlayerStateData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class InventoryStateData
    {
        public List<InventoryItemStateData> items = new List<InventoryItemStateData>();

        public void EnsureInitialized()
        {
            if (items == null)
                items = new List<InventoryItemStateData>();
        }

        public InventoryStateData DeepCopy()
        {
            EnsureInitialized();
            InventoryStateData copy = new InventoryStateData();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    copy.items.Add(items[i].DeepCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class InventoryItemStateData
    {
        public string itemId;
        public int row;
        public int column;
        public float energy;
        public string runtimeStateJson;

        public InventoryItemStateData DeepCopy()
        {
            return (InventoryItemStateData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class QuestStateData
    {
        public string questId;
        public string state;
        public List<QuestObjectiveStateData> objectives = new List<QuestObjectiveStateData>();

        public void EnsureInitialized()
        {
            if (objectives == null)
                objectives = new List<QuestObjectiveStateData>();
        }

        public QuestStateData DeepCopy()
        {
            EnsureInitialized();
            QuestStateData copy = new QuestStateData
            {
                questId = questId,
                state = state
            };

            for (int i = 0; i < objectives.Count; i++)
            {
                if (objectives[i] != null)
                    copy.objectives.Add(objectives[i].DeepCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class QuestObjectiveStateData
    {
        public string objectiveId;
        public string state;
        public int progress;

        public QuestObjectiveStateData DeepCopy()
        {
            return (QuestObjectiveStateData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class FloorStateData
    {
        public List<PersistentObjectStateData> objects = new List<PersistentObjectStateData>();

        public void EnsureInitialized()
        {
            if (objects == null)
                objects = new List<PersistentObjectStateData>();
        }

        public FloorStateData DeepCopy()
        {
            EnsureInitialized();
            FloorStateData copy = new FloorStateData();

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                    copy.objects.Add(objects[i].DeepCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class PersistentObjectStateData
    {
        public string persistentId;
        public bool requiredOnRestore;
        public bool isActive;
        public bool hasTransform;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public bool hasRigidbody;
        public bool isKinematic;
        public bool useGravity;
        public int rigidbodyConstraints;
        public string stateType;
        public int stateVersion;
        public string state;

        public PersistentObjectStateData DeepCopy()
        {
            return (PersistentObjectStateData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class EnemyStateData
    {
        public string instanceId;
        public string archetypeId;
        public bool isAlive;
        public float health;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;

        public EnemyStateData DeepCopy()
        {
            return (EnemyStateData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class RunJournalData
    {
        public List<DeathRecordData> deaths = new List<DeathRecordData>();

        public void EnsureInitialized()
        {
            if (deaths == null)
                deaths = new List<DeathRecordData>();
        }

        public RunJournalData DeepCopy()
        {
            EnsureInitialized();
            RunJournalData copy = new RunJournalData();

            for (int i = 0; i < deaths.Count; i++)
            {
                if (deaths[i] != null)
                    copy.deaths.Add(deaths[i].DeepCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class DeathRecordData
    {
        public string eventId;
        public long occurredAtUnixMilliseconds;
        public int floorIndex;
        public string causeId;
        public string sourceId;
        public string instigatorId;
        public string sourceRelation;
        public string damageType;
        public string applicationKind;

        public DeathRecordData DeepCopy()
        {
            return (DeathRecordData)MemberwiseClone();
        }
    }
}
