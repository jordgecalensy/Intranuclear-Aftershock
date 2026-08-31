using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;

[RequireComponent(typeof(SphereCollider))]
public sealed class FireAreaAdvanced : MonoBehaviour
{
    public enum Tier
    {
        Weak,
        Medium,
        Strong,
        Big
    }

    [Header("Targets")]
    public LayerMask targetMask = ~0;

    [Min(0.02f)]
    public float tickInterval = 0.25f;

    public int maxTargetsPerTick = 64;

    [Header("Geometry")]
    [Min(0.1f)]
    public float initialRadius = 2f;

    [Min(0.1f)]
    public float maxRadius = 10f;

    [Min(0f)]
    public float radiusGrowPerSec = 0.5f;

    [Header("Intensity")]
    public float intensity = 0.75f;

    [Min(0f)]
    public float intensityGrowPerSec = 0.2f;

    public float mediumThreshold = 1.0f;
    public float strongThreshold = 2.0f;
    public float peakIntensity = 3.0f;
    public float sustainIntensity = 1.2f;

    [Min(0f)]
    public float burnoutDecayPerSec = 0.5f;

    [Header("Extinguish")]
    public float extinguishAt = 0.15f;
    public bool destroyOnExtinguish = true;
    public float extinguishFadeTime = 0.6f;

    [Header("Contact DPS per tier")]
    [Min(0f)]
    public float weakContactDps = 5f;

    [Min(0f)]
    public float mediumContactDps = 12f;

    [Min(0f)]
    public float strongContactDps = 25f;

    [Header("Burn DoT")]
    [Min(0f)]
    public float dotDpsPerIntensity = 8f;

    [Min(0f)]
    public float mediumDotIntensity = 1.0f;

    [Min(0f)]
    public float strongDotIntensity = 2.0f;

    [Header("Effect Bundles")]
    [SerializeField] private EffectBundle _contactDamageEffects;
    [SerializeField] private EffectBundle _burnDotEffects;

    [Header("Spreading")]
    public bool enableSpreading = false;
    public FireAreaAdvanced firePrefab;

    [Min(0.25f)]
    public float spreadEvery = 2.0f;

    [Range(0f, 1f)]
    public float spreadChance = 0.35f;

    public int maxChildren = 5;
    public float spreadDistance = 3.0f;

    [Range(0.1f, 1.5f)]
    public float childIntensityFactor = 0.8f;

    [Range(0.1f, 1.5f)]
    public float childRadiusFactor = 0.7f;

    [Header("FX")]
    public ParticleSystem fxPrefab;

    [Min(0.01f)]
    public float fxSmoothTime = 0.25f;

    [Min(0.0f)]
    public float fxPrewarmTime = 0.35f;

