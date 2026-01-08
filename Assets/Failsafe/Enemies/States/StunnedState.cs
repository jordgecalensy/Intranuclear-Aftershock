using UnityEngine;

public class StunnedState : BehaviorForcedState
{
    private float _stunProgress;
    private Transition _transitionToPreviousState;
    private Vector3 _impactDirection = Vector3.zero;
    private EnemyMovement _movement; // <-- Новый класс
    private EnemyAnimator _enemyAnimator;
    private Transform _transform;

    public StunnedState(EnemyAnimator enemyAnimator, EnemyMovement movement, Transform transform)
    {
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _transform = transform;
    }

    public override void Enter()
    {
        base.Enter();
        _movement.Stop(); // Полная остановка
        _enemyAnimator.TryStun();
        _enemyAnimator.IsInStun(true);
        _stunProgress = 0;
        _transitionToPreviousState = new Transition(this, PreviousState, IsStateFinished);
    }

    public override void Update()
    {
        _stunProgress += Time.deltaTime;
    }

    public override void Exit()
    {
        _enemyAnimator.IsInStun(false);
        if (!_impactDirection.Equals(Vector3.zero))
        {
            // Разворачиваем к источнику удара
            _movement.LookAt(_transform.position + _impactDirection);
            _impactDirection = Vector3.zero;
        }
    }

    private bool IsStateFinished() => StateDuration != null ? _stunProgress >= StateDuration : false;

    public override Transition DecideTransition()
    {
        if (IsStateFinished()) return _transitionToPreviousState;
        else return null;
    }

    public void SetDirection(Vector3 impactDirection)
    {
        _impactDirection = impactDirection;
    }
}