using UnityEngine;
using UnityEngine.AI;

public class ChasingState : BehaviorState
{
    private Sensor[] _sensors;
    private Transform _transform;
    private Vector3? _chasingPosition;
    private NavMeshAgent _navMeshAgent;
    private Enemy_ScriptableObject _enemyConfig;
    private EnemyAnimator _enemyAnimator;
    private EnemyMovement _movement; // <-- Новый класс
    private EnemyMemory _enemyMemory;
    
    private readonly int _jumpAreaIndex = 3; 
    private readonly float _jumpActivationDistance = 15f; 
    private float _distanceToPlayer;
    private bool _playerInSight;

    public ChasingState(Sensor[] sensors, Transform currentTransform, EnemyMovement movement, 
                        EnemyMemory enemyMemory, NavMeshAgent navMeshAgent, Enemy_ScriptableObject enemyConfig, 
                        EnemyAnimator enemyAnimator)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _movement = movement;
        _navMeshAgent = navMeshAgent;   
        _enemyConfig = enemyConfig;
        _enemyAnimator = enemyAnimator;
        _enemyMemory = enemyMemory;
    }

    public bool PlayerInAttackRange() => _playerInSight && (_distanceToPlayer < _enemyConfig.AttackRangeMin);

    public override void Enter()
    {
        base.Enter();
        _playerInSight = false;
        _navMeshAgent.stoppingDistance = _enemyConfig.AttackRangeMin;
    }

    public override void Update()
    {
        UpdateJumpAreaCost(); 

        foreach (var sensor in _sensors)
        {
            if (sensor is VisualSensor)
            {
                if (sensor.IsActivated())
                {
                    _distanceToPlayer = ((Vector3)sensor.SignalSourcePosition - _transform.position).magnitude;
                    _playerInSight = true;
                }
                else _playerInSight = false;
            }

            if (sensor.IsActivated())
            {
                // Поворот к цели
                _movement.LookAt((Vector3)sensor.SignalSourcePosition);
                
                _chasingPosition = sensor.SignalSourcePosition;
                _enemyMemory.SetLastKnownPlayerPosition(
                    sensor.SignalSourcePosition.Value,
                    (sensor.SignalSourcePosition.Value - _transform.position).normalized
                );                
                break;
            }
        }
       
        if (_chasingPosition != null)
        {
            _movement.MoveTo(_chasingPosition.Value, _enemyConfig.ChaseSpeed);
        }
    }

    private void UpdateJumpAreaCost()
    {
        if (_playerInSight && _distanceToPlayer > _jumpActivationDistance)
            _navMeshAgent.SetAreaCost(_jumpAreaIndex, 1f);
        else
            _navMeshAgent.SetAreaCost(_jumpAreaIndex, 10000f);
    }
}