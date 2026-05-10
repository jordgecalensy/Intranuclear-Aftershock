using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "GranadeData", menuName = "ScriptableObjects/Entities/Items/GranadeData")]
public class ThrowGrenadeData : ScriptableObject
{
    [Header("Granade Settings")]
    [SerializeField] private float _grenadeTimer;
    [SerializeField] private float _mineTriggerRadius;
    [Header("Delays")]
    [SerializeField] private float _startUseDelay;
    [SerializeField] private float _useDelay;
    [Header("SFX")]
    [SerializeField] private EventReference _throwGrenade;
    [SerializeField] private EventReference _mineStateOn;
    [SerializeField] private EventReference _mineStateOff;
    [SerializeField] private EventReference _mineIndication;
    [SerializeField] private EventReference _minePinPull;


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
    public EventReference ThrowGrendeSfx
    {
        get
        {
            return _throwGrenade;
        }
    }
    public EventReference MineStateOnSfx
    {
        get
        {
            return _mineStateOn;
        }
    }
    public EventReference MineStateOffSfx
    {
        get
        {
            return _mineStateOff;
        }
    }
    public EventReference MineIndication
    {
        get
        {
            return _mineIndication;
        }
    }
    public EventReference MinePinPull
    {
        get
        {
            return _minePinPull;
        }
    }

}
