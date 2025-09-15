using System.Linq;
using UnityEngine;
using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using Failsafe.Scripts.Damage.Providers;
using Failsafe.Scripts.Health;
using System;
using VContainer;

public sealed class DamageServiceBinder : MonoBehaviour
{
    private IDamageService _service;
    private DamageableComponent _damageable;
    private IHealth _health;
    
    [Inject]
    public void Construct(IHealth health)
    {
        _health = health;
        Debug.Log($"[DamageServiceBinder] DI Inject: IHealth = {(_health != null ? _health.ToString() : "null")}", this);
    }
    private void Awake()
    {
        _damageable = GetComponent<DamageableComponent>();
        // 1) IHealth
        if (_health == null)
        {
            // ищем у себя/родителей компонент, который реализует IHealth
            var t = transform;
            while (t != null && _health == null)
            {
                var mb = t.GetComponents<MonoBehaviour>().OfType<IHealth>().FirstOrDefault();
                if (mb != null) _health = mb;
                t = t.parent;
            }
        }
        

        // 2) DamageService + все нужные провайдеры
        _service = new DamageService(new FlatDamageProvider(_health));
        _service.Register(new FireContactDamageProvider(_health));
        _service.Register(new FireDotTickDamageProvider(_health));
        _service.Register(new FireDamageProvider(_health));

        // 3) Подписка одного сервиса на все входящие IDamage
        _damageable.OnTakeDamage += _service.Provide;
    }

    private void Update()
    {
        if (_health != null)
        {
            Debug.Log("Есть здоровье: " + _health.CurrentHealth);
        }
    }

    private void OnDestroy()
    {
        if (_damageable != null) _damageable.OnTakeDamage -= _service.Provide;
    }
    
}