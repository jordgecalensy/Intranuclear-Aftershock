using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Failsafe.Scripts.Damage.Implementation;

[RequireComponent(typeof(Rigidbody))] // Гарантируем наличие Rigidbody
public class DamageObstacle : MonoBehaviour
{
    [Header("1. Вращение")]
    [SerializeField] private bool rotate = false;
    [SerializeField] private Vector3 rotationAxis = Vector3.up; 
    [SerializeField] private float rotationSpeed = 90f;         

    [Header("2. Цикличность (Лазер/Газ)")]
    [SerializeField] private bool useCycle = false;       
    [SerializeField] private float activeTime = 2f;       
    [SerializeField] private float inactiveTime = 2f;     
    [SerializeField] private GameObject visualModel;      
    
    [Header("3. Движение по путям")]
    [SerializeField] private bool movable = true;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitAtWaypoint = 0f;
    [SerializeField] private List<Transform> waypoints;   
    
    [Header("Настройки Урона")]
    [SerializeField] private bool canDealDamage = true;
    [SerializeField] private bool damagePlayers = true;
    [SerializeField] private bool damageEnemies = false;
    [SerializeField] private LayerMask playerLayers = 0;
    [SerializeField] private LayerMask enemyLayers = 0;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float damageAmount = 10f;
    [SerializeField] private float damageInterval = 1f;

    [Header("Триггер & Физика")]
    [SerializeField] private Collider damageTrigger;
    // Кинематик обязателен для MoveRotation
    [SerializeField] private bool autoAddKinematicRigidbody = true;
    
    [Header("Прилипание игрока")]
    [SerializeField] private bool stickPlayerOnTop = true;
    [SerializeField] private float topTolerance = 0.15f;
    [SerializeField] private bool stickOnlyPlayers = true;

    [Header("Stasis")]
    [SerializeField] private bool freezeByStasis = true;
    [SerializeField] private bool freezeAlsoDamage = true;

    // Внутренние переменные
    private int _currentWaypointIndex = 0;
    private float _waitTimer = 0f;
    private bool _isWaiting = false;
    private Rigidbody _rb;
    private Vector3 _startPosition; 

    private float _cycleTimer;
    private bool _isCycleActive = true; 
    private bool _frozen;

