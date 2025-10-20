using Failsafe.Scripts.Damage.Implementation;
using System.Collections;
using UnityEngine;

/// <summary>
/// Компонент, который обрабатывает урон игроку от ударов физическими объектами
/// </summary>
[RequireComponent(typeof(DamageableComponent))]
public class ItemDamageComponent : MonoBehaviour
{
    private Collider _collider;
    private Rigidbody _rigidBody;
    private DamageableComponent _damageableComponent;
    [SerializeField]
    private Damage_ScriptableObject _config;

    void Start()
    {
        _collider = GetComponent<Collider>();
        _rigidBody = GetComponent<Rigidbody>();
        if (_collider == null && _rigidBody == null)
            Debug.LogError("На объекте не найден компонент для обработки коллизий");
        _damageableComponent = GetComponent<DamageableComponent>();
    }

    void OnCollisionEnter(Collision collision)
    {
        var impactBase = collision.relativeVelocity.magnitude * collision.rigidbody.mass;

        var damage = impactBase * _config.DamageMultiplier;
        if (damage > _config.DamageThreshhold)
        {
            damage = Mathf.Min(damage, _config.MaxDamage);
            _damageableComponent.TakeDamage(new FlatDamage(damage));
            Debug.Log("Урон: " + damage);
        }
    }
}