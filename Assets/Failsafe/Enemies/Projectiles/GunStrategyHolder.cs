using UnityEngine;

public class GunStrategyHolder : MonoBehaviour
{
    [Tooltip("Ссылка на ScriptableObject с настройками (урон, скорострельность)")]
    public WeaponStrategy strategy;

    [Tooltip("Точка выстрела (пустой объект на конце дула этой модели)")]
    public Transform muzzlePoint; 
}