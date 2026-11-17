using Failsafe.Items;
using UnityEngine;

public class ShootAction : IActionWithItem
{
    private readonly WeaponController _weaponController;
    private readonly Transform _cameraTransform;

    public ShootAction(WeaponController weaponController, Transform cameraTransform)
    {
        _weaponController = weaponController;
        _cameraTransform = cameraTransform;
    }

    public ItemUseResult Execute(PlayerHandsContainer playerHandsContainer)
    {
        return Shoot();
    }

    public ItemUseResult Shoot()
    {
        // 1. Определяем, куда смотрит игрок (центр экрана)
        Vector3 targetPoint;
        Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 999f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(100f); // Если смотрим в небо
        }

        // 2. Командуем контроллеру стрелять в эту точку
        // (Контроллер сам проверит патроны, скорострельность и создаст пулю в MuzzlePoint)
        _weaponController.TryShoot(targetPoint);

        return new ItemUseResult
        {
            UsageType = UsageType.HoldToUse,
            ItemStateAfterUse = ItemState.Hold
        };
    }
}
