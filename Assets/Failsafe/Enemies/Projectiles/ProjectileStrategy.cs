using Failsafe.Scripts.EffectSystem;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Strategies/Projectile")]
public class ProjectileStrategy : WeaponStrategy
{
    public GameObject projectilePrefab;

    [Header("Projectile Impact Physics")]
    [Tooltip("Отдельный пакет физических эффектов попадания. Его сила масштабируется через WeaponStats Hit Force.")]
    [SerializeField] private EffectBundle _impactPhysicsEffects;

    [Header("Projectile Flight Physics Field")]
    [Tooltip("Радиус движущейся сферы, которая ищет Rigidbody вокруг траектории снаряда.")]
    [SerializeField, Min(0f)] private float _flightFieldRadius;

    [Tooltip("Однократный импульс, расталкивающий каждый Rigidbody от траектории снаряда.")]
    [SerializeField, Min(0f)] private float _flightFieldImpulse;

    public override bool Fire(WeaponController controller, Vector3 targetPoint)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError($"[{nameof(ProjectileStrategy)}] Projectile prefab is not assigned.", this);
            return false;
        }

        if (controller.firePoint == null)
        {
            Debug.LogError($"[{nameof(ProjectileStrategy)}] FirePoint is not assigned.", controller);
            return false;
        }

        if (stats == null)
        {
            Debug.LogError($"[{nameof(ProjectileStrategy)}] WeaponStats is not assigned.", this);
            return false;
        }

        Vector3 startPosition = controller.firePoint.position;
        Vector3 direction = targetPoint - startPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = controller.firePoint.forward;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized);
        GameObject projectileObject = Instantiate(projectilePrefab, startPosition, rotation);

        Projectile projectile = projectileObject.GetComponent<Projectile>();

        if (projectile == null)
        {
            Debug.LogError(
                $"[{nameof(ProjectileStrategy)}] Projectile prefab must contain Projectile component.",
                projectilePrefab);

            Destroy(projectileObject);
            return false;
        }

        projectile.Initialize(
            stats.projectileSpeed,
            stats.range,
            stats.hitMask,
            stats.damage,
            stats.hitForce,
            _flightFieldRadius,
            _flightFieldImpulse,
            controller.gameObject,
            impactEffects,
            _impactPhysicsEffects,
            controller.Effects);

        return true;
    }

    private void OnValidate()
    {
        _flightFieldRadius = Mathf.Max(0f, _flightFieldRadius);
        _flightFieldImpulse = Mathf.Max(0f, _flightFieldImpulse);
    }
}
