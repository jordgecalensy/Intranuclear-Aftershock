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
    private EnemyAudioManager _audio;
    
    // Переменные состояния
    private float _distanceToPlayer;
    private bool _playerInSight;
    
    // Параметры для прыжков (если используете AreaCost)
    private readonly int _jumpAreaIndex = 3; 
    private readonly float _jumpActivationDistance = 15f; 

    // Конструктор
    public ChasingState(Sensor[] sensors, Transform currentTransform, EnemyNavMeshActions navActions, 
                        EnemyMemory enemyMemory, Enemy_ScriptableObject enemyConfig, 
                        EnemyAnimator enemyAnimator, EnemyAudioManager audio)
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
        // 1. Сброс данных перед опросом сенсоров
        _playerInSight = false;
        _distanceToPlayer = float.MaxValue;
        Vector3? liveTargetPos = null;

        // 2. Опрос сенсоров
        foreach (var sensor in _sensors)
        {
            if (sensor.IsActivated())
            {
                // Если видим глазами
                if (sensor is VisualSensor)
                {
                    _playerInSight = true;
                }

                liveTargetPos = sensor.SignalSourcePosition;
                _distanceToPlayer = Vector3.Distance(_transform.position, liveTargetPos.Value);

                // Запоминаем в память
                if (liveTargetPos.HasValue)
                {
                    _enemyMemory.SetLastKnownPlayerPosition(
                        liveTargetPos.Value,
                        (liveTargetPos.Value - _transform.position).normalized
                    );
                }

                if (_playerInSight) break; // Приоритет зрения
            }
        }

        // 3. Логика Динамической Остановки (Ваш запрос)
        if (_playerInSight)
        {
            // Если видим: держим дистанцию, чтобы не набегать на игрока
            // Используем ваш метод из EnemyNavMeshActions
            _navMeshActions.SetStoppingDistance(_enemyConfig.AttackRangeMin);
            
            // Если нужно повернуться к игроку (NavMeshAgent делает это при движении, 
            // но если мы уже стоим - можно добавить ручной поворот)
        }
        else
        {
            // Если НЕ видим (игрок за углом): дистанция 0, бежим до точки потери контакта
            _navMeshActions.SetStoppingDistance(0f);
        }

        // 4. Движение через ваш скрипт
        Vector3 targetDest = _enemyMemory.LastKnownPlayerPosition;
        
        // Используем MoveToPoint из вашего EnemyNavMeshActions
        _navMeshActions.MoveToPoint(targetDest, _enemyConfig.ChaseSpeed);

        // 5. Опционально: управление прыжками (Area Cost)
        // Для этого придется обратиться к Agent напрямую через ваше свойство
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