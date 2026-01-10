using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.AI;

public class SearchingState : BehaviorState
{
    private Vector3 _targetPoint;
    private float _searchTimer;
    private Vector3 _searchOrigin;
    private Vector3 _searchDir;
    private bool _hasReachedOrigin = false;

    private float _waitTimer;
    private bool _isWaiting = false;

    private Sensor[] _sensors;
    private Transform _transform;
    private NavMeshAgent _navMeshAgent;
    private Enemy_ScriptableObject _enemyConfig;
    private EnemyMovePatterns _enemyMovePatterns;
    
    // ЗАМЕНА: Используем новый класс Motor
    private EnemyMovement _movement; 
    private EnemyMemory _enemyMemory;
    private EnemyAudioManager _audio;

    public bool SearchingEnd() => _searchTimer >= _enemyConfig.SearchingDuration;

    // Конструктор обновлен: принимаем EnemyMovement вместо NavMeshActions
    public SearchingState(Sensor[] sensors, Transform currentTransform, EnemyMovePatterns enemyMovePatterns,
                          EnemyMovement movement, EnemyMemory enemyMemory, 
                          NavMeshAgent navMeshAgent, [CanBeNull] Enemy_ScriptableObject enemyConfig, EnemyAudioManager audio)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _navMeshAgent = navMeshAgent;
        _enemyConfig = enemyConfig;
        _enemyMovePatterns = enemyMovePatterns;
        _movement = movement;
        _enemyMemory = enemyMemory;
        _audio = audio;
    }

    public override void Enter()
    {
        base.Enter();
        _hasReachedOrigin = false;
        _searchTimer = 0f;
        _isWaiting = false;
        _waitTimer = 0f;

        _navMeshAgent.stoppingDistance = 1f;
        
        // Получаем данные из памяти
        _searchOrigin = _enemyMemory.LastKnownPlayerPosition;
        _searchDir = _enemyMemory.LastKnownPlayerDirection;
        
        // Команда Мотору: Иди в точку
        _movement.MoveTo(_searchOrigin, _enemyConfig.SearchingSpeed);
        _audio.PlayStateVoice(1);
        Debug.Log("Enter SearchingState: going to last known player position");
    }

    public override void Update()
    {
        base.Update();

        // ФАЗА 1: Идем к месту, где в последний раз видели игрока
        if (!_hasReachedOrigin)
        {
            // Используем метод проверки из Movement
            if (_movement.IsPointReached(1.0f))
            {
                _movement.Stop(); // Останавливаемся по прибытии
                _hasReachedOrigin = true;
                _isWaiting = true;
                _waitTimer = _enemyConfig.PatrollingWaitTime;
                Debug.Log("Reached last known player position, starting search phase");
            }
            return;
        }

        // ФАЗА 2: Таймер общего времени поиска
        _searchTimer += Time.deltaTime;

        if (SearchingEnd())
            return;

        // ФАЗА 3: Ожидание между точками
        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _isWaiting = false;
                PickPoint(_transform.position);
            }
            return;
        }

        // ФАЗА 4: Проверка, дошли ли до случайной точки поиска
        if (_movement.IsPointReached(1.0f))
        {
            _movement.Stop();
            _isWaiting = true;
            _waitTimer = _enemyConfig.changePointInterval;
        }
    }

    private void PickPoint(Vector3 center)
    {
        // Ваша логика выбора точки в конусе сохранена
        _targetPoint = _enemyMovePatterns.RandomPointInForwardCone(_searchOrigin, _searchDir, _enemyConfig.SearchRadius, 65f);
        _movement.MoveTo(_targetPoint, _enemyConfig.SearchingSpeed);
    }

    public override void Exit()
    {
        base.Exit();
        _searchTimer = 0f;
        _isWaiting = false;
        _waitTimer = 0f;
    }
}