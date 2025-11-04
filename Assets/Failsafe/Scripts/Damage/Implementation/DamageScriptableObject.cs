using UnityEngine;

[CreateAssetMenu(fileName = "Damage", menuName = "ScriptableObjects/Damage", order = 1)]
public class Damage_ScriptableObject : ScriptableObject
{
    public float DamageThreshhold;  //Если высчитаный скриптом результат меньше этого значения - урон не проходит
    public float MaxDamage;         //Верхняя граница урона, результаты выше понижаются до этого параметра
    public float DamageMultiplier;  //Множитель урона, чтобы подогнать результат произведения массы на скорость к желаемому значению урона
}
 