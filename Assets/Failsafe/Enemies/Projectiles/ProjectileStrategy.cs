using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Strategies/Projectile")]
public class ProjectileStrategy : WeaponStrategy
{
    public GameObject projectilePrefab; 

    public override bool Fire(WeaponController controller, Vector3 targetPoint)
    {
        // 1. Точка спавна
        var startPos = controller.firePoint.position;
        
        // 2. Поворот к цели
        var rotation = Quaternion.LookRotation(targetPoint - startPos);

        // 3. Создаем пулю
        var projectileGO = Instantiate(projectilePrefab, startPos, rotation);
        
        // 4. Настраиваем пулю данными из STATS
        var projectile = projectileGO.GetComponent<LaserProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(
                stats.projectileSpeed, 
                stats.damage, 
                stats.range, 
                stats.hitMask
            );
        }

        return true;
    }
}