using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Failsafe.Scripts.Damage.Implementation;

[RequireComponent(typeof(SphereCollider))]
public sealed class FireAreaAdvanced : MonoBehaviour
{
    public enum Tier { Weak, Medium, Strong, Big } // боевые тиры для ДПС/DoT (визуально не используются)

    // -------------------- TARGETS / TICKS --------------------
    [Header("Targets")]
    public LayerMask targetMask = ~0;
    [Min(0.02f)] public float tickInterval = 0.25f;
    public int maxTargetsPerTick = 64;

    // -------------------- GEOMETRY --------------------
    [Header("Geometry (Sphere)")]
    [Min(0.1f)] public float initialRadius = 2f;
    [Min(0.1f)] public float maxRadius = 10f;
    [Min(0f)]   public float radiusGrowPerSec = 0.5f;

    // -------------------- INTENSITY MODEL --------------------
    [Header("Intensity")]
    [Tooltip("Текущая «сила» очага")]
    public float intensity = 0.75f;
    [Min(0f)] public float intensityGrowPerSec = 0.2f;
    public float mediumThreshold = 1.0f;
    public float strongThreshold = 2.0f;
    public float peakIntensity = 3.0f;      // максимум шкалы для маппинга FX
    public float sustainIntensity = 1.2f;   // уровень, до которого догораем
    [Min(0f)] public float burnoutDecayPerSec = 0.5f;

    // -------------------- EXTINGUISH --------------------
    [Header("Extinguish (угасание)")]
    [Tooltip("При падении ниже этого значения огонь считается потухшим")]
    public float extinguishAt = 0.15f;
    public bool destroyOnExtinguish = true;
    public float extinguishFadeTime = 0.6f; // плавное затухание FX

    // -------------------- COMBAT DAMAGE --------------------
    [Header("Contact DPS per tier")]
    [Min(0f)] public float weakContactDps   = 5f;
    [Min(0f)] public float mediumContactDps = 12f;
    [Min(0f)] public float strongContactDps = 25f;

    [Header("Burn DoT (no stacks)")]
    [Min(0.1f)] public float dotBaseDuration = 4f;
    [Min(0.05f)] public float dotTickInterval = 1f;
    [Min(0f)]    public float dotDpsPerIntensity = 8f;
    [Min(0f)]    public float mediumDotIntensity = 1.0f;
    [Min(0f)]    public float strongDotIntensity = 2.0f;

    // -------------------- SPREAD (опционально) --------------------
    [Header("Spreading (optional)")]
    public bool enableSpreading = false;
    public FireAreaAdvanced firePrefab;
    [Min(0.25f)] public float spreadEvery = 2.0f;
    [Range(0f,1f)] public float spreadChance = 0.35f;
    public int maxChildren = 5;
    public float spreadDistance = 3.0f;
    [Range(0.1f, 1.5f)] public float childIntensityFactor = 0.8f;
    [Range(0.1f, 1.5f)] public float childRadiusFactor = 0.7f;

    // -------------------- SINGLE-FX (бесшовно) --------------------
    [Header("FX (single prefab)")]
    public ParticleSystem fxPrefab;                    // один общий префаб огня
    [Min(0.01f)] public float fxSmoothTime = 0.25f;    // сглаживание переходов
    [Min(0.0f)]  public float fxPrewarmTime = 0.35f;   // «прогрев» при старте

    [Header("FX continuous mapping (0..1 от peakIntensity)")]
    public AnimationCurve scaleCurve    = AnimationCurve.Linear(0, 0.8f, 1, 1.4f); // intensity -> transform.localScale
    public AnimationCurve emissionCurve = AnimationCurve.Linear(0, 0.6f, 1, 1.6f); // intensity -> emission.rateOverTime.curveMultiplier

    // -------------------- DEBUG --------------------
    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(1f, 0.4f, 0f, 0.25f);

    // -------------------- RUNTIME --------------------
    float _radius;
    float _nextTickAt;
    bool  _burningOut;
    float _nextSpreadAt;
    int   _childrenSpawned;

    readonly Collider[] _buf = new Collider[256];
    readonly HashSet<Transform> _seen = new HashSet<Transform>(128);

    class DotState
    {
        public float Intensity;
        public float ExpiresAt;
        public float NextTickAt;
    }
    readonly Dictionary<DamageableComponent, DotState> _dots = new();
    static readonly List<DamageableComponent> _toRemove = new();

    // single FX instance + сглаживание
    ParticleSystem _fxInstance;
    float _scaleVel, _emisVel;
    float _currentScale = 1f, _currentEmission = 1f;

