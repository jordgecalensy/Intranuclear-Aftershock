using UnityEngine;
using System;
using Failsafe.Scripts.EffectSystem;
using Failsafe.PlayerMovements.Controllers;

public class DamageCameraShakeProvider
{
    private readonly IEffectManager _effects;
    private readonly PlayerRotationController _rotation;

    public DamageCameraShakeProvider(
        IEffectManager effects,
        PlayerRotationController rotation)
    {
        _effects = effects;
        _rotation = rotation;
    }

    public void ApplyDamage(float damage)
    {
        var p = SelectPreset(damage);

        _effects.ApplyEffect(
            new CameraShakeEffect(_rotation, p.Intensity, p.Duration, p.Frequency));
    }

    private (float Intensity, float Duration, float Frequency) SelectPreset(float damage)
    {
        if (damage >= 30) return (0.65f, 0.38f, 18f);
        if (damage >= 15) return (0.35f, 0.28f, 18f);
        if (damage >= 1)  return (0.15f, 0.18f, 18f);
        return (0.10f, 0.15f, 15f);
    }
}
 
