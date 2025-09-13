using UnityEngine;
using Failsafe.Scripts.Damage.Implementation;

public class DamageOnTouch : MonoBehaviour
{
    [Header("Движение объекта")]
    public Transform targetPoint;            // конечная точка
    public float moveSpeed = 3f;             // скорость движения

    private Vector3 _startPoint;             // стартовая точка (фиксируется в Start)
    private Vector3 _currentTarget;          // к какой точке сейчас движемся

    [Header("Урон")]
    public float damageAmount = 10f;         // урон за тик
    public float damageInterval = 1f;        // периодичность тика

    private DamageableComponent _damageable;
    private bool _isTouching;
    private float _damageTimer;

    private void Start()
    {
        _startPoint = transform.position;               // фиксируем старт
        if (targetPoint != null)
            _currentTarget = targetPoint.position;
        else
            _currentTarget = _startPoint;
    }

    private void Update()
    {
        // движение объекта туда-обратно
        transform.position = Vector3.MoveTowards(
            transform.position,
            _currentTarget,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, _currentTarget) < 0.01f)
        {
            // меняем направление
            if (_currentTarget == _startPoint)
                _currentTarget = targetPoint.position;
            else
                _currentTarget = _startPoint;
        }

        // тик урона
        if (_isTouching && _damageable != null)
        {
            _damageTimer -= Time.deltaTime;
            if (_damageTimer <= 0f)
            {
                _damageable.TakeDamage(new FlatDamage(damageAmount));
                _damageTimer = damageInterval;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        _damageable = other.GetComponentInChildren<DamageableComponent>();
        if (_damageable != null)
        {
            _isTouching = true;
            _damageTimer = 0f; // сразу наносим при входе
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_damageable != null && other.GetComponentInChildren<DamageableComponent>() == _damageable)
        {
            _isTouching = false;
            _damageable = null;
        }
    }
}