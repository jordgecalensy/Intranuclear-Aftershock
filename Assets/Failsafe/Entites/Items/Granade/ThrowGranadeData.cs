using UnityEngine;

[CreateAssetMenu(fileName = "GranadeData", menuName = "ScriptableObjects/Entities/Items/GranadeData")]
public class ThrowGranadeData : ScriptableObject
{
    [Header("Granade Settings")]
    [SerializeField] private float _granadeTimer;
    [SerializeField] private float _mineTriggerRadius;
    [Header("Delays")]
    [SerializeField] private float _startUseDelay;
    [SerializeField] private float _useDelay;

    public float GranadeTimer 
    {
        get
        {
            return _granadeTimer; 
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
