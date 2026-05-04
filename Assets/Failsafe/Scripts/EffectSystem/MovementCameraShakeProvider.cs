using UnityEngine;
using Failsafe.Scripts.EffectSystem;
using VContainer.Unity;
using Failsafe.PlayerMovements.Controllers;

public class MovementCameraShakeProvider : ITickable
{
    private readonly InputHandler _input;
    private readonly IEffectManager _effects;
    private readonly PlayerRotationController _rotation;

    private float _nextShakeTime;

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

        if (Time.time < _nextShakeTime)
            return;

        float intensity = 0f;
        float frequency = 0f;
        float interval = 0f;

        if (_input.SprintTriggered)
        {
            intensity = 0.6f;
            frequency = 10f;
            interval = 0.5f;
        }
        else
        {
            intensity = 0.18f;
            frequency = 5.25f;
            interval = 1.17f;
        }

        _effects.ApplyEffect(
            new CameraShakeEffect(
                _rotation,
                intensity,
                1.10f,
                frequency));

        _nextShakeTime = Time.time + interval;
    }
}