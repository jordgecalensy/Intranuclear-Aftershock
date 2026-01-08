using Cysharp.Threading.Tasks.Triggers;
using DMDungeonGenerator;
using Failsafe.Enemies.Sensors;
using Failsafe.Scripts.Health;
using System.Collections;
using System.Collections.Generic;
using Tayx.Graphy.Utils.NumString;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using VContainer;
using Vector3 = UnityEngine.Vector3;

public class Enemy : MonoBehaviour
{
    [SerializeField] private bool DebugMode = false;
    [SerializeField] private bool useRootMotion = true;
    
    // --- Компоненты ---
    private Sensor[] _sensors;
    private BehaviorStateMachine _stateMachine;
    private Animator _animator;
    private NavMeshAgent _navMeshAgent;
    private Rigidbody _rb;
    private IHealth _health;

    // --- Новая Архитектура (Motor-View) ---
    private EnemyAnimator _enemyAnimator;
    private EnemyMovement _enemyMovement; // Замена EnemyNavMeshActions

    // --- Данные ---
    private EnemyGetData _enemyGetData;
    private LaserBeamController _activeLaser;
    [SerializeField] private Transform _laserSpawnPoint;
    [SerializeField] private List<Transform> _manualPoints;
    [SerializeField] private GameObject _corpseModel;
    
    public BehaviorState currentState;
    public AwarenessMeter _awarenessMeter;
    public bool seePlayer;
    public bool hearPlayer;
    
    [SerializeField] private Enemy_ScriptableObject _enemyConfig;
    public Enemy_ScriptableObject EnemyConfig => _enemyConfig;
    
    private EnemyMovePatterns _enemyMovePatterns;
    private EnemyMemory _enemyMemory;

    [Header("Anim triggers (имена)")]
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string landTrigger = "Land";

    [Header("Arc & Timing")]
    [SerializeField] private float baseArcHeight = 1.2f;
    [SerializeField] private float speedXZ = 4.0f;
    [SerializeField] private float speedY  = 3.0f;
    // (Остальные параметры прыжка используются внутри EnemyMovement)

    public IHealth Health => _health;
    
    // Публичный доступ к мотору (для других скриптов, если нужно)
    public EnemyMovement Movement => _enemyMovement;

    [Inject]
    public void Construct(IHealth health)
    {
        _health = health;
    }

    private void Awake()
    {
        // 1. Кэшируем компоненты
        _animator = GetComponent<Animator>();
        _sensors = GetComponents<Sensor>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        
        // 2. Инициализируем МОТОР (EnemyMovement)
        // Передаем 'this' как MonoBehaviour для запуска корутин прыжка
        _enemyMovement = new EnemyMovement(transform, _navMeshAgent, _rb, this, useRootMotion);

        // 3. Инициализируем ВИЗУАЛ (EnemyAnimator)
        // Он зависит от Мотора
        _enemyAnimator = new EnemyAnimator(_animator, _enemyMovement);

        // 4. Вспомогательные классы
        _enemyGetData = new EnemyGetData(transform);
        _awarenessMeter = new AwarenessMeter(_sensors, _enemyConfig);
        _enemyMovePatterns = new EnemyMovePatterns(_navMeshAgent);
        _enemyMemory = new EnemyMemory();
        
        _awarenessMeter.Initialize();
        _awarenessMeter.ApplyCalmSensorParams();
    }

