using UnityEngine;

public abstract class WeaponStrategy : ScriptableObject
{
    [Header("Settings")]
    public WeaponStats stats;
    public AmmoConfig ammoConfig;
    
    [Header("Visuals")]
    public GameObject modelPrefab; // Модель оружия в руках (опционально)

    // Инициализация (спавн VFX, пулов)
    public virtual void Initialize(WeaponController controller) { }

    // Логика выстрела. Возвращает true, если выстрел успешен
    public abstract bool Fire(WeaponController controller, Vector3 targetPoint);

    // Остановка (важно для лазера)
    public virtual void StopFiring(WeaponController controller) { }
}