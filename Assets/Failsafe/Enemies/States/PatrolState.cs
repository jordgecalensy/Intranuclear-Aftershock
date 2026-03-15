using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Failsafe.Enemies.Sensors;

public class PatrolState : BehaviorState
{
    private Sensor[] _sensors;
    private Transform _enemyTransform;
    private EnemyMovePatterns _movePatterns;
    private EnemyMovement _movement;
    private EnemyGetData _getData;
    private NavMeshAgent _agent;
    private Enemy_ScriptableObject _config;

    private Transform[] _manualPatrolPoints;
    private EnemyLinkTraverser _linkTraverser;

    private int _currentManualIndex = 0;
    private float _waitTimer = 0f;
    private bool _isWaiting = false;
    
    private Vector3 _lastPosition;
    private float _stuckTimer = 0f;

    public PatrolState(
        Sensor[] sensors, Transform transform, EnemyMovePatterns movePatterns, 
        EnemyMovement movement, EnemyGetData getData, NavMeshAgent agent, 
        Enemy_ScriptableObject config,
        Transform[] manualPatrolPoints = null, EnemyLinkTraverser linkTraverser = null)
    {
        _sensors = sensors;
        _enemyTransform = transform;
        _movePatterns = movePatterns;
        _movement = movement;
        _getData = getData;
        _agent = agent;
        _config = config;
        
        _manualPatrolPoints = manualPatrolPoints;
        _linkTraverser = linkTraverser;
    }

    public override void Enter()
    {
        _stuckTimer = 0f;
        _isWaiting = false;
        _lastPosition = _enemyTransform.position;

        MoveToNextPoint();
    }

    public override void Update()
    {
        // 1. Защита от прыжков (отключаем логику, пока паук на линке)
        if (_linkTraverser != null && _linkTraverser.IsTraversing)
        {
            _lastPosition = _enemyTransform.position; 
            return; 
        }

        // 2. Основная логика
        if (_isWaiting)
        {
            HandleWaiting();
        }
        else
        {
            CheckDestinationReached();
            CheckForStuck();
        }

        _lastPosition = _enemyTransform.position;
    }

    public override void Exit()
    {
        _movement.Stop();
    }

    private void MoveToNextPoint()
    {
        Vector3 targetPosition = _enemyTransform.position;

        // ПРИОРИТЕТ 1: Ручные точки из инспектора
        if (HasManualPoints())
        {
            targetPosition = _manualPatrolPoints[_currentManualIndex].position;
            _currentManualIndex = (_currentManualIndex + 1) % _manualPatrolPoints.Length;
        }
        else
        {
            // ПРИОРИТЕТ 2: Точки текущей комнаты
            List<Transform> roomPoints = _getData.GetRoomPatrolPoints();
            if (roomPoints != null && roomPoints.Count > 0)
            {
                // Берем случайную точку из комнаты
                int randomIndex = Random.Range(0, roomPoints.Count);
                targetPosition = roomPoints[randomIndex].position;
            }
            // ПРИОРИТЕТ 3: Свободное блуждание, если точек комнаты нет
            else
            {
                // Блуждаем в радиусе 10 метров
                targetPosition = _movePatterns.RandomPointAround(_enemyTransform.position, 10f);
            }
        }

        _movement.MoveTo(targetPosition, _config.PatrolingSpeed);
    }

    private void CheckDestinationReached()
    {
        if (_movement.IsPointReached(_agent.stoppingDistance + 0.1f))
        {
            _isWaiting = true;
            _waitTimer = 2f; // Время ожидания на точке
            _movement.Stop();
        }
    }

    private void HandleWaiting()
    {
        _waitTimer -= Time.deltaTime;
        
        if (_waitTimer <= 0f)
        {
            _isWaiting = false;
            MoveToNextPoint();
        }
    }

    private void CheckForStuck()
    {
        if (Vector3.Distance(_enemyTransform.position, _lastPosition) < 0.01f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 2f)
            {
                MoveToNextPoint();
                _stuckTimer = 0f;
            }
        }
        else
        {
            _stuckTimer = 0f; 
        }
    }

    private bool HasManualPoints()
    {
        return _manualPatrolPoints != null && _manualPatrolPoints.Length > 0;
    }
}