    private void Start()
    {
        // 5. Инициализация СОСТОЯНИЙ
        // Везде передаем _enemyMovement вместо _enemyNavMeshActions

        var defaultState = new DefaultState(_sensors, transform, _enemyMovement);
        
        // AlertState (Промежуточное состояние обнаружения)
        // Длительность 1.5 сек (или вынесите в конфиг)
        var alertState = new AlertState(_enemyMovement, _enemyAnimator, _sensors, 1.5f);
        
        var chasingState = new ChasingState(
            _sensors, transform, _enemyMovement, _enemyMemory, 
            _navMeshAgent, _enemyConfig, _enemyAnimator 
        );
        
        var patrolState = new PatrolState(
            _sensors, transform, _enemyMovePatterns, _enemyMovement, 
            _enemyGetData, _navMeshAgent, _enemyConfig
        );
        
        var attackState = new AttackState(
            _sensors, transform, _enemyMovement, _enemyAnimator, 
            _laserSpawnPoint, _enemyConfig
        );
        
        var searchingState = new SearchingState(
            _sensors, transform, _enemyMovePatterns, _enemyMovement, 
            _enemyMemory, _navMeshAgent, _enemyConfig
        );
        
        var checkState = new CheckState(
            _sensors, transform, _enemyMovePatterns, _enemyMovement, 
            _enemyConfig
        );
        
        var disabledState = new DisabledState(_animator, _enemyMovement);
        var stunnedState = new StunnedState(_enemyAnimator, _enemyMovement, transform);
        var deathState = new EnemyDeathState(_enemyAnimator, _enemyMovement, _animator);

        // --- НАСТРОЙКА ПЕРЕХОДОВ ---

        // 1. Реакция на обнаружение (через Alert)
        defaultState.AddTransition(alertState, _awarenessMeter.IsChasing);
        patrolState.AddTransition(alertState, _awarenessMeter.IsChasing);
        checkState.AddTransition(alertState, _awarenessMeter.IsChasing);
        
        // 2. Переход к Погоне после анимации Alert
        alertState.AddTransition(chasingState, alertState.IsAnimationFinished);
        
        // 3. Остальные переходы
        patrolState.AddTransition(checkState, _awarenessMeter.IsAlerted);
        defaultState.AddTransition(patrolState, defaultState.IsPatroling);
        
        chasingState.AddTransition(searchingState, _awarenessMeter.IsPlayerLost);
        chasingState.AddTransition(attackState, chasingState.PlayerInAttackRange);
        
        attackState.AddTransition(chasingState, attackState.PlayerOutOfAttackRange);
        
        searchingState.AddTransition(patrolState, searchingState.SearchingEnd);
        searchingState.AddTransition(alertState, _awarenessMeter.IsChasing); // Если нашли игрока во время поиска -> снова Alert или сразу Chasing

        var forcedStates = new List<BehaviorForcedState> {disabledState, stunnedState, deathState};
        _stateMachine = new BehaviorStateMachine(defaultState, forcedStates);

        if(_manualPoints.Count > 0)
        {
            patrolState.SetManualPatrolPoints(_manualPoints);
        }
        else
        {
           _enemyGetData.RoomCheck();
        }

        _health.OnDeath += DeathState;
    }

    void Update()
    {
        // 1. Логика (Brain) - принимает решения
        _stateMachine.Update();
        
        // 2. Физика (Motor) - выполняет движение и вращение (Stop-Turn-Go)
        _enemyMovement.HandleRotationAndMovement();
        
        // 3. Визуал (View) - обновляет BlendTree и параметры аниматора
        _enemyAnimator.UpdateAnimator();
        
        // 4. Утилиты
        _awarenessMeter.Update();
        LinkTester();
        
        currentState = _stateMachine.CurrentState;
        
        // Логика Idle
        if (currentState.GetType() != typeof(DefaultState))
        {
            // Метод IsActive/IsOff нужно добавить в EnemyAnimator если он используется
            // или просто вызывать обработку Idle
             _enemyAnimator.HandleIdleAnimations();
        }
    }

    public void DisableState(float? duration = null)
    {
        _stateMachine.ForseChangeState<DisabledState>(duration);
    }

    public void DeathState()
    {
        _stateMachine.ForseChangeState<EnemyDeathState>();
    }

    public void StunnedState(Vector3 direction, float? duration = null)
    {
        if (!(_stateMachine.CurrentState is StunnedState))
        {
            _stateMachine.GetForcedState<StunnedState>().SetDirection(direction);
            _stateMachine.ForseChangeState<StunnedState>(duration);
        }
    }

    void OnAnimatorMove()
    {
        // Передаем root motion из анимации в мотор
        _enemyAnimator.OnAnimatorMove();
    }

    /// <summary>
    /// Прыжок теперь делегируется в EnemyMovement
    /// </summary>
    public void Jump(Vector3 start, Vector3 end)
    {
        _enemyMovement.Jump(start, end, speedXZ, speedY, baseArcHeight, _animator, jumpTrigger, landTrigger);
    }

    private void LinkTester()
    {
        _navMeshAgent.autoTraverseOffMeshLink = false; 
        
        // Не прыгаем, если уже заняты (прыжком или другим действием)
        if (_enemyMovement.IsBusy) return;

        if (_navMeshAgent.hasPath && _navMeshAgent.nextOffMeshLinkData.valid)
        {
            var next = _navMeshAgent.nextOffMeshLinkData;
            Jump(next.startPos, next.endPos);
        }

        if (_navMeshAgent.isOnOffMeshLink)
        {
            var cur = _navMeshAgent.currentOffMeshLinkData;
            if (cur.valid)
                Jump(cur.startPos, cur.endPos);
        }
    }
    
    // Debug методы
    public void DebugEnemy()
    {
        foreach (var sensor in _sensors)
        {
            if (sensor is VisualSensor visual)
                seePlayer = visual.IsActivated();
            if (sensor is NoiseSensor noise)
                hearPlayer = noise.IsActivated();
        }
    }

    public void ReplaceWithDummy()
    {
        Instantiate(_corpseModel, transform.position, transform.rotation);
        Destroy(this.gameObject);
    }
}