    // -------------------- LIFECYCLE --------------------
    void OnEnable()
    {
        _radius = Mathf.Clamp(initialRadius, 0.1f, maxRadius);
        _nextTickAt = Time.time + tickInterval;
        _nextSpreadAt = Time.time + spreadEvery;
        _burningOut = false;

        var sc = GetComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.01f; // служебный, геометрию считаем сами

        // инстанс единственного FX
        if (fxPrefab && !_fxInstance)
        {
            _fxInstance = Instantiate(fxPrefab, transform);
            _fxInstance.name = fxPrefab.name + "(Runtime)";
            _fxInstance.transform.localPosition = Vector3.zero;
            _fxInstance.transform.localRotation = Quaternion.identity;

            var main = _fxInstance.main;
            main.prewarm = true;
            if (fxPrewarmTime > 0f)
                _fxInstance.Simulate(fxPrewarmTime, withChildren: true, restart: true, fixedTimeStep: false);

            _fxInstance.Play(true);
            ApplyFxContinuous(intensity, immediate: true);
        }
    }

    void OnDisable()
    {
        if (_fxInstance)
        {
            _fxInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(_fxInstance.gameObject);
            _fxInstance = null;
        }
    }

    // -------------------- UPDATE --------------------
    void Update()
    {
        float dt = Time.deltaTime;

        // рост радиуса
        if (_radius < maxRadius)
            _radius = Mathf.Min(maxRadius, _radius + radiusGrowPerSec * dt);

        // рост/выгорание интенсивности
        if (!_burningOut)
        {
            if (intensity < peakIntensity)
            {
                intensity = Mathf.Min(peakIntensity, intensity + intensityGrowPerSec * dt);
                if (Mathf.Approximately(intensity, peakIntensity)) _burningOut = true;
            }
            else _burningOut = true;
        }
        else
        {
            if (intensity > sustainIntensity)
                intensity = Mathf.Max(sustainIntensity, intensity - burnoutDecayPerSec * dt);
        }

        // угасание
        if (intensity <= extinguishAt)
        {
            StartCoroutine(CoExtinguishAndMaybeDestroy());
            return; // после старта угасания логику не продолжаем
        }

        float now = Time.time;

        // контактные тики
        if (now >= _nextTickAt)
        {
            _nextTickAt = now + tickInterval;
            DoContactTick();
        }

        // тики DoT
        TickDots(now);

        // распространение
        if (enableSpreading && firePrefab && _childrenSpawned < maxChildren && now >= _nextSpreadAt)
        {
            _nextSpreadAt = now + spreadEvery;
            TrySpread();
        }

        // бесшовный визуал
        ApplyFxContinuous(intensity, immediate: false);
    }

    // -------------------- DAMAGE / DOT --------------------
    void DoContactTick()
    {
        _seen.Clear();
        int n = Physics.OverlapSphereNonAlloc(transform.position, _radius, _buf, targetMask, QueryTriggerInteraction.Collide);

        Tier tier = GetTier(intensity);
        float dps = tier switch
        {
            Tier.Weak   => weakContactDps,
            Tier.Medium => mediumContactDps,
            _           => strongContactDps
        };

        int processed = 0;
        for (int i = 0; i < n && processed < maxTargetsPerTick; i++)
        {
            var col = _buf[i];
            if (!col) continue;

            var tr = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            if (!tr || !_seen.Add(tr)) continue;
            processed++;

            var dmgComp = tr.GetComponentInChildren<DamageableComponent>();
            if (!dmgComp) continue;

            // контактный урон за тик
            if (dps > 0f)
            {
                float contactAmount = dps * tickInterval;
                dmgComp.TakeDamage(new FireContactDamage(contactAmount, this));
            }

            // DoT для Medium/Strong/Big
            if (tier != Tier.Weak)
            {
                float newIntensity = (tier == Tier.Medium) ? mediumDotIntensity : strongDotIntensity;
                ApplyOrRefreshDot(dmgComp, newIntensity);
            }
        }
    }

    void ApplyOrRefreshDot(DamageableComponent target, float newIntensity)
    {
        float now = Time.time;

        if (!_dots.TryGetValue(target, out var st))
        {
            st = new DotState
            {
                Intensity = Mathf.Max(0f, newIntensity),
                ExpiresAt = now + dotBaseDuration,
                NextTickAt = now + dotTickInterval
            };
            _dots[target] = st;
            return;
        }

        if (newIntensity > st.Intensity)
            st.Intensity = newIntensity;

        st.ExpiresAt = now + dotBaseDuration; // рефреш
    }

