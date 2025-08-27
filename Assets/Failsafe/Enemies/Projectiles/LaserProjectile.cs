using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    public float speed = 20f;
    public float lifetime = 5f;
    public int damage = 10;

    private Vector3 _direction;

    public void Initialize(Vector3 direction)
    {
        _direction = direction.normalized;
        // Поворачиваем снаряд в сторону движения
        if (_direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_direction);
        }
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += _direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Игнорируем столкновения с самим врагом или другими снарядами
        if (other.GetComponent<Enemy>() != null || other.GetComponent<LaserProjectile>() != null)
        {
            return; // Не делаем ничего и не уничтожаем снаряд
        }

        // Пытаемся нанести урон, если у объекта есть компонент DamageableComponent
        var damageable = other.GetComponent<DamageableComponent>();
        if (damageable != null)
        {            
            damageable.TakeDamage(new FlatDamage(damage));
        }

        // Уничтожаем снаряд при столкновении с любым другим объектом (игроком, стеной и т.д.)
        Destroy(gameObject);
    }
}
