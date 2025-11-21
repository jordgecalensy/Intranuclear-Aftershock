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
        if (damage >= 30) return (3.5f, 0.6f, 8f);
        if (damage >= 15) return (1.1f, 0.4f, 18f);
        if (damage >= 1)  return (0.45f, 0.25f, 20f);
        return (0.25f, 0.15f, 20f);
    }
}
 
