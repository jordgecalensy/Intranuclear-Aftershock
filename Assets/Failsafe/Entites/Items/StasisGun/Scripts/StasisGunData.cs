using Failsafe.Scripts.EffectSystem;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StasisGunData",
    menuName = "ScriptableObjects/Entities/Items/StasisGunData")]
public class StasisGunData : ScriptableObject
{
    [Header("Legacy")]
    public float StasisDuration;

    public float FireRate;
    public int ChargeAmountMax;

    [Header("Effects")]
    public EffectBundle DefaultModeEffects;
    public EffectBundle AlternativeModeEffects;

    public float StartUseDelay;
    public float UseDelay;

    public FMODUnity.EventReference GunshotSFX;
    public FMODUnity.EventReference EmptyShotSFX;
    public FMODUnity.EventReference ModeSwitchSFX;
}