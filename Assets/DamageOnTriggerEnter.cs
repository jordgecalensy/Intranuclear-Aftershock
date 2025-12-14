using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Failsafe.Scripts.Damage.Implementation;

public class DamageOnTouchPlatform : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private bool movable = true;        // галочка: объект движется
    [SerializeField] private Transform targetPoint;      // конечная точка
    [SerializeField] private float moveSpeed = 3f;       // скорость
    [SerializeField] private bool moveInFixedUpdate = true;

    [Header("Урон")]
    [SerializeField] private bool canDealDamage = true;  // галочка: наносит урон
    [SerializeField] private bool damagePlayers = true;  // галочка: дамажить игрока
    [SerializeField] private bool damageEnemies = false; // галочка: дамажить врагов
    [SerializeField] private LayerMask playerLayers = 0; // если 0 -> фильтрация по тегу
    [SerializeField] private LayerMask enemyLayers = 0;  // если 0 -> фильтрация по тегу
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [SerializeField] private float damageAmount = 10f;   // урон за тик
    [SerializeField] private float damageInterval = 1f;  // интервал тиков

    [Header("Триггер")]
    [SerializeField] private Collider damageTrigger;     // IsTrigger=true
    [SerializeField] private bool autoAddKinematicRigidbody = true; // чтобы триггер гарантированно работал

    [Header("Чтобы игрок не соскальзывал")]
    [SerializeField] private bool stickPlayerOnTop = true; // галочка
    [SerializeField] private float topTolerance = 0.15f;   // допуск по высоте для "сверху"
    [SerializeField] private bool stickOnlyPlayers = true; // приклеивать только игрока

    [Header("Stasis")]
    [SerializeField] private bool freezeByStasis = true;   // галочка
    [SerializeField] private bool freezeAlsoDamage = true; // заморозка стопает урон

    private Vector3 _startPoint;
    private Vector3 _currentTarget;

    private Rigidbody _rb;
    private bool _frozen;

    // несколько целей одновременно + несколько коллайдеров у одной цели
    private readonly Dictionary<DamageableComponent, int> _overlapCount = new();
    private readonly Dictionary<DamageableComponent, float> _timers = new();
    private static readonly List<DamageableComponent> _tmp = new();

    // приклеивание: считаем пересечения по root, чтобы корректно отклеивать
    private readonly Dictionary<Transform, int> _stickOverlap = new();
    private readonly Dictionary<Transform, Transform> _oldParents = new();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (damageTrigger == null)
        {
            // ищем любой trigger-коллайдер на объекте
            var cols = GetComponents<Collider>();
            foreach (var c in cols)
            {
                if (c != null && c.isTrigger)
                {
                    damageTrigger = c;
                    break;
                }
            }
        }

        if (damageTrigger != null && !damageTrigger.isTrigger)
            damageTrigger.isTrigger = true;

        // чтобы OnTriggerEnter стабильно работал: у одного из объектов должен быть Rigidbody
        if (autoAddKinematicRigidbody && _rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
        else if (_rb != null)
        {
            // для платформ/ловушек обычно лучше кинематик
            if (!_rb.isKinematic) _rb.isKinematic = true;
            if (_rb.useGravity) _rb.useGravity = false;
        }
    }

    private void Start()
    {
        _startPoint = transform.position;
        _currentTarget = (targetPoint != null) ? targetPoint.position : _startPoint;
    }

    private void Update()
    {
        if (!moveInFixedUpdate) TickMovement(Time.deltaTime);
        TickDamage(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (moveInFixedUpdate) TickMovement(Time.fixedDeltaTime);
        if(!this.GetComponent<Rigidbody>().isKinematic) this.GetComponent<Rigidbody>().isKinematic = true;
    }

    private void TickMovement(float dt)
    {
        if (!movable || _frozen) return;
        if (targetPoint == null) return;

        var next = Vector3.MoveTowards(transform.position, _currentTarget, moveSpeed * dt);

        if (_rb != null && _rb.isKinematic)
            _rb.MovePosition(next);
        else
            transform.position = next;

        if (Vector3.Distance(next, _currentTarget) < 0.01f)
        {
            _currentTarget = (Vector3.Distance(_currentTarget, _startPoint) < 0.01f)
                ? targetPoint.position
                : _startPoint;
        }
    }

    private void TickDamage(float dt)
    {
        if (!canDealDamage) return;
        if (_frozen && freezeAlsoDamage) return;
        if (_timers.Count == 0) return;

        float interval = Mathf.Max(0.01f, damageInterval);

        _tmp.Clear();
        foreach (var kv in _timers) _tmp.Add(kv.Key);

        foreach (var d in _tmp)
        {
            if (d == null)
            {
                _timers.Remove(d);
                _overlapCount.Remove(d);
                continue;
            }

            float t = _timers[d] - dt;
            if (t <= 0f)
            {
                d.TakeDamage(new FlatDamage(damageAmount));
                t = interval;
            }

            _timers[d] = t;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1) приклеивание (опционально)
        if (stickPlayerOnTop)
            TryStick(other);

        // 2) урон (опционально)
        if (!canDealDamage) return;

        var damageable = FindDamageable(other);
        if (damageable == null) return;

        if (!IsAllowedTarget(damageable.gameObject)) return;

        if (_overlapCount.TryGetValue(damageable, out int cnt))
            _overlapCount[damageable] = cnt + 1;
        else
            _overlapCount[damageable] = 1;

        if (!_timers.ContainsKey(damageable))
            _timers[damageable] = 0f; // ударить сразу
    }

    private void OnTriggerExit(Collider other)
    {
        // отклеивание
        if (stickPlayerOnTop)
            TryUnstick(other);

        if (!canDealDamage) return;

        var damageable = FindDamageable(other);
        if (damageable == null) return;

        if (_overlapCount.TryGetValue(damageable, out int cnt))
        {
            cnt -= 1;
            if (cnt <= 0)
            {
                _overlapCount.Remove(damageable);
                _timers.Remove(damageable);
            }
            else
            {
                _overlapCount[damageable] = cnt;
            }
        }
    }

    private DamageableComponent FindDamageable(Collider other)
    {
        // главное отличие от твоего старого варианта:
        // ищем Damageable вверх по иерархии (hitbox обычно child), плюс запасные варианты как в AttackState
        var d = other.GetComponentInParent<DamageableComponent>();
        if (d != null) return d;

        if (other.attachedRigidbody != null)
        {
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

    private bool IsPlayer(GameObject go)
    {
        if (playerLayers.value != 0)
            return (playerLayers.value & (1 << go.layer)) != 0;

        return go.CompareTag(playerTag) || go.transform.root.CompareTag(playerTag);
    }

    private bool IsEnemy(GameObject go)
    {
        if (enemyLayers.value != 0)
            return (enemyLayers.value & (1 << go.layer)) != 0;

        return go.CompareTag(enemyTag) || go.transform.root.CompareTag(enemyTag);
    }

    private void TryStick(Collider other)
    {
        var root = GetRootTransform(other);
        if (root == null) return;

        if (stickOnlyPlayers && !IsPlayer(root.gameObject))
            return;

        // считаем пересечения, чтобы при нескольких коллайдерах не отклеивать раньше времени
        if (_stickOverlap.TryGetValue(root, out int cnt))
            _stickOverlap[root] = cnt + 1;
        else
            _stickOverlap[root] = 1;

        // уже приклеен
        if (_oldParents.ContainsKey(root))
            return;

        // проверка "вошёл сверху"
        if (!IsFromTop(root, other))
            return;

        // не трогаем динамический rigidbody цели (чтобы не ломать физику)
        var rb = root.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            return;

        _oldParents[root] = root.parent;
        root.SetParent(transform, true);
    }

    private void TryUnstick(Collider other)
    {
        var root = GetRootTransform(other);
        if (root == null) return;

        if (_stickOverlap.TryGetValue(root, out int cnt))
        {
            cnt -= 1;
            if (cnt <= 0) _stickOverlap.Remove(root);
            else _stickOverlap[root] = cnt;
        }
        else return;

        // ещё есть коллайдеры внутри триггера — не отклеиваем
        if (_stickOverlap.ContainsKey(root))
            return;

        if (_oldParents.TryGetValue(root, out var parent))
        {
            root.SetParent(parent, true);
            _oldParents.Remove(root);
        }
    }

    private Transform GetRootTransform(Collider c)
    {
        if (c.attachedRigidbody != null) return c.attachedRigidbody.transform;
        return c.transform.root;
    }

    private bool IsFromTop(Transform targetRoot, Collider other)
    {
        if (damageTrigger == null) return false;

        Bounds platformB = damageTrigger.bounds;
        Bounds targetB = GetBounds(targetRoot);

        return targetB.min.y >= (platformB.max.y - topTolerance);
    }

    private Bounds GetBounds(Transform root)
    {
        var cols = root.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return new Bounds(root.position, Vector3.zero);

        var b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++)
            b.Encapsulate(cols[i].bounds);

        return b;
    }

    // ---- STASIS ----
    public void SetStasis(bool active)
    {
        if (!freezeByStasis) return;
        _frozen = active;
    }

    public void ApplyStasis(float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(StasisRoutine(duration));
    }

    private IEnumerator StasisRoutine(float duration)
    {
        SetStasis(true);
        yield return new WaitForSeconds(duration);
        SetStasis(false);
    }

    // если твоя система стасиса шлёт SendMessage("OnStasisStart/OnStasisEnd")
    private void OnStasisStart() => SetStasis(true);
    private void OnStasisEnd() => SetStasis(false);
}
