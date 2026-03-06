using Failsafe.Items;
using UnityEngine;

public class GunUsable : IUsable
{
    private readonly WeaponController _weaponController;
    
    // Кэшируем задержки
    private float _startDelay;
    private float _useDelay;

    public GunUsable(WeaponController weaponController)
    {
        _weaponController = weaponController;
    }

    public void ParseItem(Item item)
    {
        // 1. Получаем данные с префаба
        var holder = item.GetComponent<GunStrategyHolder>();
        
        if (holder != null && holder.strategy != null)
        {
            // 2. Передаем стратегию в контроллер
            _weaponController.weaponStrategy = holder.strategy;

            // 3. --- ВАЖНО: Назначаем точку выстрела ---
            // Если у модели есть точка дула - берем её. 
            // Если забыли назначить - стреляем от центра предмета.
            if (holder.muzzlePoint != null)
            {
                _weaponController.firePoint = holder.muzzlePoint;
            }
            else
            {
                Debug.LogWarning($"У оружия {item.name} не назначен MuzzlePoint! Стреляем из центра объекта.");
                _weaponController.firePoint = item.transform;
            }

            // 4. Инициализируем (сбрасываем патроны, если нужно, или загружаем сохраненные)
            _weaponController.InitializeWeapon();

            // 5. Настраиваем задержки для системы рук
            _startDelay = 0f;
            _useDelay = holder.strategy.stats.fireRate; // Задержка между выстрелами
        }
        else
        {
            Debug.LogError($"ОШИБКА: На предмете {item.name} нет компонента GunStrategyHolder или не назначена стратегия!");
        }
    }

    // Этот метод определяет тип использования (зажать или кликнуть)
    public ItemUseResult Use()
    {
        // Возвращаем HoldToUse, чтобы можно было зажать кнопку для автоматного огня.
        // (WeaponController сам ограничит скорострельность через FireRate)
        return new ItemUseResult { UsageType = UsageType.HoldToUse };
    }

    public void Reload()
    {
        _weaponController.StartReload();
    }

    public void AltMode() 
    { 
        // Логика прицеливания (ADS) будет здесь
    }

    public void GetItemUseDelays(out float start, out float delay)
    {
        start = _startDelay;
        delay = _useDelay;
    }
}