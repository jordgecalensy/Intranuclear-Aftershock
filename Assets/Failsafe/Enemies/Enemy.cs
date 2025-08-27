using DMDungeonGenerator;
using Failsafe.Enemies.Sensors;
using System.Collections.Generic;
using Tayx.Graphy.Utils.NumString;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using Vector3 = UnityEngine.Vector3;

public class Enemy : MonoBehaviour
{
    [SerializeField] private bool DebugMode = false;
    [SerializeField] private bool useRootMotion = true;
    private Sensor[] _sensors;
    private BehaviorStateMachine _stateMachine;
    private Animator _animator;
    private EnemyAnimator _enemyAnimator;
    private EnemyGetData _enemyGetData;
    private NavMeshAgent _navMeshAgent;
    [SerializeField] private GameObject _laserBeamPrefab;
    [SerializeField] private GameObject _laserProjectilePrefab; // Новый префаб для снаряда
    private LaserBeamController _activeLaser;
    [SerializeField] private Transform _laserSpawnPoint; // Точка спавна лазера, если нужно
    [SerializeField] private List<Transform> _manualPoints; // Привязать вручную через инспектор
    public BehaviorState currentState;
    public AwarenessMeter _awarenessMeter;
    public bool seePlayer;
    public bool hearPlayer;
    public Enemy_ScriptableObject _enemyConfig;
    private EnemyMovePatterns _enemyMovePatterns;
    private EnemyNavMeshActions _enemyNavMeshActions;
    private EnemyMemory _enemyMemory;

    private void Awake()
    {
        // Основные компоненты
        _animator = GetComponent<Animator>();
        _sensors = GetComponents<Sensor>();
        _navMeshAgent = GetComponent<NavMeshAgent>();

        if (useRootMotion)
        {
            _animator.applyRootMotion = true;
            // Отключаем автоматическое управление трансформацией для Root Motion
            _navMeshAgent.updatePosition = false;
            _navMeshAgent.updateRotation = false;
        }
        else
        {
            _animator.applyRootMotion = false;
            // Включаем автоматическое управление для движения без Root Motion
            _navMeshAgent.updatePosition = true;
            _navMeshAgent.updateRotation = false; // Отключаем, чтобы управлять вращением вручную для плавности
            _navMeshAgent.autoTraverseOffMeshLink = true; // Включаем авто-перемещение по линкам
        }

        // Создаём вспомогательные классы
        _enemyGetData = new EnemyGetData(transform);
        _awarenessMeter = new AwarenessMeter(_sensors, _enemyConfig);
        _enemyAnimator = new EnemyAnimator(_navMeshAgent, _animator, transform, this, useRootMotion);
        _enemyMovePatterns = new EnemyMovePatterns(_navMeshAgent);
        _enemyNavMeshActions = new EnemyNavMeshActions(_navMeshAgent, transform);
        _enemyMemory = new EnemyMemory();
        _awarenessMeter.Initialize();
        _awarenessMeter.ApplyCalmSensorParams();

    }

    private void Start()
    {
        

        // Создаём состояния (уже можно брать патрульные точки из Room)
        var defaultState = new DefaultState(_sensors, transform);
        var chasingState = new ChasingState(_sensors, transform, _enemyNavMeshActions, _enemyMemory, _navMeshAgent, _enemyConfig, _enemyAnimator );
        var patrolState = new PatrolState(_sensors, transform, _enemyMovePatterns, _enemyNavMeshActions,_enemyGetData,_navMeshAgent, _enemyConfig);
        var attackState = new AttackState(_sensors, transform, _enemyNavMeshActions, _enemyAnimator, _activeLaser, _laserBeamPrefab, _laserProjectilePrefab, _laserSpawnPoint, _navMeshAgent, _enemyConfig);
        var searchingState = new SearchingState(_sensors, transform, _enemyMovePatterns, _enemyNavMeshActions,_enemyMemory, _navMeshAgent, _enemyConfig);
        var checkState = new CheckState(_sensors, transform, _enemyMovePatterns,_enemyNavMeshActions , _enemyConfig);
        
        defaultState.AddTransition(chasingState, _awarenessMeter.IsChasing);
        patrolState.AddTransition(chasingState, _awarenessMeter.IsChasing);
        patrolState.AddTransition(checkState, _awarenessMeter.IsAlerted);
        defaultState.AddTransition(patrolState, defaultState.IsPatroling);
        chasingState.AddTransition(searchingState, _awarenessMeter.IsPlayerLost);
        chasingState.AddTransition(attackState, chasingState.PlayerInAttackRange);
        attackState.AddTransition(chasingState, attackState.PlayerOutOfAttackRange);
        searchingState.AddTransition(patrolState, searchingState.SearchingEnd);
        searchingState.AddTransition(chasingState, _awarenessMeter.IsChasing);
        checkState.AddTransition(chasingState, _awarenessMeter.IsChasing);

        var disabledStates = new List<BehaviorForcedState> { new DisabledState() };
        _stateMachine = new BehaviorStateMachine(defaultState, disabledStates);

        if(_manualPoints.Count > 0)
        {
            patrolState.SetManualPatrolPoints(_manualPoints);

        }
        else
        {
            // Ищем комнату, в которой находится противник
           _enemyGetData.RoomCheck();
        }
        

    }

    void Update()
    {

        _enemyAnimator.UpdateAnimator();
        _stateMachine.Update();
        _awarenessMeter.Update();
        currentState = _stateMachine.CurrentState;

        // Проверяем, нужно ли запускать логику случайных Idle анимаций
        if (currentState.GetType() != typeof(DefaultState))
        {
            _enemyAnimator.HandleIdleAnimations();
        }
    }

    [ContextMenu("DisableState")]
    public void DisableState()
    {
        _stateMachine.ForseChangeState<DisabledState>();
    }
    

    void OnAnimatorMove()
    {
        if (useRootMotion)
        {
            _enemyAnimator.ApplyRootMotion(); // Root Motion управляет позицией
        }
        else
        {
            // Когда Root Motion отключен, вручную обновляем позицию модели по NavMeshAgent
            transform.position = _navMeshAgent.nextPosition;
        }

        // Вращение обрабатывается здесь для обоих режимов, чтобы избежать конфликтов с аниматором
        _enemyNavMeshActions.UpdateAgentRotation();
    }
//Описал тут, но вызываю его в DebugManager
    public void DebugEnemy()
    {
       
        
            foreach (var sensor in _sensors)
            {
                if (sensor is VisualSensor visual)
                    if (visual.IsActivated())
                    {
                        seePlayer = true;
                    }
                    else
                    {
                        seePlayer = false;
                    }

                if (sensor is NoiseSensor noise)
                    if (noise.IsActivated())
                    {
                        hearPlayer = true;
                    }
                    else
                    {
                        hearPlayer = false;
                    }
            }

    }
}
