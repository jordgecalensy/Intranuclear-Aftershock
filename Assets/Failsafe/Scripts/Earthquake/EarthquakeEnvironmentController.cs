using System;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer.Unity;

public class EarthquakeEnvironmentController : IInitializable, IDisposable
{
    private readonly IEffectApplicationService _effects;
    private readonly GameplayEffectCatalog _effectCatalog;

    public EarthquakeEnvironmentController(
        IEffectApplicationService effects,
        GameplayEffectCatalog effectCatalog)
    {
        _effects = effects;
        _effectCatalog = effectCatalog;
    }

    public void Initialize()
    {
        EarthquakeTrigger.OnEarthquakeZoneStarted += HandleEarthquake;
    }

    public void Dispose()
    {
        EarthquakeTrigger.OnEarthquakeZoneStarted -= HandleEarthquake;
    }

    private void HandleEarthquake(EarthquakeEnvironmentZone zone, float strength, float duration)
    {
        if (zone == null)
        {
            Debug.LogWarning("[EarthquakeEnvironmentController] Zone is null, earthquake environment effect skipped.");
            return;
        }

        var context = new EffectContext(
            zone.gameObject,
            null,
            zone.transform.position,
            Vector3.up,
            Vector3.up,
            strength,
            zone.gameObject,
            duration);

        _effects.Apply(
            _effectCatalog.EarthquakeEnvironment,
            context);
    }
}