    [Header("FX Mapping")]
    public AnimationCurve scaleCurve =
        AnimationCurve.Linear(0f, 0.8f, 1f, 1.4f);
    public AnimationCurve emissionCurve =
        AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f);

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new(1f, 0.4f, 0f, 0.25f);

    private IEffectApplicationService _pendingEffectService;
    private EffectApplicationServiceResolver _effectResolver;
    private FireAreaLifecycle _lifecycle;
    private FireAreaContactEffects _contactEffects;
    private FireAreaVisuals _visuals;
    private FireAreaChildFactory _childFactory;
    private SpatialPropagation _propagation;
    private bool _extinguishing;
    private bool _started;

    internal EffectBundle ContactEffects => _contactDamageEffects;
    internal EffectBundle BurnEffects => _burnDotEffects;
    internal IEffectApplicationService EffectService =>
        _effectResolver?.Service ?? _pendingEffectService;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _pendingEffectService = effects;
        _effectResolver?.Set(effects);
    }

    private void Awake()
    {
        _effectResolver = new EffectApplicationServiceResolver(
            gameObject,
            _pendingEffectService);
        _lifecycle = new FireAreaLifecycle();
        _contactEffects = new FireAreaContactEffects(gameObject);
        _visuals = new FireAreaVisuals(transform);
        _childFactory = new FireAreaChildFactory(this);
        _propagation = new SpatialPropagation(_childFactory.TryCreate);

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.01f;
    }

    private void Start()
    {
        _started = true;
        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (_started)
            InitializeRuntime();
    }

    private void OnDisable()
    {
        _contactEffects?.Clear();
        _visuals?.Dispose();
        _extinguishing = false;
    }

    private void Update()
    {
        if (_extinguishing)
        {
            if (_visuals.TickExtinguish(Time.deltaTime))
                CompleteExtinguishing();

            return;
        }

        _effectResolver.TryResolve(Time.unscaledTime, false);
        SynchronizeExternalIntensity();
        _lifecycle.Tick(
            Time.deltaTime,
            maxRadius,
            radiusGrowPerSec,
            intensityGrowPerSec,
            peakIntensity,
            sustainIntensity,
            burnoutDecayPerSec);
        intensity = _lifecycle.Intensity;

        if (intensity <= extinguishAt)
        {
            BeginExtinguishing();
            return;
        }

        Tier tier = _lifecycle.GetTier(
            mediumThreshold,
            strongThreshold,
            peakIntensity);
        TickContactEffects(tier);
        TickPropagation();
        _visuals.ApplyIntensity(
            intensity,
            peakIntensity,
            scaleCurve,
            emissionCurve,
            fxSmoothTime,
            false,
            Time.deltaTime);
    }

    public void AddExtinguishImpulse(float amount)
    {
        if (amount <= 0f)
            return;

        if (!_started)
        {
            intensity = Mathf.Max(0f, intensity - amount);
            return;
        }

        _lifecycle.AddExtinguishImpulse(amount);
        intensity = _lifecycle.Intensity;

        if (intensity < 0.5f)
        {
            maxRadius = Mathf.Max(
                initialRadius,
                maxRadius - amount * 0.5f);
        }

        if (intensity <= 0.01f)
            Destroy(gameObject);
    }

    internal void SetEffectBundles(
        EffectBundle contactEffects,
        EffectBundle burnEffects)
    {
        _contactDamageEffects = contactEffects;
        _burnDotEffects = burnEffects;
    }

    internal void RefreshRuntimeConfiguration()
    {
        if (_started && isActiveAndEnabled)
            InitializeRuntime();
    }

    private void InitializeRuntime()
    {
        _effectResolver.TryResolve(Time.unscaledTime, true);
        _lifecycle.Initialize(initialRadius, maxRadius, intensity);
        intensity = _lifecycle.Intensity;
        _contactEffects.Initialize(Time.time, tickInterval);
        _propagation.Initialize(Time.time, spreadEvery);
        _visuals.Initialize(
            fxPrefab,
            fxPrewarmTime,
            intensity,
            peakIntensity,
            scaleCurve,
            emissionCurve);
        _extinguishing = false;
    }

    private void SynchronizeExternalIntensity()
    {
        if (!Mathf.Approximately(intensity, _lifecycle.Intensity))
            _lifecycle.SetIntensity(intensity);
    }

    private void TickContactEffects(Tier tier)
    {
        _contactEffects.Tick(
            Time.time,
            transform.position,
            _lifecycle.Radius,
            targetMask,
            maxTargetsPerTick,
            tickInterval,
            tier,
            weakContactDps,
            mediumContactDps,
            strongContactDps,
            dotDpsPerIntensity,
            mediumDotIntensity,
            strongDotIntensity,
            EffectService,
            _contactDamageEffects,
            _burnDotEffects);
    }

    private void TickPropagation()
    {
        _propagation.Tick(
            Time.time,
            enableSpreading && firePrefab != null,
            spreadEvery,
            spreadChance,
            maxChildren,
            transform.position,
            transform.forward,
            _lifecycle.Radius,
            spreadDistance,
            ~0,
            5f,
            15f);
    }

    private void BeginExtinguishing()
    {
        _extinguishing = true;

        if (_visuals.BeginExtinguish(extinguishFadeTime))
            CompleteExtinguishing();
    }

    private void CompleteExtinguishing()
    {
        if (destroyOnExtinguish)
        {
            Destroy(gameObject);
            return;
        }

        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        float radius = Application.isPlaying && _started
            ? _lifecycle.Radius
            : initialRadius;

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(
            gizmoColor.r,
            gizmoColor.g,
            gizmoColor.b,
            0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
