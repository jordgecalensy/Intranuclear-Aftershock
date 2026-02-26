using UnityEngine;

[CreateAssetMenu(fileName = "DefaultStats", menuName = "Combat/Weapon Stats")]
public class WeaponStats : ScriptableObject
{
    [Header("Combat")]
    public float damage = 10f;      // Урон
    public float hitForce = 5f;     // Сила толчка (физика)
    public LayerMask hitMask;       // По кому попадаем

    [Header("Ballistics")]
    public float fireRate = 0.2f;   // Скорострельность
    public float range = 50f;       // Дальность полета
    public float projectileSpeed = 20f; // <-- НОВОЕ: Скорость пули
}