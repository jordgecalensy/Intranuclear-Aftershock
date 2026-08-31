using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

[RequireComponent(typeof(Rigidbody))]
public class Obstacle : MonoBehaviour
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
    [FormerlySerializedAs("canDealDamage")]
    [SerializeField] private bool canApplyContactEffects = true;

    [FormerlySerializedAs("damagePlayers")]
    [SerializeField] private bool affectPlayers = true;

    [FormerlySerializedAs("damageEnemies")]
    [SerializeField] private bool affectEnemies = false;

    [SerializeField] private LayerMask playerLayers = 0;
    [SerializeField] private LayerMask enemyLayers = 0;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Tooltip("Bundle, который применяется к цели при контакте.")]
    [SerializeField] private EffectBundle contactEffects;

    [FormerlySerializedAs("damageAmount")]
    [Tooltip("Передаётся в EffectContext.Power.")]
    [SerializeField] private float effectPower = 10f;

    [FormerlySerializedAs("damageInterval")]
    [SerializeField] private float applicationInterval = 1f;

    [Header("Триггер & Физика")]
    [FormerlySerializedAs("damageTrigger")]
    [SerializeField] private Collider contactTrigger;
    [SerializeField] private bool autoAddKinematicRigidbody = true;

    [Header("Прилипание игрока")]
    [SerializeField] private bool stickPlayerOnTop = true;
    [SerializeField] private float topTolerance = 0.15f;
    [SerializeField] private bool stickOnlyPlayers = true;

    [Header("Stasis")]
    [SerializeField] private bool freezeByStasis = true;

    [FormerlySerializedAs("freezeAlsoDamage")]
    [SerializeField] private bool freezeContactEffects = true;

    private Rigidbody _rigidbody;
    private IEffectApplicationService _pendingEffectService;
    private ObstacleContactEffects _contactEffectTracker;
    private ObstacleMotion _motion;
    private ObstacleActivityCycle _activityCycle;
    private ObstaclePassengerAttachment _passengerAttachment;
    private ObstacleTargetFilter _targetFilter;
    private EffectApplicationServiceResolver _effectResolver;
    private ObstacleStasis _stasis;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _pendingEffectService = effects;
        _effectResolver?.Set(effects);
    }

    private void Awake()
    {
        contactTrigger = ObstaclePhysicsSetup.PrepareContactTrigger(
            gameObject,
            contactTrigger);
        _rigidbody = ObstaclePhysicsSetup.PrepareRigidbody(
            gameObject,
            autoAddKinematicRigidbody);

        _targetFilter = new ObstacleTargetFilter(
            affectPlayers,
            affectEnemies,
            playerLayers,
            enemyLayers,
            playerTag,
            enemyTag);
        _effectResolver = new EffectApplicationServiceResolver(
            gameObject,
            _pendingEffectService);
        _stasis = new ObstacleStasis();

        _contactEffectTracker = new ObstacleContactEffects(
            gameObject,
            _targetFilter.IsAllowed);
        _motion = new ObstacleMotion(_rigidbody, transform);
        _activityCycle = new ObstacleActivityCycle(
            gameObject,
            contactTrigger,
            visualModel);
        _passengerAttachment = new ObstaclePassengerAttachment(
            transform,
            contactTrigger,
            _targetFilter.IsPlayer);
    }

    private void Start()
    {
        _activityCycle.Initialize(activeTime);
        _effectResolver.TryResolve(Time.unscaledTime, true);
    }

    private void Update()
    {
        _stasis.Tick(Time.time);
        _effectResolver.TryResolve(Time.unscaledTime, false);

        if (!_stasis.IsFrozen &&
            _activityCycle.Tick(
                Time.deltaTime,
                useCycle,
                activeTime,
                inactiveTime))
        {
            _contactEffectTracker.Clear();
        }

        TickContactEffects(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (_stasis.IsFrozen)
            return;

        if (useCycle && !_activityCycle.IsActive)
            return;

        _motion.Tick(
            Time.fixedDeltaTime,
            rotate,
            rotationAxis,
            rotationSpeed,
            movable,
            moveSpeed,
            waitAtWaypoint,
            waypoints);

        if (_rigidbody != null && !_rigidbody.isKinematic)
            _rigidbody.isKinematic = true;
    }

    private void OnDisable()
    {
        _contactEffectTracker?.Clear();
        _passengerAttachment?.Clear();
        _stasis?.Clear();
    }

    public void SetRotationActive(bool active)
    {
        rotate = active;
    }

    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    private void TickContactEffects(float deltaTime)
    {
        if (!canApplyContactEffects)
            return;

        IEffectApplicationService effects = _effectResolver.Service;

        if (contactEffects == null || effects == null)
            return;

        if (_stasis.IsFrozen && freezeContactEffects)
            return;

        if (useCycle && !_activityCycle.IsActive)
            return;

        _contactEffectTracker.Tick(
            deltaTime,
            effects,
            contactEffects,
            effectPower,
            applicationInterval);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (stickPlayerOnTop)
        {
            _passengerAttachment.Enter(
                other,
                stickOnlyPlayers,
                topTolerance);
        }

        if (!canApplyContactEffects ||
            (useCycle && !_activityCycle.IsActive))
        {
            return;
        }

        _contactEffectTracker.Enter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (stickPlayerOnTop)
            _passengerAttachment.Exit(other);

        _contactEffectTracker.Exit(other);
    }

    public void SetStasis(bool active)
    {
        if (!freezeByStasis)
            return;

        _stasis?.Set(active);
    }

    public void ApplyStasis(float duration)
    {
        if (!freezeByStasis || !gameObject.activeInHierarchy)
            return;

        _stasis?.Apply(duration, Time.time);
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