    void TickDots(float now)
    {
        _toRemove.Clear();

        foreach (var kv in _dots)
        {
            var dmgComp = kv.Key;
            var st = kv.Value;

            if (dmgComp == null) { _toRemove.Add(kv.Key); continue; }
            if (now >= st.ExpiresAt) { _toRemove.Add(kv.Key); continue; }

            if (now >= st.NextTickAt)
            {
                st.NextTickAt = now + dotTickInterval;
                float amount = Mathf.Max(0f, dotDpsPerIntensity * st.Intensity * dotTickInterval);
                if (amount > 0f)
                    dmgComp.TakeDamage(new FireDotTickDamage(amount, st.Intensity, this));
            }
        }

        for (int i = 0; i < _toRemove.Count; i++)
            _dots.Remove(_toRemove[i]);
        _toRemove.Clear();
    }

    // боевой тир (для урона), визуально не используется
    Tier GetTier(float x)
    {
        if (x < mediumThreshold) return Tier.Weak;
        if (x < strongThreshold) return Tier.Medium;
        if (x < Mathf.Max(strongThreshold + 0.0001f, peakIntensity * 0.95f)) return Tier.Strong;
        return Tier.Big;
    }

    // -------------------- SINGLE-FX CONTROL --------------------
    void ApplyFxContinuous(float x, bool immediate)
    {
        if (!_fxInstance) return;

        // нормализация 0..1 относительно 0..peakIntensity
        float n = Mathf.InverseLerp(0f, Mathf.Max(0.001f, peakIntensity), Mathf.Max(0f, x));
        float targetScale    = scaleCurve.Evaluate(n);
        float targetEmission = emissionCurve.Evaluate(n);

        if (immediate)
        {
            _currentScale    = targetScale;
            _currentEmission = targetEmission;
        }
        else
        {
            _currentScale    = Mathf.SmoothDamp(_currentScale,    targetScale,    ref _scaleVel, fxSmoothTime);
            _currentEmission = Mathf.SmoothDamp(_currentEmission, targetEmission, ref _emisVel,  fxSmoothTime);
        }

        _fxInstance.transform.localScale = Vector3.one * _currentScale;
        SetEmissionMultiplier(_fxInstance, _currentEmission);
    }

    void SetEmissionMultiplier(ParticleSystem root, float m)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in all)
        {
            var em = ps.emission;
            var rate = em.rateOverTime;   // MinMaxCurve
            rate.curveMultiplier = m;     // современный способ масштабировать rate
            em.rateOverTime = rate;
        }
    }

    // -------------------- EXTINGUISH --------------------
    IEnumerator CoExtinguishAndMaybeDestroy()
    {
        enabled = false;

        if (_fxInstance)
        {
            float t = Mathf.Max(0.05f, extinguishFadeTime);
            float time = 0f;

            var all = _fxInstance.GetComponentsInChildren<ParticleSystem>(true);
            var initial = new float[all.Length];
            for (int i = 0; i < all.Length; i++)
                initial[i] = all[i].emission.rateOverTime.curveMultiplier;

            while (time < t)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(time / t);

                for (int i = 0; i < all.Length; i++)
                {
                    var ps = all[i];
                    var em = ps.emission;
                    var rate = em.rateOverTime;
                    rate.curveMultiplier = initial[i] * k;
                    em.rateOverTime = rate;
                }

                _fxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, _currentScale, k); // лёгкое схлопывание
                yield return null;
            }

            foreach (var ps in all)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (destroyOnExtinguish)
            Destroy(gameObject);
    }

    // внешнее «заливание водой»/штраф к интенсивности
    public void AddExtinguishImpulse(float amount)
    {
        if (amount <= 0f) return;
        intensity = Mathf.Max(0f, intensity - amount);
        _burningOut = true; // форсируем выгорание
    }

    // -------------------- SPREAD / GIZMOS --------------------
    void TrySpread()
    {
        if (UnityEngine.Random.value > spreadChance) return;

        Vector3 dir = UnityEngine.Random.insideUnitSphere; dir.y = 0f; dir.Normalize();
        Vector3 spawnPos = transform.position + dir * (_radius + spreadDistance);

        if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out var hit, 15f, ~0, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point;

        var child = Instantiate(firePrefab, spawnPos, Quaternion.identity);
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

        child.dotBaseDuration = dotBaseDuration;
        child.dotTickInterval = dotTickInterval;
        child.dotDpsPerIntensity = dotDpsPerIntensity;
        child.mediumDotIntensity = Mathf.Max(0.1f, mediumDotIntensity * 0.9f);
        child.strongDotIntensity = Mathf.Max(0.1f, strongDotIntensity * 0.9f);

        _childrenSpawned++;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        float r = Application.isPlaying ? _radius : initialRadius;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, r);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.8f);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}