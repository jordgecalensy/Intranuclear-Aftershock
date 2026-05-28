using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Strategies/Projectile")]
public class ProjectileStrategy : WeaponStrategy
{
    public GameObject projectilePrefab;

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

        Vector3 startPos = controller.firePoint.position;
        Vector3 direction = targetPoint - startPos;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = controller.firePoint.forward;

        Quaternion rotation = Quaternion.LookRotation(direction.normalized);
        GameObject projectileGO = Instantiate(projectilePrefab, startPos, rotation);

        var projectile = projectileGO.GetComponent<Projectile>();

        if (projectile == null)
        {
            Debug.LogError(
                $"[{nameof(ProjectileStrategy)}] Projectile prefab must contain Projectile component.",
                projectilePrefab);

            Destroy(projectileGO);
            return false;
        }

        projectile.Initialize(
            stats.projectileSpeed,
            stats.range,
            stats.hitMask,
            controller.gameObject,
            impactEffects,
            controller.Effects);

        return true;
    }
}