using Failsafe.Scripts.EffectSystem;
using UnityEngine;

public abstract class WeaponStrategy : ScriptableObject
{
    [Header("Settings")]
    public WeaponStats stats;
    public AmmoConfig ammoConfig;

    [Header("Effects")]
    [Tooltip("Пакет эффектов, который будет применяться при попадании.")]
    public EffectBundle impactEffects;

    [Header("Animation")]
    [Tooltip("Если true, оружие не будет дергать триггер атаки каждый выстрел. Подходит для лазеров и огнеметов.")]
    public bool isContinuousFire = false;

    [Header("Visuals")]
    public GameObject modelPrefab;

    public virtual void Initialize(WeaponController controller)
    {
    }

    public abstract bool Fire(WeaponController controller, Vector3 targetPoint);

    public virtual void StopFiring(WeaponController controller)
    {
    }
}