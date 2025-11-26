using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using FMODUnity;

namespace Failsafe.Scripts.EffectSystem
{
public class DamageHitEffect : Effect, IReapplicableEffect
{
    private Material _damageHitMaterial;
    private CustomPassVolume _customPassVolume;
    private StudioEventEmitter _damageHitEmitter;

    private EventReference _damageHitEvent;

    private float _initialAlpha = 1f;      // стартовое значение альфа
    private float _currentAlpha = 1f;      // текущее значение
    private float _fadeOutStart = 0.2f;    // за сколько секунд до конца начинаем fade

    private const string _alphaIntensity = "_AlphaIntensity";

    public DamageHitEffect(float duration, float fadeOutStart = 0.2f, float initialAlpha = 1f)
    {
        _duration = duration;
        IsUniqueEffect = true;

        _initialAlpha = initialAlpha;
        _currentAlpha = initialAlpha;
        _fadeOutStart = fadeOutStart;

        _damageHitMaterial = Resources.Load<Material>("TakingDamage");
        if (_damageHitMaterial == null)
            Debug.LogWarning("DamageHitEffect: материал DamageHitEffect не найден!");

        _damageHitEvent = EventReference.Find("event:/UI/LowHP/LowHealthSFX");
    }

    public override void ApplyEffect()
    {
        _customPassVolume = new GameObject("DamageHitEffectPass")
            .AddComponent<CustomPassVolume>();

        _customPassVolume.isGlobal = true;
        _customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;

        var pass = new CustomPassDrawer(_damageHitMaterial);
        _customPassVolume.customPasses.Add(pass);

        // Звук
        _damageHitEmitter = _customPassVolume.gameObject.AddComponent<StudioEventEmitter>();
        _damageHitEmitter.EventReference = _damageHitEvent;
        _damageHitEmitter.Play();

        SetAlpha(_initialAlpha);
    }

    public override void Update()
    {
        float remaining = ElapsedAt - Time.time;

        if (remaining <= _fadeOutStart)
        {
            float t = Mathf.Clamp01(remaining / _fadeOutStart);

            _currentAlpha = Mathf.Lerp(0f, _initialAlpha, t);
            SetAlpha(_currentAlpha);
        }
    }

    public override void ClearEffect()
    {
        if (_damageHitEmitter != null)
            _damageHitEmitter.Stop();

        if (_customPassVolume != null)
            Object.Destroy(_customPassVolume.gameObject);
    }

    private void SetAlpha(float value)
    {
        if (_damageHitMaterial != null)
            _damageHitMaterial.SetFloat(_alphaIntensity, value);
    }

    public void OnReapply(Effect newEffect)
    {
        DamageHitEffect reapplied = newEffect as DamageHitEffect;
        if (reapplied == null)
            return;

        float remaining = ElapsedAt - Time.time;
        if (remaining < 0f)
            remaining = 0f;

        // продлеваем эффект на длительность нового вызова
        _duration = remaining + reapplied._duration;

        // сбрасываем AlphaIntensity
        _currentAlpha = _initialAlpha;
        SetAlpha(_initialAlpha);

        // звук
        if (_damageHitEmitter != null)
        {
            _damageHitEmitter.Stop();
            _damageHitEmitter.Play();
        }
    }
}

}
