using UnityEngine;
using UnityEngine.AI;

public class ChasingState : BehaviorState
{
    // Ссылки
    private Sensor[] _sensors;
    private Transform _transform;
    private EnemyNavMeshActions _navMeshActions; // <-- Используем ваш класс
    private Enemy_ScriptableObject _enemyConfig;
    private EnemyAnimator _enemyAnimator;
    private EnemyMemory _enemyMemory;
    private EnemyAudioManagerBase _audio;
    
    // Переменные состояния
    private float _distanceToPlayer;
    private bool _playerInSight;
    
    // Параметры для прыжков (если используете AreaCost)
    private readonly int _jumpAreaIndex = 3; 
    private readonly float _jumpActivationDistance = 15f; 

    // Конструктор
    public ChasingState(Sensor[] sensors, Transform currentTransform, EnemyNavMeshActions navActions, 
                        EnemyMemory enemyMemory, Enemy_ScriptableObject enemyConfig, 
                        EnemyAnimator enemyAnimator, EnemyAudioManagerBase audio)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _navMeshActions = navActions; // <-- Сохраняем ссылку
        _enemyConfig = enemyConfig;
        _enemyAnimator = enemyAnimator;
        _enemyMemory = enemyMemory;
        _audio = audio;
    }

    public bool PlayerInAttackRange() => _playerInSight && (_distanceToPlayer <= _enemyConfig.AttackRangeMin);

    public override void Enter()
    {
        base.Enter();
        _playerInSight = true; 
        _distanceToPlayer = float.MaxValue;
        
        if (_audio != null) _audio.PlayStateVoice(2);
    }

    public override void Update()
    {
        _playerInSight = false;
        _distanceToPlayer = float.MaxValue;

        foreach (var sensor in _sensors)
        {
            if (!sensor.IsActivated())
                continue;

            Vector3? signalPosition = sensor.SignalSourcePosition;

            if (!signalPosition.HasValue)
                continue;

            Vector3 targetPosition = signalPosition.Value;

            if (sensor is VisualSensor)
            {
                _playerInSight = true;
            }

            _distanceToPlayer = Vector3.Distance(_transform.position, targetPosition);

            _enemyMemory.SetLastKnownPlayerPosition(
                targetPosition,
                (targetPosition - _transform.position).normalized
            );

            if (_playerInSight)
                break;
        }

        if (_playerInSight)
        {
            _navMeshActions.SetStoppingDistance(_enemyConfig.AttackRangeMin);
        }
        else
        {
            _navMeshActions.SetStoppingDistance(0f);
        }

        Vector3 targetDest = _enemyMemory.LastKnownPlayerPosition;

        _navMeshActions.MoveToPoint(targetDest, _enemyConfig.ChaseSpeed);

        UpdateJumpAreaCost();
    }

    private void UpdateJumpAreaCost()
    {
        // Используем публичное свойство Agent из вашего скрипта
        var agent = _navMeshActions.Agent; 
        
        if (agent == null) return;

        if (_playerInSight && _distanceToPlayer > _jumpActivationDistance)
            agent.SetAreaCost(_jumpAreaIndex, 1f);
        else
            agent.SetAreaCost(_jumpAreaIndex, 100f);
    }
}