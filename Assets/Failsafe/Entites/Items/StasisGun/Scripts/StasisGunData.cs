using UnityEngine;
[CreateAssetMenu(fileName = "StasisGunData", menuName = "ScriptableObjects/Entities/Items/Components/StasisGunData")]
public class StasisGunData : ScriptableObject
{
    public float StasisDuration;
    public float FireRate;
    public int ChargeAmountMax;

    /// <summary>
    /// Время с момента использования предмета (например, нажатия кнопки) до срабатывания его эффекта (Нужно для синхронизации анимации, геймплея и звука)
    /// </summary>
    public float StartUseDelay;
    /// <summary>
    /// Кулдаун изпользования предмета
    /// </summary>
    public float UseDelay;
}
