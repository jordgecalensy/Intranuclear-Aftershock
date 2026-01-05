using UnityEngine;

/// <summary>
/// Стандартное поведение (обычно Idle).
/// Гарантирует остановку двигателя.
/// </summary>
public class DefaultState : BehaviorState
{
    private Sensor[] _sensors;
    private Transform _transform;
    private EnemyMovement _movement; // <-- Добавили

    public bool IsPatroling()
    {
        return true;
    }

    // Обновленный конструктор
    public DefaultState(Sensor[] sensors, Transform transform, EnemyMovement movement)
    {
        _sensors = sensors;
        _transform = transform;
        _movement = movement;
    }

    public override void Enter()
    {
        base.Enter();
        // Гарантируем, что физика остановлена
        if (_movement != null) 
            _movement.Stop();
            
        Debug.Log("Enter DefaultState");
    }
}