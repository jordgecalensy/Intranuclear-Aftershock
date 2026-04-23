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
    [SerializeField] private EventReference _switchGrenade;
    [SerializeField] private EventReference _throwGrenade;

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
    public EventReference SwitchGrendeSfx
    {
        get
        {
            return _switchGrenade;
        }
    }
    public EventReference ThrowGrendeSfx
    {
        get
        {
            return _throwGrenade;
        }
    }

}
