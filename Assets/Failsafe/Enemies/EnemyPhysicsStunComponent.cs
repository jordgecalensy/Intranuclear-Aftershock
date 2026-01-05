using UnityEngine;

/// <summary>
/// Компонент, обрабатывающий физические столкновения.
/// Вызывает состояние StunnedState при сильном ударе или поворачивает врага при слабом.
/// </summary>
public class EnemyPhysicsStunComponent : MonoBehaviour
{
    private Enemy_ScriptableObject _physicsStunData;
    private Enemy _enemy;

    void Start()
    {
        _enemy = GetComponent<Enemy>();
        // EnemyNavMeshActions здесь больше не нужен, так как мы не используем навигацию для реакции на физику
        if (_enemy != null)
        {
            _physicsStunData = _enemy.EnemyConfig;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Проверка на корректность данных столкновения
        if (_enemy == null || _physicsStunData == null || collision.rigidbody == null) return;

        // Расчет "силы" стана на основе кинетической энергии
        var stunTime = Mathf.Pow(collision.relativeVelocity.magnitude, 2) * collision.rigidbody.mass * _physicsStunData.StunMultiplier;
        
        // Вектор направления удара (откуда прилетело)
        // -5f — инвертируем импульс, чтобы враг повернулся лицом к опасности
        Vector3 impactDirection = collision.impulse.normalized * -5f; 

        if (stunTime > _physicsStunData.MinStunTime)
        {
            // Ограничиваем максимальное время стана
            stunTime = Mathf.Min(stunTime, (float)_physicsStunData.MaxStunTime);
            
            // Передаем направление и время в Enemy (который переведет StateMachine)
            _enemy.StunnedState(impactDirection, stunTime / 1000f);
            
            Debug.Log($"Physics Stun applied: {stunTime / 1000f}s");
        }
        else
        {
            // Слабый удар: просто поворачиваемся к источнику угрозы
            RotateToImpact(impactDirection);
        }
    }

    /// <summary>
    /// Локальный метод поворота к источнику удара.
    /// Используется мгновенный поворот, так как это реакция на физический импульс.
    /// </summary>
    private void RotateToImpact(Vector3 direction)
    {
        direction.y = 0; // Игнорируем наклон по вертикали
        
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = lookRotation;
        }
    }
}