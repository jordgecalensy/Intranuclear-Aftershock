using UnityEngine;
using Failsafe.Scripts.EffectSystem;
using VContainer.Unity;
using Failsafe.PlayerMovements.Controllers;

public class MovementCameraShakeProvider : ITickable
{
    private readonly InputHandler _input;
    private readonly IEffectManager _effects;
    private readonly PlayerRotationController _rotation;

    public MovementCameraShakeProvider(
        InputHandler input,
        IEffectManager effects,
        PlayerRotationController rotation)
    {
        _input = input;
        _effects = effects;
        _rotation = rotation;
    }

    public void Tick()
    {
        if (_input.MovementInput == Vector2.zero)
            return;

        bool sprint = _input.SprintTriggered;

        float intensity = sprint ? 0.75f : 0f;
        float frequency      = sprint ? 1.75f    : 0f;

        _effects.ApplyEffect(new CameraShakeEffect(_rotation, intensity, 0.1f, frequency));
    }
}

