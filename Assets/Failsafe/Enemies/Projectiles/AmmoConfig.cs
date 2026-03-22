using UnityEngine;

[CreateAssetMenu(fileName = "DefaultAmmo", menuName = "Combat/Ammo Config")]
public class AmmoConfig : ScriptableObject
{
    public int maxAmmo = 30;       // Размер магазина
    public float reloadTime = 2.0f; // Время перезарядки
    public bool infiniteAmmo = false; // Бесконечные патроны (для лазеров или тестов)
}