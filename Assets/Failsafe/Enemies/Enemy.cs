using DMDungeonGenerator;
using Failsafe.Enemies.Sensors;
using System.Collections;
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
    [Header("Anim triggers (имена)")]
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string landTrigger = "Land";

    [Header("Arc & Timing")]
    [SerializeField] private float baseArcHeight = 1.2f;
    [SerializeField] private float speedXZ = 4.0f;      // м/с по плоскости
    [SerializeField] private float speedY  = 3.0f;      // м/с по высоте
    [SerializeField] private float minDuration = 0.35f; // минимальное время прыжка
    [SerializeField] private float endSnapTolerance = 0.25f;
    [SerializeField] private bool  scaleArcByUpDelta = true; // добавлять дугу при прыжке "вверх"
    private Rigidbody rb;
    bool busy;

    private void Awake()
    {
        // Основные компоненты
        _animator = GetComponent<Animator>();
        _sensors = GetComponents<Sensor>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        
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
        LinkTester();
        currentState = _stateMachine.CurrentState;
        // Проверяем, нужно ли запускать логику случайных Idle анимаций
        if (currentState.GetType() != typeof(DefaultState))
        {
            _enemyAnimator.IsActive();
            _enemyAnimator.HandleIdleAnimations();
        }
        else
        {
            _enemyAnimator.IsOff();
        }
        
    }

    [ContextMenu("DisableState")]
    public void DisableState(float? duration = null)
    {
        _stateMachine.ForseChangeState<DisabledState>(duration);
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

    /// <summary>
    /// Прыжок по дуге от start до end.
    /// Управляет Rigidbody вручную (kinematic), в начале и в конце дёргает триггеры анимаций.
    /// Если есть NavMeshAgent, он отключается на время прыжка и синхронизируется в конце.
    /// </summary>
    /// <summary>
    /// Прыжок по дуге от start до end.
    /// Управляет Rigidbody вручную (kinematic), в начале и в конце дёргает триггеры анимаций.
    /// Если есть NavMeshAgent, он отключается на время прыжка и синхронизируется в конце.
    /// </summary>
    public void Jump(Vector3 start, Vector3 end)
    {
        if (!busy) StartCoroutine(JumpRoutine(start, end));
    }

    IEnumerator JumpRoutine(Vector3 start, Vector3 end)
    {
        busy = true;
        Vector3 oldDestenation = _navMeshAgent.destination;
        // --- Подготовка агента ---
        bool hadAgent = _navMeshAgent != null && _navMeshAgent.enabled;
        bool oldUpdPos = true, oldUpdRot = true;
        if (hadAgent)
        {
            oldUpdPos = _navMeshAgent.updatePosition;
            oldUpdRot = _navMeshAgent.updateRotation;
            _navMeshAgent.updatePosition = false;
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.enabled = false; // чтобы агент не тянул трансформ
        }

        bool oldKinematic = rb.isKinematic;
        rb.isKinematic = true;

        // --- Анимация старта ---
        if (_animator && !string.IsNullOrEmpty(jumpTrigger)) _animator.SetTrigger(jumpTrigger);

        // --- Поворот к цели по XZ ---
        Vector3 dir = end - start; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);

        // === ВАЖНО: расчёт длительности ===
        float dxz = Vector3.Distance(new Vector3(start.x,0,start.z), new Vector3(end.x,0,end.z));
        float dy  = end.y - start.y;

        float tXZ = dxz / Mathf.Max(0.01f, speedXZ);
        float tY  = Mathf.Abs(dy) / Mathf.Max(0.01f, speedY);
        float duration = Mathf.Max(minDuration, Mathf.Max(tXZ, tY)); // <-- выравнивание скорости вверх/вниз

        // (альтернатива вместо tY: учесть вертикаль в «эффективной длине»)
        // float verticalWeight = 2.0f;
        // float effectiveLen = Mathf.Sqrt(dxz*dxz + (verticalWeight*Mathf.Abs(dy))*(verticalWeight*Mathf.Abs(dy)));
        // float duration = Mathf.Max(minDuration, effectiveLen / speedXZ);

        // Коррекция высоты дуги при прыжке вверх (чтоб не «иголка»)
        float arc = baseArcHeight;
        if (scaleArcByUpDelta && dy > 0f)
            arc += dy * 0.35f; // коэффициент подстрой: 0.25–0.5 обычно хорошо

        // --- Полёт по управляемой параболе ---
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += 4f * arc * t * (1f - t); // парабола колоколом

            rb.MovePosition(pos);
            yield return null;
        }

        // Прилипнуть к финишу
        if ((rb.position - end).sqrMagnitude > endSnapTolerance * endSnapTolerance)
            rb.MovePosition(end);

        // --- Анимация приземления ---
        if (_animator && !string.IsNullOrEmpty(landTrigger)) _animator.SetTrigger(landTrigger);

        // --- Возврат агента ---
        if (hadAgent)
        {
            _navMeshAgent.enabled = true;
            if (_navMeshAgent.isOnOffMeshLink) _navMeshAgent.CompleteOffMeshLink();
            _navMeshAgent.Warp(end);
            _navMeshAgent.updatePosition = oldUpdPos;
            _navMeshAgent.updateRotation = oldUpdRot;
        }

        _navMeshAgent.SetDestination(oldDestenation);
        rb.isKinematic = oldKinematic;
        busy = false;
    }


    private void LinkTester()
    {
        _navMeshAgent.autoTraverseOffMeshLink = false; // чтобы агент сам не пролетал линк
        // Агент подошёл к линку, но ещё не начал
        if (_navMeshAgent.hasPath && _navMeshAgent.nextOffMeshLinkData.valid)
        {
            var next = _navMeshAgent.nextOffMeshLinkData;
            Jump(next.startPos, next.endPos);
            Debug.Log($"NEXT LINK: start={next.startPos}, end={next.endPos}");
        }

        // Агент реально находится на линке
        if (_navMeshAgent.isOnOffMeshLink)
        {
            var cur = _navMeshAgent.currentOffMeshLinkData;
            if (cur.valid)
                Jump(cur.startPos, cur.endPos);
                Debug.Log($"CURRENT LINK: start={cur.startPos}, end={cur.endPos}");
        }
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
