using UnityEngine;

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
    private EnemyLinkTraverser _linkTraverser;
    
    // Переменные состояния
    private float _distanceToPlayer;
    private bool _playerInSight;
    private Transform _navigationTargetRoot;

    // Конструктор
    public ChasingState(Sensor[] sensors, Transform currentTransform, EnemyNavMeshActions navActions, 
                        EnemyMemory enemyMemory, Enemy_ScriptableObject enemyConfig, 
                        EnemyAnimator enemyAnimator, EnemyAudioManagerBase audio,
                        EnemyLinkTraverser linkTraverser = null)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _navMeshActions = navActions; // <-- Сохраняем ссылку
        _enemyConfig = enemyConfig;
        _enemyAnimator = enemyAnimator;
        _enemyMemory = enemyMemory;
        _audio = audio;
        _linkTraverser = linkTraverser;
    }

    public bool PlayerInAttackRange() =>
        _playerInSight &&
        (_linkTraverser == null || !_linkTraverser.HasCommittedTraversal) &&
        _distanceToPlayer <= _enemyConfig.AttackRangeMin;

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
        _navigationTargetRoot = null;

        foreach (var sensor in _sensors)
        {
            if (!sensor.IsActivated())
                continue;

            Vector3? signalPosition = sensor.SignalSourcePosition;

            if (!signalPosition.HasValue)
                continue;

            Vector3 sensedPosition = signalPosition.Value;
            Vector3 navigationPosition = sensedPosition;

            if (sensor is VisualSensor visual)
            {
                _playerInSight = true;
                _navigationTargetRoot = visual.Target;

                // Для навигации нужна опора игрока на NavMesh, а не грудь.
                // Точка груди по-прежнему используется сенсором и оружием.
                if (_navigationTargetRoot != null)
                    navigationPosition = _navigationTargetRoot.position;
            }

            _distanceToPlayer = Vector3.Distance(_transform.position, sensedPosition);

            _enemyMemory.SetLastKnownPlayerPosition(
                navigationPosition,
                (navigationPosition - _transform.position).normalized
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

        _navMeshActions.MoveToPoint(
            targetDest,
            _enemyConfig.ChaseSpeed,
            _playerInSight ? _navigationTargetRoot : null);
    }
}
