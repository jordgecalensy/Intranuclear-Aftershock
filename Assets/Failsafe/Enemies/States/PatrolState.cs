using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PatrolState : BehaviorState
{
    private readonly EnemyMovePatterns _enemyMovePatterns;
    private readonly EnemyMovement _movement; // <-- Новый класс
    private NavMeshAgent _navMeshAgent;
    private Enemy_ScriptableObject _enemyConfig;
    private Transform _enemyPos;
    private EnemyGetData _enemyGetData;
    private List<Transform> _patrolPoints = new();
    private int _currentPatrolPointIndex = -1;
    private Vector3 _patrolPoint;

    private float _waitTimer;
    private bool _isWaiting;

    // Конструктор обновлен
    public PatrolState(Sensor[] sensors, Transform enemyPos, EnemyMovePatterns enemyMovePatterns, 
                       EnemyMovement movement, EnemyGetData enemyGetData, 
                       NavMeshAgent navMeshAgent, Enemy_ScriptableObject enemyConfig)
    {
        _enemyMovePatterns = enemyMovePatterns;
        _movement = movement;
        _navMeshAgent = navMeshAgent;
        _enemyConfig = enemyConfig;
        _enemyPos = enemyPos;
        _enemyGetData = enemyGetData;
    }

    public override void Enter()
    {
        base.Enter();
        _navMeshAgent.stoppingDistance = 1f;
        ChoosePatrolStyle();
    }

    private void ChoosePatrolStyle()
    {
        if (_patrolPoints == null || _patrolPoints.Count == 0)
            _patrolPoints = _enemyGetData.GetRoomPatrolPoints();

        if (_patrolPoints == null || _patrolPoints.Count == 0)
        {
            _patrolPoint = _enemyMovePatterns.RandomPointAround(_enemyPos.position, _enemyConfig.offsetSearchingPoint);
            _movement.MoveTo(_patrolPoint, _enemyConfig.PatrolingSpeed);
        }
        else
        {
            _currentPatrolPointIndex = -1;
            HandlePatrolling();
        }
    }

    public override void Update()
    {
        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _isWaiting = false;
                HandlePatrolling();
            }
            return;
        }

        // Проверка дистанции через новый метод
        if (_movement.IsPointReached(1.5f))
        {
            _movement.Stop(); 
            _isWaiting = true;
            _waitTimer = _enemyConfig.PatrollingWaitTime;
        }
    }

    private void HandlePatrolling()
    {
        if (_patrolPoints == null || _patrolPoints.Count == 0)
        {
            _patrolPoint = _enemyMovePatterns.RandomPointAround(_enemyPos.position, _enemyConfig.offsetSearchingPoint);
        }
        else
        {
            _currentPatrolPointIndex = (_currentPatrolPointIndex + 1) % _patrolPoints.Count;
            _patrolPoint = _patrolPoints[_currentPatrolPointIndex].position;
        }

        _movement.MoveTo(_patrolPoint, _enemyConfig.PatrolingSpeed);
    }
    
    public void SetManualPatrolPoints(List<Transform> points, bool restart = true)
    {
        _patrolPoints = points ?? new List<Transform>();
        if (restart)
        {
            _currentPatrolPointIndex = -1;
            HandlePatrolling();
        }
    }
}