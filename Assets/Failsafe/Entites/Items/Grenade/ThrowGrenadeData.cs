using UnityEngine;

[CreateAssetMenu(fileName = "GranadeData", menuName = "ScriptableObjects/Entities/Items/GranadeData")]
public class ThrowGrenadeData : ScriptableObject
{
    [Header("Granade Settings")]
    [SerializeField] private float _grenadeTimer;
    [SerializeField] private float _mineTriggerRadius;
    [Header("Delays")]
    [SerializeField] private float _startUseDelay;
    [SerializeField] private float _useDelay;

    public float GrenadeTimer 
    {
        get
        {
            return _grenadeTimer; 
        } 
    }
    public float StartUseDelay
    {
        get
        {
            return _startUseDelay;
        }
    }
    public float UseDelay
    {
        get
        {
            return _useDelay;
        }
    }
    public float MineTriggerRadius
    {
        get
        {
            return _mineTriggerRadius;
        }
    }

}
