using UnityEngine;

using System;
using System.Collections.Generic;
using Failsafe.Scripts.SaveSystem;

public class PowerNetworkManager :
    MonoBehaviour,
    IRunPersistentStateProvider,
    IRunPersistentStateRestoreFinalizer
{
    private const string PersistentStateType = "power-network";
    private const int PersistentStateVersion = 1;

    private PowerNode[] _allNodes;

    public string StateTypeId => PersistentStateType;
    public int StateVersion => PersistentStateVersion;

    private void Awake()
    {
        RefreshNodeCache();
    }

    public string CapturePersistentState()
    {
        Dictionary<string, PowerCross> crossesById = IndexPowerCrosses();
        PowerNetworkPersistentState state = new PowerNetworkPersistentState
        {
            crosses = new List<PowerCrossPersistentState>(crossesById.Count)
        };

        foreach (KeyValuePair<string, PowerCross> pair in crossesById)
        {
            state.crosses.Add(new PowerCrossPersistentState
            {
                persistentId = pair.Key,
                rotationSteps = pair.Value.RotationSteps
            });
        }

        state.crosses.Sort(
            (left, right) =>
                string.CompareOrdinal(left.persistentId, right.persistentId));

        return JsonUtility.ToJson(state);
    }

    public void RestorePersistentState(
        string serializedState,
        int stateVersion)
    {
        if (stateVersion != PersistentStateVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported power network state version '{stateVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(serializedState))
        {
            throw new InvalidOperationException(
                "Power network state is empty.");
        }

        PowerNetworkPersistentState state =
            JsonUtility.FromJson<PowerNetworkPersistentState>(serializedState);

        if (state?.crosses == null)
        {
            throw new InvalidOperationException(
                "Power network state has no cross rotation data.");
        }

        Dictionary<string, PowerCross> runtimeCrosses = IndexPowerCrosses();
        HashSet<string> savedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < state.crosses.Count; i++)
        {
            PowerCrossPersistentState savedCross = state.crosses[i];
            string persistentId = savedCross?.persistentId?.Trim();

            if (string.IsNullOrWhiteSpace(persistentId))
            {
                throw new InvalidOperationException(
                    "Power network state contains a cross with an empty ID.");
            }

            if (!savedIds.Add(persistentId))
            {
                throw new InvalidOperationException(
                    $"Power cross ID '{persistentId}' occurs more than once in the checkpoint.");
            }

            if (!runtimeCrosses.TryGetValue(
                    persistentId,
                    out PowerCross runtimeCross))
            {
                throw new InvalidOperationException(
                    $"Saved power cross '{persistentId}' is missing from the loaded scene.");
            }

            runtimeCross.RestoreRotationSteps(savedCross.rotationSteps);
        }

        if (savedIds.Count != runtimeCrosses.Count)
        {
            throw new InvalidOperationException(
                "The loaded scene contains power crosses that are missing from the checkpoint.");
        }
    }

    public void FinalizePersistentStateRestore()
    {
        RefreshPower();
    }

    // Перезапуск питания: сбросить у всех, потом запустить у источников
    public void RefreshPower()
    {
        RefreshNodeCache();

        // Сброс питания у всех
        foreach (var node in _allNodes)
        {
            node.ResetPower();
        }

        // Запуск питания от всех источников
        int sourceCount = 0;
        foreach (var node in _allNodes)
        {
            if (node is PowerSource source)
            {
                sourceCount++;
                source.StartPower();
            }
        }

        int poweredNodeCount = 0;
        int poweredEndPointCount = 0;

        foreach (PowerNode node in _allNodes)
        {
            if (!node.HasPower)
                continue;

            poweredNodeCount++;

            if (node is PowerEndPoint)
                poweredEndPointCount++;
        }

        Debug.Log(
            $"[POWER-NET] Refresh complete. " +
            $"Nodes: {_allNodes.Length}, " +
            $"sources: {sourceCount}, " +
            $"powered nodes: {poweredNodeCount}, " +
            $"powered endpoints: {poweredEndPointCount}.");

        // После запуска питания можно проверить, кто не получил питание и отключить их явно,
        // но так как ResetPower и ReceivePower управляют состоянием, это не обязательно.
    }

    private void RefreshNodeCache()
    {
        _allNodes = FindObjectsByType<PowerNode>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
    }

    private Dictionary<string, PowerCross> IndexPowerCrosses()
    {
        RefreshNodeCache();

        Dictionary<string, PowerCross> crossesById =
            new Dictionary<string, PowerCross>(StringComparer.Ordinal);

        for (int i = 0; i < _allNodes.Length; i++)
        {
            if (!(_allNodes[i] is PowerCross powerCross))
                continue;

            RunPersistentObject persistentObject =
                powerCross.GetComponent<RunPersistentObject>();

            if (persistentObject == null)
            {
                throw new InvalidOperationException(
                    $"Power cross '{powerCross.name}' has no {nameof(RunPersistentObject)}.");
            }

            string persistentId = persistentObject.PersistentId?.Trim();
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                throw new InvalidOperationException(
                    $"Power cross '{powerCross.name}' has an empty persistent ID.");
            }

            if (!crossesById.TryAdd(persistentId, powerCross))
            {
                throw new InvalidOperationException(
                    $"Power cross ID '{persistentId}' occurs more than once in the loaded scene.");
            }
        }

        return crossesById;
    }

    [Serializable]
    private sealed class PowerNetworkPersistentState
    {
        public List<PowerCrossPersistentState> crosses;
    }

    [Serializable]
    private sealed class PowerCrossPersistentState
    {
        public string persistentId;
        public int rotationSteps;
    }
}
