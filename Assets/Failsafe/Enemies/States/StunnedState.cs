using UnityEngine;

/// <summary>
/// Состояние cтана
/// </summary>
public class StunnedState : BehaviorForcedState
{
    private float _stunProgress;
    private Transition _transitionToPreviousState;
    private Vector3 _impactDirection;

    public override void Enter()
    {
        base.Enter();
        _stunProgress = 0;
        _transitionToPreviousState = new Transition(this, PreviousState, IsStateFinished);
        Debug.Log("Enter StunnedState");
    }

    public override void Update()
    {
        _stunProgress += Time.deltaTime;
        Debug.Log(_stunProgress);
    }

    private bool IsStateFinished() => StateDuration != null ? _stunProgress >= StateDuration : false;

    public override Transition DecideTransition()
    {
        if (IsStateFinished())
            return _transitionToPreviousState;
        else
            return null;
    }

    public void SetDirection(Vector3 impactDirection)
    {
        _impactDirection = impactDirection;
    }
}