    private readonly Dictionary<DamageableComponent, int> _overlapCount = new();
    private readonly Dictionary<DamageableComponent, float> _timers = new();
    private static readonly List<DamageableComponent> _tmp = new();
    private readonly Dictionary<Transform, int> _stickOverlap = new();
    private readonly Dictionary<Transform, Transform> _oldParents = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        SetupCollider();
        SetupRigidbody();
    }

    private void Start()
    {
        _startPosition = transform.position;
        _cycleTimer = activeTime; 
        
        if (useCycle && visualModel == null)
        {
            var mesh = GetComponent<MeshRenderer>();
            if (mesh != null) visualModel = gameObject;
        }
    }

    private void Update()
    {
        // В Update оставляем только логику таймеров (не физику!)
        if (_frozen) return; 

        HandleCycle(Time.deltaTime);
        TickDamage(Time.deltaTime);
    }

    private void FixedUpdate()
    {   
        // 1. Если Стазис -> полностью выходим, Rigidbody остается на месте
        if (_frozen) return;

        // 2. Если выключены циклом (газ не идет) -> выходим
        if (useCycle && !_isCycleActive) return;

        // 3. ФИЗИЧЕСКОЕ ВРАЩЕНИЕ
        HandleRotationFixed(Time.fixedDeltaTime);

        // 4. ФИЗИЧЕСКОЕ ДВИЖЕНИЕ
        HandleMovementFixed(Time.fixedDeltaTime);
        
        
        
        // Гарантия кинематики (чтобы объект не падал и не отлетал от ударов)
        if(_rb != null && !_rb.isKinematic) _rb.isKinematic = true;
    }

    // =========================================================
    // ЛОГИКА ВРАЩЕНИЯ (RIGIDBODY)
    // =========================================================
    
    private void HandleRotationFixed(float dt)
    {
        if (!rotate) return;
        if (_rb == null) return;

        // Вычисляем, насколько нужно повернуть за этот кадр
        Quaternion deltaRotation = Quaternion.Euler(rotationAxis * rotationSpeed * dt);
        
        // MoveRotation плавно интерполирует вращение и обновляет физику
        // Это "телепортация" в следующий поворот, но с учетом физики
        _rb.MoveRotation(_rb.rotation * deltaRotation);
    }

    // =========================================================
    // ЛОГИКА ДВИЖЕНИЯ (RIGIDBODY)
    // =========================================================

    private void HandleMovementFixed(float dt)
    {
        if (!movable) return;
        if (waypoints == null || waypoints.Count == 0) return;

        Vector3 targetPos = waypoints[_currentWaypointIndex].position;

        if (Vector3.Distance(transform.position, targetPos) < 0.02f) // Чуть увеличил порог для надежности
        {
            if (!_isWaiting)
            {
                _isWaiting = true;
                _waitTimer = waitAtWaypoint;
            }
            else
            {
                // Таймер ожидания уменьшаем, но проверяем его в FixedUpdate
                _waitTimer -= dt;
                if (_waitTimer <= 0)
                {
                    _isWaiting = false;
                    NextWaypoint();
                }
            }
        }
        else
        {
            // Двигаем через Rigidbody
            Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * dt);
            _rb.MovePosition(nextPos);
        }
    }

    // =========================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (Без изменений логики)
    // =========================================================

    public void SetRotationActive(bool active) => rotate = active;
    public void SetRotationSpeed(float speed) => rotationSpeed = speed;

    private void NextWaypoint()
    {
        _currentWaypointIndex++;
        if (_currentWaypointIndex >= waypoints.Count) _currentWaypointIndex = 0;
    }

    private void HandleCycle(float dt)
    {
        if (!useCycle) return;
        _cycleTimer -= dt;
        if (_cycleTimer <= 0f) ToggleCycleState(!_isCycleActive);
    }

    private void ToggleCycleState(bool isActive)
    {
        _isCycleActive = isActive;
        _cycleTimer = isActive ? activeTime : inactiveTime;

        if (damageTrigger != null) damageTrigger.enabled = isActive;
        
        if (visualModel != null) visualModel.SetActive(isActive);
        else {
            var r = GetComponent<Renderer>();
            if(r != null) r.enabled = isActive;
        }

        if (!isActive) {
            _timers.Clear();
            _overlapCount.Clear();
        }
    }

    private void TickDamage(float dt)
    {
        if (!canDealDamage) return;
        if (_frozen && freezeAlsoDamage) return;
        if (useCycle && !_isCycleActive) return;
        if (_timers.Count == 0) return;

        float interval = Mathf.Max(0.01f, damageInterval);
        _tmp.Clear();
        foreach (var kv in _timers) _tmp.Add(kv.Key);

        foreach (var d in _tmp) {
            if (d == null) {
                _timers.Remove(d);
                _overlapCount.Remove(d);
                continue;
            }
            float t = _timers[d] - dt;
            if (t <= 0f) {
                d.TakeDamage(new FlatDamage(damageAmount));
                t = interval;
            }
            _timers[d] = t;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stickPlayerOnTop) TryStick(other);
        if (!canDealDamage || (useCycle && !_isCycleActive)) return; 

        var damageable = FindDamageable(other);
        if (damageable == null || !IsAllowedTarget(damageable.gameObject)) return;

        if (_overlapCount.TryGetValue(damageable, out int cnt)) _overlapCount[damageable] = cnt + 1;
        else _overlapCount[damageable] = 1;

        if (!_timers.ContainsKey(damageable)) _timers[damageable] = 0f; 
    }

    private void OnTriggerExit(Collider other)
    {
        if (stickPlayerOnTop) TryUnstick(other);
        if (!canDealDamage) return;
        
        var damageable = FindDamageable(other);
        if (damageable == null) return;

        if (_overlapCount.TryGetValue(damageable, out int cnt)) {
            cnt -= 1;
            if (cnt <= 0) {
                _overlapCount.Remove(damageable);
                _timers.Remove(damageable);
            } else _overlapCount[damageable] = cnt;
        }
    }

    // --- Helpers (Поиск компонентов, проверки) ---
    private DamageableComponent FindDamageable(Collider other)
    {
        var d = other.GetComponentInParent<DamageableComponent>();
        if (d != null) return d;
        if (other.attachedRigidbody != null) {
            d = other.attachedRigidbody.GetComponentInChildren<DamageableComponent>();
            if (d != null) return d;
        }
        return other.transform.root.GetComponentInChildren<DamageableComponent>();
    }

    private bool IsAllowedTarget(GameObject go)
    {
        if (damagePlayers && IsPlayer(go)) return true;
        if (damageEnemies && IsEnemy(go)) return true;
        return false;
    }
    private bool IsPlayer(GameObject go) {
        if (playerLayers.value != 0) return (playerLayers.value & (1 << go.layer)) != 0;
        return go.CompareTag(playerTag) || go.transform.root.CompareTag(playerTag);
    }
    private bool IsEnemy(GameObject go) {
        if (enemyLayers.value != 0) return (enemyLayers.value & (1 << go.layer)) != 0;
        return go.CompareTag(enemyTag) || go.transform.root.CompareTag(enemyTag);
    }

    // --- Прилипание ---
    private void TryStick(Collider other)
    {
        var root = GetRootTransform(other);
        if (root == null) return;
        if (stickOnlyPlayers && !IsPlayer(root.gameObject)) return;
        
        if (_stickOverlap.TryGetValue(root, out int cnt)) _stickOverlap[root] = cnt + 1;
        else _stickOverlap[root] = 1;

        if (_oldParents.ContainsKey(root)) return;
        if (!IsFromTop(root, other)) return;

        var rb = root.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) return;

        _oldParents[root] = root.parent;
        root.SetParent(transform, true);
    }

    private void TryUnstick(Collider other)
    {
        var root = GetRootTransform(other);
        if (root == null) return;

        if (_stickOverlap.TryGetValue(root, out int cnt)) {
            cnt -= 1;
            if (cnt <= 0) _stickOverlap.Remove(root);
            else _stickOverlap[root] = cnt;
        } else return;

        if (_stickOverlap.ContainsKey(root)) return;

        if (_oldParents.TryGetValue(root, out var parent)) {
            root.SetParent(parent, true);
            _oldParents.Remove(root);
        }
    }

    private Transform GetRootTransform(Collider c) {
        if (c.attachedRigidbody != null) return c.attachedRigidbody.transform;
        return c.transform.root;
    }
    private bool IsFromTop(Transform targetRoot, Collider other) {
        if (damageTrigger == null) return false;
        Bounds platformB = damageTrigger.bounds;
        Bounds targetB = GetBounds(targetRoot);
        return targetB.min.y >= (platformB.max.y - topTolerance);
    }
    private Bounds GetBounds(Transform root) {
        var cols = root.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return new Bounds(root.position, Vector3.zero);
        var b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        return b;
    }
    private void SetupCollider() {
        if (damageTrigger == null) {
            var cols = GetComponents<Collider>();
            foreach (var c in cols) if (c != null && c.isTrigger) { damageTrigger = c; break; }
        }
        if (damageTrigger != null && !damageTrigger.isTrigger) damageTrigger.isTrigger = true;
    }
    private void SetupRigidbody() {
        if (autoAddKinematicRigidbody && _rb == null) {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true; _rb.useGravity = false;
        } else if (_rb != null) {
            if (!_rb.isKinematic) _rb.isKinematic = true;
            if (_rb.useGravity) _rb.useGravity = false;
        }
    }

    // --- STASIS ---
    public void SetStasis(bool active) => _frozen = active;
    public void ApplyStasis(float duration) { if (gameObject.activeInHierarchy) StartCoroutine(StasisRoutine(duration)); }
    private IEnumerator StasisRoutine(float duration) { SetStasis(true); yield return new WaitForSeconds(duration); SetStasis(false); }
    private void OnStasisStart() => SetStasis(true);
    private void OnStasisEnd() => SetStasis(false);
}