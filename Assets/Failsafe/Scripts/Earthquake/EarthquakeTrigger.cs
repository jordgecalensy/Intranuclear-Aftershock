using System;
using UnityEngine;

public class EarthquakeTrigger : MonoBehaviour
{
    public static event Action<float, float> OnEarthquakeStarted;
    public static event Action<EarthquakeEnvironmentZone, float, float> OnEarthquakeZoneStarted;

    [SerializeField] private KeyCode _triggerKey = KeyCode.M;
    [SerializeField] private float _strength = 2f;
    [SerializeField] private float _duration = 3f;
    [Header("Zone Link")]
    [SerializeField] private EarthquakeEnvironmentZone _earthquakeZone;
    [Header("Auto Trigger")]
    [SerializeField] private bool _autoTriggerOnZoneEnter;
    [SerializeField] private string _triggeringTag = "Player";
    [SerializeField] private Vector2 _randomRollRange = new(0f, 100f);
    [SerializeField] private Vector2 _triggerSuccessRange = new(0f, 30f);

    // После успешного автосрабатывания эта конкретная точка больше не триггерит землетрясение повторно.
    private bool _hasTriggeredFromZoneEnter;

    private void Awake()
    {
        TryResolveZone();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryResolveZone();
    }
#endif

    private void Update()
    {
        if (Input.GetKeyDown(_triggerKey))
        {
            TriggerEarthquake("manual key press");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_autoTriggerOnZoneEnter)
            return;

        if (_hasTriggeredFromZoneEnter)
            return;

        if (!string.IsNullOrWhiteSpace(_triggeringTag) && !other.CompareTag(_triggeringTag))
            return;

        float minRoll = Mathf.Min(_randomRollRange.x, _randomRollRange.y);
        float maxRoll = Mathf.Max(_randomRollRange.x, _randomRollRange.y);
        float successMin = Mathf.Min(_triggerSuccessRange.x, _triggerSuccessRange.y);
        float successMax = Mathf.Max(_triggerSuccessRange.x, _triggerSuccessRange.y);

        float rolledValue = UnityEngine.Random.Range(minRoll, maxRoll);
        bool shouldTrigger = rolledValue >= successMin && rolledValue <= successMax;

        //Debug.Log(
            //$"[EarthquakeTrigger] Zone enter by {other.name}. Roll={rolledValue:F2}, " +
            //$"TriggerRange={successMin:F2}-{successMax:F2}, ShouldTrigger={shouldTrigger}");

        if (shouldTrigger)
        {
            _hasTriggeredFromZoneEnter = true;
            TriggerEarthquake($"zone enter by {other.name}");
        }
    }

    private void TriggerEarthquake(string triggerReason)
    {
        //Debug.Log(
            //$"[EarthquakeTrigger] Earthquake triggered by {triggerReason}. " +
            //$"Strength={strength}, Duration={duration}, Zone={earthquakeZone?.name}");

        OnEarthquakeStarted?.Invoke(_strength, _duration);

        if (_earthquakeZone != null)
        {
            OnEarthquakeZoneStarted?.Invoke(_earthquakeZone, _strength, _duration);
        }
        else
        {
            //Debug.LogWarning(
                //$"[EarthquakeTrigger] Zone-specific earthquake skipped on '{name}', " +
                //"because EarthquakeEnvironmentZone is not assigned.");
        }
    }

    private void TryResolveZone()
    {
        if (_earthquakeZone != null)
            return;

        _earthquakeZone = GetComponentInParent<EarthquakeEnvironmentZone>();
    }
}
