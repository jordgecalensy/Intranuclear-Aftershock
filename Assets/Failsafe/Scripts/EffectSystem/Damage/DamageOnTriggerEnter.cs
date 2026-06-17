using System.Collections;
using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

[RequireComponent(typeof(Rigidbody))]
public class DamageObstacle : MonoBehaviour
{
    [Header("1. Вращение")]
    [SerializeField] private bool rotate = false;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("2. Цикличность")]
    [SerializeField] private bool useCycle = false;
    [SerializeField] private float activeTime = 2f;
    [SerializeField] private float inactiveTime = 2f;
    [SerializeField] private GameObject visualModel;

    [Header("3. Движение по путям")]
    [SerializeField] private bool movable = true;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitAtWaypoint = 0f;
    [SerializeField] private List<Transform> waypoints;

    [Header("Настройки применения эффектов")]
    [SerializeField] private bool canDealDamage = true;
    [SerializeField] private bool damagePlayers = true;
    [SerializeField] private bool damageEnemies = false;
    [SerializeField] private LayerMask playerLayers = 0;
    [SerializeField] private LayerMask enemyLayers = 0;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Tooltip("Bundle, который применяется к цели при контакте. Для обычного урона добавь InstantDamageEffectDefinition.")]
    [SerializeField] private EffectBundle contactEffects;

    [Tooltip("Передаётся в EffectContext.Power. Для урона через Power настрой InstantDamageEffectDefinition: Amount = 1, Scale By Context Power = true.")]
    [SerializeField] private float damageAmount = 10f;

    [SerializeField] private float damageInterval = 1f;

    [Header("Триггер & Физика")]
    [SerializeField] private Collider damageTrigger;
    [SerializeField] private bool autoAddKinematicRigidbody = true;

    [Header("Прилипание игрока")]
    [SerializeField] private bool stickPlayerOnTop = true;
    [SerializeField] private float topTolerance = 0.15f;
    [SerializeField] private bool stickOnlyPlayers = true;

    [Header("Stasis")]
    [SerializeField] private bool freezeByStasis = true;
    [SerializeField] private bool freezeAlsoDamage = true;

    private int _currentWaypointIndex = 0;
    private float _waitTimer = 0f;
    private bool _isWaiting = false;
    private Rigidbody _rb;

    private float _cycleTimer;
    private bool _isCycleActive = true;
    private bool _frozen;

    private readonly Dictionary<Transform, int> _overlapCount = new();
    private readonly Dictionary<Transform, float> _timers = new();
    private readonly Dictionary<Transform, Collider> _targetColliders = new();
    private static readonly List<Transform> _tmp = new();

    private readonly Dictionary<Transform, int> _stickOverlap = new();
    private readonly Dictionary<Transform, Transform> _oldParents = new();

    private IEffectApplicationService _effects;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _effects = effects;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        SetupCollider();
        SetupRigidbody();
    }

    private void Start()
    {
        _cycleTimer = activeTime;
        ResolveEffectsIfNeeded();

        if (useCycle && visualModel == null)
        {
            MeshRenderer mesh = GetComponent<MeshRenderer>();

            if (mesh != null)
                visualModel = gameObject;
        }
    }

    private void Update()
    {
        ResolveEffectsIfNeeded();

        if (!_frozen)
            HandleCycle(Time.deltaTime);

        TickDamage(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (_frozen)
            return;

        if (useCycle && !_isCycleActive)
            return;

        HandleRotationFixed(Time.fixedDeltaTime);
        HandleMovementFixed(Time.fixedDeltaTime);

        if (_rb != null && !_rb.isKinematic)
            _rb.isKinematic = true;
    }

    private void HandleRotationFixed(float deltaTime)
    {
        if (!rotate)
            return;

        if (_rb == null)
            return;

        Quaternion deltaRotation = Quaternion.Euler(rotationAxis * rotationSpeed * deltaTime);
        _rb.MoveRotation(_rb.rotation * deltaRotation);
    }

    private void HandleMovementFixed(float deltaTime)
    {
        if (!movable)
            return;

        if (waypoints == null || waypoints.Count == 0)
            return;

        Vector3 targetPosition = waypoints[_currentWaypointIndex].position;

        if (Vector3.Distance(transform.position, targetPosition) < 0.02f)
        {
            if (!_isWaiting)
            {
                _isWaiting = true;
                _waitTimer = waitAtWaypoint;
            }
            else
            {
                _waitTimer -= deltaTime;

                if (_waitTimer <= 0f)
                {
                    _isWaiting = false;
                    NextWaypoint();
                }
            }
        }
        else
        {
            Vector3 nextPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * deltaTime);

            _rb.MovePosition(nextPosition);
        }
    }

    public void SetRotationActive(bool active)
    {
        rotate = active;
    }

    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    private void NextWaypoint()
    {
        _currentWaypointIndex++;

        if (_currentWaypointIndex >= waypoints.Count)
            _currentWaypointIndex = 0;
    }

    private void HandleCycle(float deltaTime)
    {
        if (!useCycle)
            return;

        _cycleTimer -= deltaTime;

        if (_cycleTimer <= 0f)
            ToggleCycleState(!_isCycleActive);
    }

    private void ToggleCycleState(bool isActive)
    {
        _isCycleActive = isActive;
        _cycleTimer = isActive ? activeTime : inactiveTime;

        if (damageTrigger != null)
            damageTrigger.enabled = isActive;

        if (visualModel != null)
        {
            visualModel.SetActive(isActive);
        }
        else
        {
            Renderer renderer = GetComponent<Renderer>();

            if (renderer != null)
                renderer.enabled = isActive;
        }

        if (!isActive)
        {
            _timers.Clear();
            _overlapCount.Clear();
            _targetColliders.Clear();
        }
    }

    private void TickDamage(float deltaTime)
    {
        if (!canDealDamage)
            return;

        if (contactEffects == null)
            return;

        if (_effects == null)
            return;

        if (_frozen && freezeAlsoDamage)
            return;

        if (useCycle && !_isCycleActive)
            return;

        if (_timers.Count == 0)
            return;

        float interval = Mathf.Max(0.01f, damageInterval);

        _tmp.Clear();

        foreach (KeyValuePair<Transform, float> pair in _timers)
            _tmp.Add(pair.Key);

        foreach (Transform targetTransform in _tmp)
        {
            if (targetTransform == null)
            {
                RemoveTarget(targetTransform);
                continue;
            }

            Collider targetCollider = GetValidTargetCollider(targetTransform);

            if (targetCollider == null)
            {
                RemoveTarget(targetTransform);
                continue;
            }

            float timer = _timers[targetTransform] - deltaTime;

            if (timer <= 0f)
            {
                ApplyContactEffects(targetTransform, targetCollider);
                timer = interval;
            }

            _timers[targetTransform] = timer;
        }
    }

    private void ApplyContactEffects(Transform targetTransform, Collider targetCollider)
    {
        Vector3 direction = targetTransform.position - transform.position;

        if (direction.sqrMagnitude < 0.0001f)
            direction = targetCollider.bounds.center - transform.position;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        direction.Normalize();

        Vector3 point = targetCollider.ClosestPoint(transform.position);

        var context = new EffectContext(
            gameObject,
            targetCollider,
            point,
            Vector3.up,
            direction,
            damageAmount);

        _effects.Apply(contactEffects, context);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stickPlayerOnTop)
            TryStick(other);

        if (!canDealDamage || (useCycle && !_isCycleActive))
            return;

        if (other == null)
            return;

        Transform targetTransform = GetRootTransform(other);

        if (targetTransform == null)
            return;

        if (!IsAllowedTarget(targetTransform.gameObject))
            return;

        if (_overlapCount.TryGetValue(targetTransform, out int count))
            _overlapCount[targetTransform] = count + 1;
        else
            _overlapCount[targetTransform] = 1;

        _targetColliders[targetTransform] = other;

        if (!_timers.ContainsKey(targetTransform))
            _timers[targetTransform] = 0f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (stickPlayerOnTop)
            TryUnstick(other);

        if (!canDealDamage)
            return;

        Transform targetTransform = GetRootTransform(other);

        if (targetTransform == null)
            return;

        if (!_overlapCount.TryGetValue(targetTransform, out int count))
            return;

        count--;

        if (count <= 0)
        {
            RemoveTarget(targetTransform);
        }
        else
        {
            _overlapCount[targetTransform] = count;

            if (_targetColliders.TryGetValue(targetTransform, out Collider storedCollider) && storedCollider == other)
                _targetColliders[targetTransform] = FindFirstEnabledCollider(targetTransform);
        }
    }

    private void RemoveTarget(Transform targetTransform)
    {
        _overlapCount.Remove(targetTransform);
        _timers.Remove(targetTransform);
        _targetColliders.Remove(targetTransform);
    }

    private Collider GetValidTargetCollider(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        if (_targetColliders.TryGetValue(targetTransform, out Collider collider))
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                return collider;
        }

        collider = FindFirstEnabledCollider(targetTransform);

        if (collider != null)
            _targetColliders[targetTransform] = collider;

        return collider;
    }

    private Collider FindFirstEnabledCollider(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        Collider[] colliders = targetTransform.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                return collider;
        }

        return null;
    }

    private bool IsAllowedTarget(GameObject target)
    {
        if (damagePlayers && IsPlayer(target))
            return true;

        if (damageEnemies && IsEnemy(target))
            return true;

        return false;
    }

    private bool IsPlayer(GameObject target)
    {
        if (target == null)
            return false;

        if (playerLayers.value != 0)
            return (playerLayers.value & (1 << target.layer)) != 0;

        return target.CompareTag(playerTag) || target.transform.root.CompareTag(playerTag);
    }

    private bool IsEnemy(GameObject target)
    {
        if (target == null)
            return false;

        if (enemyLayers.value != 0)
            return (enemyLayers.value & (1 << target.layer)) != 0;

        return target.CompareTag(enemyTag) || target.transform.root.CompareTag(enemyTag);
    }

    private void TryStick(Collider other)
    {
        Transform root = GetRootTransform(other);

        if (root == null)
            return;

        if (stickOnlyPlayers && !IsPlayer(root.gameObject))
            return;

        if (_stickOverlap.TryGetValue(root, out int count))
            _stickOverlap[root] = count + 1;
        else
            _stickOverlap[root] = 1;

        if (_oldParents.ContainsKey(root))
            return;

        if (!IsFromTop(root))
            return;

        Rigidbody targetRigidbody = root.GetComponent<Rigidbody>();

        if (targetRigidbody != null && !targetRigidbody.isKinematic)
            return;

        _oldParents[root] = root.parent;
        root.SetParent(transform, true);
    }

    private void TryUnstick(Collider other)
    {
        Transform root = GetRootTransform(other);

        if (root == null)
            return;

        if (_stickOverlap.TryGetValue(root, out int count))
        {
            count--;

            if (count <= 0)
                _stickOverlap.Remove(root);
            else
                _stickOverlap[root] = count;
        }
        else
        {
            return;
        }

        if (_stickOverlap.ContainsKey(root))
            return;

        if (_oldParents.TryGetValue(root, out Transform parent))
        {
            root.SetParent(parent, true);
            _oldParents.Remove(root);
        }
    }

    private Transform GetRootTransform(Collider collider)
    {
        if (collider == null)
            return null;

        if (collider.attachedRigidbody != null)
            return collider.attachedRigidbody.transform;

        return collider.transform.root;
    }

    private bool IsFromTop(Transform targetRoot)
    {
        if (damageTrigger == null)
            return false;

        Bounds platformBounds = damageTrigger.bounds;
        Bounds targetBounds = GetBounds(targetRoot);

        return targetBounds.min.y >= platformBounds.max.y - topTolerance;
    }

    private Bounds GetBounds(Transform root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>();

        if (colliders.Length == 0)
            return new Bounds(root.position, Vector3.zero);

        Bounds bounds = colliders[0].bounds;

        for (int i = 1; i < colliders.Length; i++)
            bounds.Encapsulate(colliders[i].bounds);

        return bounds;
    }

    private void SetupCollider()
    {
        if (damageTrigger == null)
        {
            Collider[] colliders = GetComponents<Collider>();

            foreach (Collider collider in colliders)
            {
                if (collider != null && collider.isTrigger)
                {
                    damageTrigger = collider;
                    break;
                }
            }
        }

        if (damageTrigger != null && !damageTrigger.isTrigger)
            damageTrigger.isTrigger = true;
    }

    private void SetupRigidbody()
    {
        if (autoAddKinematicRigidbody && _rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
        else if (_rb != null)
        {
            if (!_rb.isKinematic)
                _rb.isKinematic = true;

            if (_rb.useGravity)
                _rb.useGravity = false;
        }
    }

    private void ResolveEffectsIfNeeded()
    {
        if (_effects != null)
            return;

        LifetimeScope scope = GetComponentInParent<LifetimeScope>();

        if (scope == null)
            scope = LifetimeScope.Find<LifetimeScope>(gameObject.scene);

        if (scope == null || scope.Container == null)
            return;

        try
        {
            _effects = scope.Container.Resolve<IEffectApplicationService>();
        }
        catch
        {
            _effects = null;
        }
    }

    public void SetStasis(bool active)
    {
        if (!freezeByStasis)
            return;

        _frozen = active;
    }

    public void ApplyStasis(float duration)
    {
        if (!gameObject.activeInHierarchy)
            return;

        StartCoroutine(StasisRoutine(duration));
    }

    private IEnumerator StasisRoutine(float duration)
    {
        SetStasis(true);
        yield return new WaitForSeconds(duration);
        SetStasis(false);
    }

    private void OnStasisStart()
    {
        SetStasis(true);
    }

    private void OnStasisEnd()
    {
        SetStasis(false);
    }
}