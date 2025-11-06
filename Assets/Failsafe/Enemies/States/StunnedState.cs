using UnityEngine;

/// <summary>
/// Состояние cтана
/// </summary>
public class StunnedState : BehaviorForcedState
{
    private float _stunProgress;
    private Transition _transitionToPreviousState;
    private Vector3 _impactDirection = Vector3.zero;
    private EnemyNavMeshActions _enemyNavMeshActions;
    private EnemyAnimator _enemyAnimator;
    private Transform _transform;

    public StunnedState(EnemyAnimator enemyAnimator, EnemyNavMeshActions navMeshActions, Transform transform)
    {
        _enemyNavMeshActions = navMeshActions;
        _enemyAnimator = enemyAnimator;
        _transform = transform;
    }

    public override void Enter()
    {
        base.Enter();
        _enemyAnimator.TryStun();
        _enemyAnimator.isInStun(true);
        _stunProgress = 0;
        _transitionToPreviousState = new Transition(this, PreviousState, IsStateFinished);
        Debug.Log("Enter StunnedState");
    }

    public override void Update()
    {
        _stunProgress += Time.deltaTime;
        //Debug.Log(_stunProgress);
    }

    public override void Exit()
    {
        _enemyAnimator.isInStun(false);
        if (!_impactDirection.Equals(Vector3.zero))
        {
            _enemyNavMeshActions.RotateToPoint(_transform.position + _impactDirection);
            _impactDirection = Vector3.zero;
        }
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