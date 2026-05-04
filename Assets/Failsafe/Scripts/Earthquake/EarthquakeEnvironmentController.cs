using System;
using Failsafe.Scripts.EffectSystem;
using UnityEngine;
using VContainer.Unity;

public class EarthquakeEnvironmentController : IInitializable, IDisposable
{
    private readonly IEffectManager _effectManager;

    public EarthquakeEnvironmentController(
        IEffectManager effectManager)
    {
        _effectManager = effectManager;
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

        float horizontalForce = 0f;
        float verticalForce = 0f;
        float tickInterval = 0f;
        float effectDuration = 0f;

        bool destroyObjects = false;
        float destroyStartTime = 0f;
        float destroyEndTime = 0f;

        ForceMode forceMode = ForceMode.Impulse;

        if (strength > 0)
            switch (strength)
            {
                case >= 3:
                    horizontalForce = 8.0f; verticalForce = 2.0f;
                    tickInterval = 0.08f; effectDuration = 3.5f;
                    destroyObjects = true; destroyStartTime = 0.8f; destroyEndTime = 1.6f;
                    break;

                case >= 2:
                    horizontalForce = 5.5f; verticalForce = 1.3f;
                    tickInterval = 0.10f; effectDuration = 3.0f;
                    destroyObjects = true; destroyStartTime = 1.0f; destroyEndTime = 1.8f;
                    break;

                case >= 1:
                    horizontalForce = 3.5f; verticalForce = 0.8f;
                    tickInterval = 0.12f; effectDuration = 2.5f;
                    destroyObjects = true; destroyStartTime = 2f; destroyEndTime = 5f;
                    break;

                default:
                    horizontalForce = 2.0f; verticalForce = 0.35f;
                    tickInterval = 0.15f; effectDuration = 2.0f;
                    destroyObjects = true; destroyStartTime = 1f; destroyEndTime = 5f;
                    break;
            }

        effectDuration = Mathf.Max(effectDuration, duration);

        if (destroyObjects)
        {
            destroyStartTime = Mathf.Clamp(destroyStartTime, 0f, effectDuration);
            destroyEndTime = Mathf.Clamp(destroyEndTime, destroyStartTime, effectDuration);
        }

        //Debug.Log(
            //$"[EarthquakeEnvironmentController] Earthquake started. Strength={strength}, Duration={duration}, " +
            //$"EffectDuration={effectDuration}, DestroyObjects={destroyObjects}, " +
            //$"DestroyWindow={destroyStartTime}-{destroyEndTime}, Zone={zone.name}");

        _effectManager.ApplyEffect(
            new EarthquakeEnvironmentEffect(
                zone,
                horizontalForce,
                verticalForce,
                effectDuration,
                tickInterval,
                forceMode,
                destroyObjects,
                destroyStartTime,
                destroyEndTime));
    }
}
