using Failsafe.Scripts.EffectSystem;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

public class Projectile : MonoBehaviour
{
    [Header("Аудио Снаряда")]
    [Tooltip("Зацикленный звук пролета снаряда")]
    [SerializeField] private EventReference _flybySound;

    [Tooltip("Звук попадания/взрыва")]
    [SerializeField] private EventReference _impactSound;

    private float _speed;
    private float _maxLifetime;
    private LayerMask _hitMask;
    private Vector3 _startPosition;
    private float _power = 1f;

    private GameObject _source;
    private EffectBundle _impactEffects;
    private IEffectApplicationService _effects;

    private EventInstance _flybyInstance;

    public void Initialize(
        float speed,
        float range,
        LayerMask mask,
        float power,
        GameObject source,
        EffectBundle impactEffects,
        IEffectApplicationService effects)
    {
        _speed = Mathf.Max(0.01f, speed);
        _hitMask = mask;
        _power = Mathf.Max(0f, power);
        _source = source;
        _impactEffects = impactEffects;
        _effects = effects;

        _maxLifetime = range / _speed;
        _startPosition = transform.position;

        StartFlybySound();

        Destroy(gameObject, _maxLifetime);
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);

        if (Vector3.Distance(_startPosition, transform.position) >= _speed * _maxLifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        if (other.isTrigger)
            return;

        bool isValidTarget = (_hitMask.value & (1 << other.gameObject.layer)) > 0;

        if (isValidTarget)
        {
            var context = new EffectContext(
                _source,
                other,
                transform.position,
                -transform.forward,
                transform.forward,
                _power);

            _effects?.Apply(_impactEffects, context);
        }

        PlayImpactSound();
        Destroy(gameObject);
    }

    private void StartFlybySound()
    {
        if (_flybySound.IsNull)
            return;

        _flybyInstance = RuntimeManager.CreateInstance(_flybySound);
        RuntimeManager.AttachInstanceToGameObject(_flybyInstance, transform);
        _flybyInstance.start();
    }

    private void PlayImpactSound()
    {
        if (!_impactSound.IsNull)
            RuntimeManager.PlayOneShot(_impactSound, transform.position);
    }

    private void OnDestroy()
    {
        if (_flybyInstance.isValid())
        {
            _flybyInstance.stop(STOP_MODE.ALLOWFADEOUT);
            _flybyInstance.release();
            _flybyInstance.clearHandle();
        }
    }
}