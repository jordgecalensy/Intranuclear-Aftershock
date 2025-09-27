using UnityEngine;

/// <summary>
/// Состояние деактивации
/// </summary>
public class DisabledState : BehaviorForcedState
{
    private float _disableProgress;
    private Transition _transitionToPreviousState;
    private Animator _animator;
    private float _normalAnimationSpeed;

    public DisabledState(Animator animator)
    {
        _animator = animator;
    }

    public override void Enter()
    {
        base.Enter();
        _normalAnimationSpeed = _animator.speed;
        _animator.speed = 0;
        _disableProgress = 0;
        _transitionToPreviousState = new Transition(this, PreviousState, IsStateFinished);
        Debug.Log("Enter DisabledState");
    }

    public override void Update()
    {
        _disableProgress += Time.deltaTime;
        Debug.Log(_disableProgress);
    }

    public override void Exit()
    {
        _animator.speed = _normalAnimationSpeed;
    }

    private bool IsStateFinished() => StateDuration != null ? _disableProgress >= StateDuration : false;

    public override Transition DecideTransition()
    {
        if (IsStateFinished())
            return _transitionToPreviousState;
        else
            return null;
    }
}