using UnityEngine;
using Failsafe.Scripts.Damage.Implementation;
using FMODUnity;
using FMOD.Studio;

public class LaserProjectile : MonoBehaviour
{
    [Header("Аудио Снаряда")]
    [Tooltip("Зацикленный звук пролета снаряда")]
    [SerializeField] private EventReference _flybySound;
    [Tooltip("Звук попадания/взрыва")]
    [SerializeField] private EventReference _impactSound;

    // Внутренние параметры логики
    private float _speed;
    private float _damage;
    private float _maxLifetime;
    private LayerMask _hitMask;
    private Vector3 _startPosition;

    // FMOD Instance для зацикленного звука полета
    private EventInstance _flybyInstance;

    // --- ГЛАВНЫЙ МЕТОД ---
    public void Initialize(float speed, float damage, float range, LayerMask mask)
    {
        _speed = speed;
        _damage = damage;
        _hitMask = mask;
        
        _maxLifetime = range / speed;
        _startPosition = transform.position;

        // Запускаем звук полета сразу при спавне
        StartFlybySound();
        
        // Уничтожить через время (страховка)
        Destroy(gameObject, _maxLifetime);
    }

    private void Update()
    {
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);

        if (Vector3.Distance(_startPosition, transform.position) >= _speed * _maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем маску
        if ((_hitMask.value & (1 << other.gameObject.layer)) > 0)
        {
            var damageable = other.GetComponentInChildren<DamageableComponent>(); 
            if (damageable != null)
            {
                damageable.TakeDamage(new FlatDamage(_damage));
            }

            // Играем звук попадания прямо в точке столкновения
            PlayImpactSound();
            Destroy(gameObject);
        }
        else 
        {
            // Попали в стену
            if (!other.isTrigger) 
            {
                PlayImpactSound(); // Об стену тоже должен быть звук!
                Destroy(gameObject);
            }
        }
    }

    // ==========================================
    // АУДИО ЛОГИКА
    // ==========================================

    private void StartFlybySound()
    {
        if (_flybySound.IsNull) return;

        _flybyInstance = RuntimeManager.CreateInstance(_flybySound);
        
        // Привязываем звук к трансформу снаряда, чтобы он "летел" вместе с ним
        // Если на префабе снаряда есть Rigidbody, лучше передать и его 3-м аргументом
        RuntimeManager.AttachInstanceToGameObject(_flybyInstance, transform); 
        
        _flybyInstance.start();
    }

    private void PlayImpactSound()
    {
        if (!_impactSound.IsNull)
        {
            // Проигрываем разовый звук в текущих координатах снаряда
            RuntimeManager.PlayOneShot(_impactSound, transform.position);
        }
    }

    private void OnDestroy()
    {
        // ВАЖНО: Очищаем зацикленный звук при уничтожении снаряда,
        // иначе он будет гудеть вечно, как призрак!
        if (_flybyInstance.isValid())
        {
            _flybyInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _flybyInstance.release();
            _flybyInstance.clearHandle();
        }
    }
}