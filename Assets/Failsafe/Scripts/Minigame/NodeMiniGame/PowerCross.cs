using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// Перекрёсток (2-4 направления, можно поворачивать)

public class PowerCross : PowerNode
{
    // Ориентация - поворот на 90 градусы
    private int _rotationSteps = 0; // 0..3
    private Quaternion _initialLocalRotation;

    [SerializeField] private Direction[] _baseConnections = new Direction[] { 
        Direction.Forward, 
        Direction.Left}; // пример базовой конфигурации

    public int RotationSteps => _rotationSteps;

    protected override void Awake()
    {
        _initialLocalRotation = transform.localRotation;
        Neighbors = new Dictionary<Direction, PowerNode>();
        ConnectedDirections = new HashSet<Direction>();
        foreach (var pair in NeighborsSerialized)
        {
            if (!Neighbors.ContainsKey(pair.Direction))
            {
                Neighbors.Add(pair.Direction, pair.Node);
            }
        }
        UpdateConnectedDirections();
    }
    public void Rotate()
    {
        _rotationSteps = (_rotationSteps + 1) % 4;
        ApplyRotation();
        UpdateConnectedDirections();
        // Найдём менеджер и попросим обновить питание
        var manager =  FindFirstObjectByType<PowerNetworkManager>();
        if (manager != null)
        {
            manager.RefreshPower();
        }
        else
        {
            Debug.LogWarning(
                "[POWER-NET] PowerNetworkManager not found in scene.");
        }
    }
    public void RestoreRotationSteps(int rotationSteps)
    {
        if (rotationSteps < 0 || rotationSteps > 3)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(rotationSteps),
                rotationSteps,
                "Power cross rotation steps must be between 0 and 3.");
        }

        _rotationSteps = rotationSteps;
        ApplyRotation();
        UpdateConnectedDirections();
    }

    private void ApplyRotation()
    {
        transform.localRotation =
            _initialLocalRotation *
            Quaternion.AngleAxis(90f * _rotationSteps, Vector3.up);
    }

    private void UpdateConnectedDirections()
    {
        ConnectedDirections.Clear();
        foreach (var baseConnection in _baseConnections)
        {
            ConnectedDirections.Add(RotateDirection(baseConnection, _rotationSteps));
        }
    }

    private Direction RotateDirection(Direction connection, int steps)
    {
        int intConnection = (int)connection;
        intConnection = (intConnection + steps) % 4;
        return (Direction)intConnection;
    }

}
