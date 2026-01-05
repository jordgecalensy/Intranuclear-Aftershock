using UnityEngine;

public class AlertState : BehaviorState
{
    private readonly EnemyMovement _movement;
    private readonly EnemyAnimator _enemyAnimator;
    private readonly Sensor[] _sensors;
    
    private float _timer;
    private float _duration;
    private Vector3 _targetPosition;
    private bool _hasTarget;

    public AlertState(EnemyMovement movement, EnemyAnimator enemyAnimator, Sensor[] sensors, float duration = 1.5f)
    {
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _sensors = sensors;
        _duration = duration; // Длительность анимации (можно вынести в конфиг)
    }

    public override void Enter()
    {
        base.Enter();
        
        // 1. Резкая остановка
        _movement.Stop();

        // 2. Запуск анимации "А!"
        _enemyAnimator.TryAlert();

        // 3. Ищем, на что мы среагировали
        _hasTarget = false;
        foreach (var sensor in _sensors)
        {
            if (sensor.IsActivated() && sensor.SignalSourcePosition.HasValue)
            {
                _targetPosition = sensor.SignalSourcePosition.Value;
                _hasTarget = true;
                break; // Берем первый активный сигнал (приоритет у Visual, если он первый в массиве)
            }
        }

        _timer = 0f;
    }

    public override void Update()
    {
        _timer += Time.deltaTime;

        // ВАЖНО: Во время крика враг поворачивается к игроку
        if (_hasTarget)
        {
            _movement.LookAt(_targetPosition);
            
            // Если сенсоры продолжают обновляться, можно обновлять цель в реальном времени:
            foreach (var sensor in _sensors)
            {
                if (sensor.IsActivated() && sensor.SignalSourcePosition.HasValue)
                {
                    _targetPosition = sensor.SignalSourcePosition.Value;
                    break; 
                }
            }
        }
    }

    // Условие выхода: анимация закончилась
    public bool IsAnimationFinished()
    {
        return _timer >= _duration;
    }
}