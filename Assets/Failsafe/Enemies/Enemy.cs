using UnityEngine;
using UnityEngine.AI;
using Failsafe.Enemies.Sensors;
using Failsafe.Scripts.Health;
using VContainer;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    [SerializeField] private bool useRootMotion = true;
    
    [SerializeField] private EnemyAnimator _enemyAnimator; // Теперь это Component!
    [SerializeField] private WeaponController _weaponController;
    [SerializeField] private Enemy_ScriptableObject _enemyConfig;
    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _navMeshAgent;
    [SerializeField] private Rigidbody _rb;
    
    private Sensor[] _sensors;
    private BehaviorStateMachine _stateMachine;
    private EnemyMovement _enemyMovement;
    private EnemyGetData _enemyGetData;
    private AwarenessMeter _awarenessMeter;
    private EnemyMovePatterns _enemyMovePatterns;
    private EnemyMemory _enemyMemory;
    private EnemyAudioManagerBase _audioManager;
    private IHealth _health;
    private EnemyNavMeshActions _enemyNavMeshActions;
    private EnemyLinkTraverser _linkTraverser;
    [Header("Настройки патрулирования")]
    [Tooltip("Если заданы ручные точки, враг будет ходить по ним. Если пусто - использует точки комнаты.")]
    [SerializeField] private Transform[] _manualPatrolPoints;
    // Свойства
    public BehaviorState currentState;
    public IHealth Health => _health;
    public EnemyMovement Movement => _enemyMovement;
    public Enemy_ScriptableObject EnemyConfig => _enemyConfig;

    [Inject]
    public void Construct(IHealth health) => _health = health;

    private void Awake()
    {
        // Автоматический поиск компонентов, если не заданы в Inspector
        if (!_animator) _animator = GetComponent<Animator>();
        if (!_navMeshAgent) _navMeshAgent = GetComponent<NavMeshAgent>();
        if (!_rb) _rb = GetComponent<Rigidbody>();
        if (!_weaponController) _weaponController = GetComponent<WeaponController>();
        if (!_enemyAnimator) _enemyAnimator = GetComponent<EnemyAnimator>(); 
        _linkTraverser = GetComponent<EnemyLinkTraverser>();
        _sensors = GetComponents<Sensor>();
        _audioManager = GetComponent<EnemyAudioManagerBase>();

        // 1. Инициализируем МОТОР
        _enemyMovement = new EnemyMovement(transform, _navMeshAgent, _rb, this, useRootMotion);
        _enemyNavMeshActions = new EnemyNavMeshActions(_navMeshAgent, transform);

        // 2. Инициализируем ВИЗУАЛ (Просто передаем зависимость)
        // Больше никаких new EnemyAnimator(...)
        if (_enemyAnimator != null)
        {
            _enemyAnimator.Initialize(_enemyMovement);
        }
        else
        {
            Debug.LogError("EnemyAnimator component missing!");
        }

        // 3. Остальные системы
        _enemyGetData = new EnemyGetData(transform);
        _awarenessMeter = new AwarenessMeter(_sensors, _enemyConfig);
        _enemyMovePatterns = new EnemyMovePatterns(_navMeshAgent);
        _enemyMemory = new EnemyMemory();
        
        _awarenessMeter.Initialize();
        _awarenessMeter.ApplyCalmSensorParams();
        
        if (_linkTraverser != null)
        {
            _linkTraverser.Initialize(_navMeshAgent, _enemyAnimator);
        }
    }

    private void Start()
    {
        // Инициализация стейтов (код тот же, просто передаем ссылку на компонент _enemyAnimator)
        var defaultState = new DefaultState(_sensors, transform, _enemyMovement);
        var alertState = new AlertState(_enemyMovement, _enemyAnimator, _sensors, _audioManager, 1.5f);
        
        var chasingState = new ChasingState(
            _sensors, 
            transform, 
            _enemyNavMeshActions,
            _enemyMemory, 
            _enemyConfig, 
            _enemyAnimator, 
            _audioManager
        );
        
        var patrolState = new PatrolState(
            _sensors, transform, _enemyMovePatterns, _enemyMovement, 
            _enemyGetData, _navMeshAgent, _enemyConfig,
            _manualPatrolPoints 
        );
        
        var attackState = new AttackState(_sensors, transform, _enemyMovement, _enemyAnimator, _enemyConfig);
        
        var searchingState = new SearchingState(
            _sensors, transform, _enemyMovePatterns, _enemyMovement, 
            _enemyMemory, _navMeshAgent, _enemyConfig
        );
        
        var checkState = new CheckState(
            _sensors, transform, _enemyMovePatterns, _enemyMovement, 
            _enemyConfig, _audioManager
        );
        
        var disabledState = new DisabledState(_animator, _enemyMovement);
        var stunnedState = new StunnedState(_enemyAnimator, _enemyMovement, transform);
        var deathState = new EnemyDeathState(_enemyAnimator, _enemyMovement, _animator, _enemyConfig);

        // Переходы
        defaultState.AddTransition(alertState, _awarenessMeter.IsChasing);
        patrolState.AddTransition(alertState, _awarenessMeter.IsChasing);
        checkState.AddTransition(alertState, _awarenessMeter.IsChasing);
        alertState.AddTransition(chasingState, alertState.IsAnimationFinished);
        patrolState.AddTransition(checkState, _awarenessMeter.IsAlerted);
        defaultState.AddTransition(patrolState, defaultState.IsPatroling);
        chasingState.AddTransition(searchingState, _awarenessMeter.IsPlayerLost);
        chasingState.AddTransition(attackState, chasingState.PlayerInAttackRange);
        attackState.AddTransition(chasingState, attackState.PlayerOutOfAttackRange);
        searchingState.AddTransition(patrolState, searchingState.SearchingEnd);
        searchingState.AddTransition(alertState, _awarenessMeter.IsChasing);

        var forcedStates = new List<BehaviorForcedState> {disabledState, stunnedState, deathState};
        _stateMachine = new BehaviorStateMachine(defaultState, forcedStates);

        if (_enemyGetData != null) _enemyGetData.RoomCheck();

        _health.OnDeath += DeathState;
        _health.OnHealthChanged += (damage) => _audioManager.PlayDamageVoice();
    }

    void Update()
    {
        _stateMachine.Update();
        _enemyMovement.HandleRotationAndMovement();
        
        // Теперь обновляем компонент
        _enemyAnimator.ManualUpdate();
        
        _awarenessMeter.Update();
        currentState = _stateMachine.CurrentState;
        
        if (_linkTraverser != null)
        {
            _linkTraverser.CheckAndTraverseLink();
        }

// Заблокируй мотор, если мы в прыжке
        if (_linkTraverser == null || !_linkTraverser.IsTraversing)
        {
            _stateMachine.Update();
            _enemyMovement.HandleRotationAndMovement();
        }
    }

    void OnAnimatorMove()
    {
        _enemyAnimator.OnAnimatorMove();
    }

    // Остальные методы (Jump, LinkTester, DisabledState и т.д.) без изменений
    public void DeathState() => _stateMachine.ForseChangeState<EnemyDeathState>();
    public void DisableState(float? duration = null) => _stateMachine.ForseChangeState<DisabledState>(duration);
    public void StunnedState(Vector3 direction, float? duration = null)
    {
        if (!(_stateMachine.CurrentState is StunnedState))
        {
            _stateMachine.GetForcedState<StunnedState>().SetDirection(direction);
            _stateMachine.ForseChangeState<StunnedState>(duration);
        }
    }
    public float DebugAlertness => _awarenessMeter != null ? _awarenessMeter.AlertnessValue : 0f;
    
    public bool DebugCanSeePlayer
    {
        get
        {
            if (_sensors == null) return false;
            foreach (var s in _sensors)
            {
                if (s is VisualSensor v && v.IsActivated()) return true;
            }
            return false;
        }
    }

    public bool DebugCanHearPlayer
    {
        get
        {
            if (_sensors == null) return false;
            foreach (var s in _sensors)
            {
                if (s is NoiseSensor n && n.IsActivated()) return true;
            }
            return false;
        }
    }
}