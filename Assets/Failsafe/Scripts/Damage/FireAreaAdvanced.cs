using System;
using System.Collections;
using System.Collections.Generic;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
    public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0.8f, 1, 1.4f);
    public AnimationCurve emissionCurve = AnimationCurve.Linear(0, 0.6f, 1, 1.6f);

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(1f, 0.4f, 0f, 0.25f);

    private float _radius;
    private float _nextTickAt;
    private bool _burningOut;
    private float _nextSpreadAt;
    private int _childrenSpawned;

    private readonly Collider[] _buffer = new Collider[256];
    private readonly HashSet<Transform> _seenTargets = new HashSet<Transform>(128);

    private ParticleSystem _fxInstance;
    private float _scaleVelocity;
    private float _emissionVelocity;
    private float _currentScale = 1f;
    private float _currentEmission = 1f;

    private IEffectApplicationService _effects;

    [Inject]
    public void Construct(IEffectApplicationService effects)
    {
        _effects = effects;
    }

    private void OnEnable()
    {
        ResolveEffectsIfNeeded();

        _radius = Mathf.Clamp(initialRadius, 0.1f, maxRadius);
        _nextTickAt = Time.time + tickInterval;
        _nextSpreadAt = Time.time + spreadEvery;
        _burningOut = false;

        var sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.01f;

        if (fxPrefab != null && _fxInstance == null)
        {
            _fxInstance = Instantiate(fxPrefab, transform);
            _fxInstance.name = fxPrefab.name + "(Runtime)";
            _fxInstance.transform.localPosition = Vector3.zero;
            _fxInstance.transform.localRotation = Quaternion.identity;

            var main = _fxInstance.main;
            main.prewarm = true;

            if (fxPrewarmTime > 0f)
            {
                _fxInstance.Simulate(
                    fxPrewarmTime,
                    withChildren: true,
                    restart: true,
                    fixedTimeStep: false);
            }

            _fxInstance.Play(true);
            ApplyFxContinuous(intensity, immediate: true);
        }
    }

    private void OnDisable()
    {
        if (_fxInstance == null)
            return;

        _fxInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(_fxInstance.gameObject);
        _fxInstance = null;
    }

    private void Update()
    {
        ResolveEffectsIfNeeded();

        float deltaTime = Time.deltaTime;

        if (_radius < maxRadius)
            _radius = Mathf.Min(maxRadius, _radius + radiusGrowPerSec * deltaTime);

        if (!_burningOut)
        {
            if (intensity < peakIntensity)
            {
                intensity = Mathf.Min(peakIntensity, intensity + intensityGrowPerSec * deltaTime);

                if (Mathf.Approximately(intensity, peakIntensity))
                    _burningOut = true;
            }
            else
            {
                _burningOut = true;
            }
        }
        else
        {
            if (intensity > sustainIntensity)
                intensity = Mathf.Max(sustainIntensity, intensity - burnoutDecayPerSec * deltaTime);
        }

        if (intensity <= extinguishAt)
        {
            StartCoroutine(ExtinguishAndMaybeDestroy());
            return;
        }

        float now = Time.time;

        if (now >= _nextTickAt)
        {
            _nextTickAt = now + tickInterval;
            DoContactTick();
        }

        if (enableSpreading && firePrefab != null && _childrenSpawned < maxChildren && now >= _nextSpreadAt)
        {
            _nextSpreadAt = now + spreadEvery;
            TrySpread();
        }

        ApplyFxContinuous(intensity, immediate: false);
    }

    private void DoContactTick()
    {
        if (_effects == null)
            return;

        _seenTargets.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            _radius,
            _buffer,
            targetMask,
            QueryTriggerInteraction.Collide);

        Tier tier = GetTier(intensity);

        float dps = tier switch
        {
            Tier.Weak => weakContactDps,
            Tier.Medium => mediumContactDps,
            _ => strongContactDps
        };

        int processed = 0;

        for (int i = 0; i < count && processed < maxTargetsPerTick; i++)
        {
            Collider targetCollider = _buffer[i];

            if (targetCollider == null)
                continue;

            Transform targetTransform = targetCollider.attachedRigidbody != null
                ? targetCollider.attachedRigidbody.transform
                : targetCollider.transform;

            if (targetTransform == null)
                continue;

            if (!_seenTargets.Add(targetTransform))
                continue;

            processed++;

            if (dps > 0f && _contactDamageEffects != null)
            {
                float contactAmount = dps * tickInterval;

                var contactContext = new EffectContext(
                    gameObject,
                    targetCollider,
                    targetCollider.ClosestPoint(transform.position),
                    Vector3.up,
                    (targetCollider.transform.position - transform.position).normalized,
                    contactAmount);

                _effects.Apply(_contactDamageEffects, contactContext);
            }

            if (tier != Tier.Weak && _burnDotEffects != null)
            {
                float dotIntensity = tier == Tier.Medium
                    ? mediumDotIntensity
                    : strongDotIntensity;

                float dotAmountPerTick = dotDpsPerIntensity * dotIntensity;

                var burnContext = new EffectContext(
                    gameObject,
                    targetCollider,
                    targetCollider.ClosestPoint(transform.position),
                    Vector3.up,
                    (targetCollider.transform.position - transform.position).normalized,
                    dotAmountPerTick);

                _effects.Apply(_burnDotEffects, burnContext);
            }
        }
    }

    private Tier GetTier(float value)
    {
        if (value < mediumThreshold)
            return Tier.Weak;

        if (value < strongThreshold)
            return Tier.Medium;

        if (value < Mathf.Max(strongThreshold + 0.0001f, peakIntensity * 0.95f))
            return Tier.Strong;

        return Tier.Big;
    }

    private void ApplyFxContinuous(float value, bool immediate)
    {
        if (_fxInstance == null)
            return;

        float normalized = Mathf.InverseLerp(
            0f,
            Mathf.Max(0.001f, peakIntensity),
            Mathf.Max(0f, value));

        float targetScale = scaleCurve.Evaluate(normalized);
        float targetEmission = emissionCurve.Evaluate(normalized);

        if (immediate)
        {
            _currentScale = targetScale;
            _currentEmission = targetEmission;
        }
        else
        {
            _currentScale = Mathf.SmoothDamp(
                _currentScale,
                targetScale,
                ref _scaleVelocity,
                fxSmoothTime);

            _currentEmission = Mathf.SmoothDamp(
                _currentEmission,
                targetEmission,
                ref _emissionVelocity,
                fxSmoothTime);
        }

        _fxInstance.transform.localScale = Vector3.one * _currentScale;
        SetEmissionMultiplier(_fxInstance, _currentEmission);
    }

    private void SetEmissionMultiplier(ParticleSystem root, float multiplier)
    {
        if (root == null)
            return;

        var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var particleSystem in particleSystems)
        {
            var emission = particleSystem.emission;
            var rate = emission.rateOverTime;
            rate.curveMultiplier = multiplier;
            emission.rateOverTime = rate;
        }
    }

    private IEnumerator ExtinguishAndMaybeDestroy()
    {
        enabled = false;

        if (_fxInstance != null)
        {
            float duration = Mathf.Max(0.05f, extinguishFadeTime);
            float time = 0f;

            var particleSystems = _fxInstance.GetComponentsInChildren<ParticleSystem>(true);
            var initialEmission = new float[particleSystems.Length];

            for (int i = 0; i < particleSystems.Length; i++)
                initialEmission[i] = particleSystems[i].emission.rateOverTime.curveMultiplier;

            while (time < duration)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(time / duration);

                for (int i = 0; i < particleSystems.Length; i++)
                {
                    var particleSystem = particleSystems[i];
                    var emission = particleSystem.emission;
                    var rate = emission.rateOverTime;
                    rate.curveMultiplier = initialEmission[i] * k;
                    emission.rateOverTime = rate;
                }

                _fxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, _currentScale, k);

                yield return null;
            }

            foreach (var particleSystem in particleSystems)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (destroyOnExtinguish)
            Destroy(gameObject);
    }

    public void AddExtinguishImpulse(float amount)
    {
        if (amount <= 0f)
            return;

        intensity = Mathf.Max(0f, intensity - amount);
        _burningOut = true;
    }

    private void TrySpread()
    {
        if (UnityEngine.Random.value > spreadChance)
            return;

        Vector3 direction = UnityEngine.Random.insideUnitSphere;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.forward;

        direction.Normalize();

        Vector3 spawnPosition = transform.position + direction * (_radius + spreadDistance);

        if (Physics.Raycast(
                spawnPosition + Vector3.up * 5f,
                Vector3.down,
                out RaycastHit hit,
                15f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            spawnPosition = hit.point;
        }

        var child = Instantiate(firePrefab, spawnPosition, Quaternion.identity);

        child.initialRadius = Mathf.Max(0.4f, initialRadius * childRadiusFactor);
        child.maxRadius = Mathf.Max(child.initialRadius, maxRadius * childRadiusFactor);
        child.radiusGrowPerSec = radiusGrowPerSec * 0.9f;

        child.intensity = Mathf.Max(0.1f, intensity * childIntensityFactor);
        child.intensityGrowPerSec = intensityGrowPerSec * 0.9f;
        child.mediumThreshold = mediumThreshold;
        child.strongThreshold = strongThreshold;
        child.peakIntensity = Mathf.Max(child.intensity + 0.1f, peakIntensity * 0.9f);
        child.sustainIntensity = Mathf.Min(child.strongThreshold - 0.01f, sustainIntensity);
        child.burnoutDecayPerSec = burnoutDecayPerSec;

        child.targetMask = targetMask;
        child.tickInterval = tickInterval;
        child.maxTargetsPerTick = Mathf.Max(8, (int)(maxTargetsPerTick * 0.7f));

        child.enableSpreading = enableSpreading;
        child.firePrefab = firePrefab;
        child.spreadEvery = spreadEvery * UnityEngine.Random.Range(0.9f, 1.2f);
        child.spreadChance = spreadChance * 0.9f;
        child.maxChildren = Math.Max(0, maxChildren - 1);
        child.spreadDistance = spreadDistance;

        child.childIntensityFactor = childIntensityFactor;
        child.childRadiusFactor = childRadiusFactor;

        child.dotDpsPerIntensity = dotDpsPerIntensity;
        child.mediumDotIntensity = Mathf.Max(0.1f, mediumDotIntensity * 0.9f);
        child.strongDotIntensity = Mathf.Max(0.1f, strongDotIntensity * 0.9f);

        child.fxPrefab = fxPrefab;
        child.fxSmoothTime = fxSmoothTime;
        child.fxPrewarmTime = fxPrewarmTime;
        child.scaleCurve = scaleCurve;
        child.emissionCurve = emissionCurve;

        child._contactDamageEffects = _contactDamageEffects;
        child._burnDotEffects = _burnDotEffects;

        _childrenSpawned++;
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

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        float radius = Application.isPlaying
            ? _radius
            : initialRadius;

        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, radius);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}