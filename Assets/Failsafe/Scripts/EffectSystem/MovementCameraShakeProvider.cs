using UnityEngine;
using Failsafe.Scripts.EffectSystem;
using VContainer.Unity;

public class MovementCameraShakeProvider : ITickable
{
    private readonly InputHandler _input;
    private readonly IEffectApplicationService _effects;
    private readonly GameplayEffectCatalog _effectCatalog;
    private readonly GameObject _target;
    private readonly Collider _targetCollider;

    private float _nextShakeTime;

    public MovementCameraShakeProvider(
        InputHandler input,
        IEffectApplicationService effects,
        GameplayEffectCatalog effectCatalog,
        GameObject target,
        Collider targetCollider)
    {
        _input = input;
        _effects = effects;
        _effectCatalog = effectCatalog;
        _target = target;
        _targetCollider = targetCollider;
    }

    public void Tick()
    {
        if (_input.MovementInput == Vector2.zero)
            return;

        if (Time.time < _nextShakeTime)
            return;

        float power;
        float interval = 0f;

        if (_input.SprintTriggered)
        {
            power = 1f;
            interval = 0.5f;
        }
        else
        {
            power = 0f;
            interval = 1.17f;
        }

        Vector3 point = _targetCollider != null
            ? _targetCollider.bounds.center
            : _target.transform.position;

        var context = new EffectContext(
            _target,
            _targetCollider,
            point,
            Vector3.up,
            _target.transform.forward,
            power,
            _target);

        _effects.Apply(
            _effectCatalog.MovementCameraShake,
            context);

        _nextShakeTime = Time.time + interval;
    }
}
