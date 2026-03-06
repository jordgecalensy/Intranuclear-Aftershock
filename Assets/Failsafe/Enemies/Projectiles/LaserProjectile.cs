using UnityEngine;
using Failsafe.Scripts.Damage.Implementation; // Ваш namespace для урона

public class LaserProjectile : MonoBehaviour
{
    // Эти поля больше НЕ публичные и НЕ Serialized. 
    // Их нельзя настроить в инспекторе префаба, только через код.
    private float _speed;
    private float _damage;
    private float _maxLifetime;
    private LayerMask _hitMask;
    
    private Vector3 _startPosition;

    // --- ГЛАВНЫЙ МЕТОД ---
    // Вызывается сразу после спавна
    public void Initialize(float speed, float damage, float range, LayerMask mask)
    {
        _speed = speed;
        _damage = damage;
        _hitMask = mask;
        
        // Вычисляем время жизни: Время = Расстояние / Скорость
        // Если скорость 20, а дальность 100 -> пуля живет 5 секунд
        _maxLifetime = range / speed;
        
        _startPosition = transform.position;
        
        // Уничтожить через время (страховка)
        Destroy(gameObject, _maxLifetime);
    }

    private void Update()
    {
        // 1. Движение вперед
        transform.Translate(Vector3.forward * _speed * Time.deltaTime);

        // 2. Проверка дистанции (опционально, если Destroy(time) недостаточно точно)
        if (Vector3.Distance(_startPosition, transform.position) >= _speed * _maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем слой (входит ли слой объекта в нашу маску)
        if ((_hitMask.value & (1 << other.gameObject.layer)) > 0)
        {
            // Наносим урон
            var damageable = other.GetComponentInChildren<DamageableComponent>(); // Или ваш интерфейс IHealth
            if (damageable != null)
            {
                // Передаем урон, который получили из конфига
                damageable.TakeDamage(new FlatDamage(_damage));
            }

            // Эффект попадания (можно добавить позже)
            Destroy(gameObject);
        }
        else 
        {
            // Если попали в стену (Default), тоже уничтожаем
            if (!other.isTrigger) Destroy(gameObject);
        }
    }
}