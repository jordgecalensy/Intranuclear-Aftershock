using UnityEngine;
using Failsafe.Scripts.Damage.Implementation; // Твой namespace для урона

[CreateAssetMenu(menuName = "Combat/Strategies/LaserBeam")]
public class LaserBeamStrategy : WeaponStrategy
{
    public GameObject laserVfxPrefab; // Сюда префаб с LaserBeamController

    public override bool Fire(WeaponController controller, Vector3 targetPoint)
    {
        // 1. Пытаемся получить уже созданный лазер из памяти контроллера
        LaserBeamController beam = controller.GetRuntimeObject<LaserBeamController>("active_beam");
        Transform targetHelper = controller.GetRuntimeObject<Transform>("beam_target");

        // 2. Если лазера нет - создаем
        if (beam == null)
        {
            var go = Instantiate(laserVfxPrefab, controller.firePoint.position, Quaternion.identity);
            beam = go.GetComponent<LaserBeamController>();
            
            // Создаем невидимую точку-цель, за которой следит лазер
            var helperObj = new GameObject("LaserTargetHelper");
            targetHelper = helperObj.transform;
            
            // Инициализация твоим скриптом
            beam.Initialize(controller.firePoint, targetHelper);

            // Сохраняем в память контроллера
            controller.SetRuntimeObject("active_beam", beam);
            controller.SetRuntimeObject("beam_target", targetHelper);
        }

        // 3. Обновляем позицию цели
        if (targetHelper != null) targetHelper.position = targetPoint;

        // 4. Наносим урон (Raycast от дула к точке)
        ApplyLaserDamage(controller, targetPoint);

        return true;
    }

    public override void StopFiring(WeaponController controller)
    {
        LaserBeamController beam = controller.GetRuntimeObject<LaserBeamController>("active_beam");
        Transform targetHelper = controller.GetRuntimeObject<Transform>("beam_target");

        if (beam != null) Destroy(beam.gameObject);
        if (targetHelper != null) Destroy(targetHelper.gameObject);

        controller.ClearRuntimeObject("active_beam");
        controller.ClearRuntimeObject("beam_target");
    }

    private void ApplyLaserDamage(WeaponController controller, Vector3 targetPoint)
    {
        Vector3 dir = (targetPoint - controller.firePoint.position).normalized;
        float dist = Vector3.Distance(controller.firePoint.position, targetPoint);

        // Рейкаст для нанесения урона
        if (Physics.Raycast(controller.firePoint.position, dir, out RaycastHit hit, dist + 0.5f, stats.hitMask))
        {
            // Используем твою систему урона
            var damageable = hit.collider.GetComponentInChildren<DamageableComponent>();
            if (damageable != null)
            {
                damageable.TakeDamage(new FlatDamage(stats.damage * Time.deltaTime));
            }
        }
    }
}