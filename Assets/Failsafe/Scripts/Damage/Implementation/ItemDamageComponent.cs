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

    private int _minDamage = 20; //Если высчитаный скриптом результат меньше этого значения - урон не проходит
    private int _maxDamage = 200; //Верхняя граница урона, результаты выше понижаются до этого параметра
    private float _damageMultiplier = 2; //Множитель урона, чтобы подогнать результат произведения массы на скорость к желаемому значению урона
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
        var impactBase = Mathf.Pow(collision.relativeVelocity.magnitude, 2) * collision.rigidbody.mass;

        var damage = impactBase * _damageMultiplier;
        if (damage > _minDamage)
        {
            damage = Mathf.Min(damage, _maxDamage);
            _damageableComponent.TakeDamage(new FlatDamage(damage));
            Debug.Log("Урон: " + damage);
        }
    }
}