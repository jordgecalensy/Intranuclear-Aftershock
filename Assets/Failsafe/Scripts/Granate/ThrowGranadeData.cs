using UnityEngine;

[CreateAssetMenu(fileName = "GranadeData", menuName = "ScriptableObjects/Entities/Items/GranadeData")]
public class ThrowGranadeData : ScriptableObject
{

    [SerializeField] private float _throwForce;
    [SerializeField] private float _granadeTimer;
    [SerializeField] private float _startUseDelay;
    [SerializeField] private float _useDelay;
    [SerializeField] private float _mineTriggerRadius;

    public float ThrowForce
    {
        get
        {
            return _throwForce;
        }
    }
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
