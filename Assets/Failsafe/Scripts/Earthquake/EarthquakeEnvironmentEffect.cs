using System.Collections.Generic;
using UnityEngine;
using Failsafe.Scripts.EffectSystem;
using Failsafe.Scripts.Destruction;

public class EarthquakeEnvironmentEffect : Effect, IReapplicableEffect
{
    private readonly EarthquakeEnvironmentZone _zone;
    private readonly float _horizontalForce;
    private readonly float _verticalForce;
    private readonly float _tickInterval;
    private readonly ForceMode _forceMode;

    private readonly bool _destroyObjects;
    private readonly float _destroyStartTime;
    private readonly float _destroyEndTime;

    private float _tickTimer;
    private readonly HashSet<GameObject> _destroyedObjects = new();

    public EarthquakeEnvironmentEffect(
        EarthquakeEnvironmentZone zone,
        float horizontalForce,
        float verticalForce,
        float duration,
        float tickInterval,
        ForceMode forceMode,
        bool destroyObjects,
        float destroyStartTime,
        float destroyEndTime)
    {
        _zone = zone;
        _horizontalForce = horizontalForce;
        _verticalForce = verticalForce;
        _duration = duration;
        _tickInterval = tickInterval;
        _forceMode = forceMode;

        _destroyObjects = destroyObjects;
        _destroyStartTime = destroyStartTime;
        _destroyEndTime = destroyEndTime;

        // Эффекты разных зон должны жить параллельно, поэтому не делаем их уникальными по типу.
        IsUniqueEffect = false;
    }

    public override void ApplyEffect()
    {
        _tickTimer = 0f;
        _destroyedObjects.Clear();
        _zone?.BeginEarthquakeAudio();
        //Debug.Log(
            //$"[EarthquakeEnvironmentEffect] Applied. Zone={_zone?.name}, Duration={_duration}, " +
            //$"DestroyObjects={_destroyObjects}, DestroyWindow={_destroyStartTime}-{_destroyEndTime}");
    }

    public override void ClearEffect()
    {
        _zone?.EndEarthquakeAudio();
    }

    public override void Update()
    {
        if (_zone == null)
        {
           //Debug.LogWarning("[EarthquakeEnvironmentEffect] Zone is null, earthquake environment update skipped.");
            return;
        }

        _zone.RefreshObjects();

        float elapsed = Time.time - StarteAt;

        if (_destroyObjects && elapsed >= _destroyStartTime && elapsed <= _destroyEndTime)
        {
            var destructibles = _zone.DestructibleObjects;

            if (destructibles.Count == 0)
            {
                //Debug.Log(
                    //$"[EarthquakeEnvironmentEffect] Destroy window active, but no destructible objects found in zone '{_zone.name}'.");
            }

            for (int i = destructibles.Count - 1; i >= 0; i--)
            {
                GameObject go = destructibles[i];
                if (go == null)
                    continue;

                if (_destroyedObjects.Contains(go))
                    continue;

                _destroyedObjects.Add(go);

                IBreakable breakable = ResolveBreakable(go);

                if (breakable != null)
                {
                    breakable.Break();
                    continue;
                }

                // Старые объекты без IBreakable сохраняют прежнее поведение.
                Object.Destroy(go);
            }
        }

        _tickTimer += Time.deltaTime;

        if (_tickTimer < _tickInterval)
            return;

        _tickTimer = 0f;

        var carryObjects = _zone.CarryObjects;
        for (int i = 0; i < carryObjects.Count; i++)
        {
            Rigidbody rb = carryObjects[i];
            if (rb == null)
                continue;

            Vector3 randomHorizontal = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            );

            if (randomHorizontal.sqrMagnitude < 0.0001f)
                randomHorizontal = Vector3.right;
            else
                randomHorizontal.Normalize();

            Vector3 force = randomHorizontal * _horizontalForce;
            force.y = _verticalForce;

            rb.AddForce(force, _forceMode);
        }
    }

    public void OnReapply(Effect other)
    {
        if (other is EarthquakeEnvironmentEffect reapplied)
        {
            _duration = reapplied._duration + (Time.time - StarteAt);
        }
    }

    private static IBreakable ResolveBreakable(GameObject target)
    {
        if (target == null)
            return null;

        return target.GetComponent<IBreakable>() ??
               target.GetComponentInParent<IBreakable>() ??
               target.GetComponentInChildren<IBreakable>(true);
    }
}
