using UnityEngine;

/// <summary>
/// Состояние деактивации (катсцена, спавн, отключение).
/// </summary>
public class DisabledState : BehaviorForcedState
{
    private float _disableProgress;
    private Transition _transitionToPreviousState;
    private Animator _animator;
    private EnemyMovement _movement; // <-- Добавили
    private float _normalAnimationSpeed;

    // Обновленный конструктор
    public DisabledState(Animator animator, EnemyMovement movement)
    {
        _animator = animator;
        _movement = movement;
    }

    public override void Enter()
    {
        base.Enter();
        
        // 1. Останавливаем физику
        _movement.Stop();
        
        // 2. Останавливаем анимацию
        _normalAnimationSpeed = _animator.speed;
        _animator.speed = 0;
        
        _disableProgress = 0;
        _transitionToPreviousState = new Transition(this, PreviousState, IsStateFinished);
        Debug.Log("Enter DisabledState");
    }

    public override void Update()
    {
        _disableProgress += Time.deltaTime;
        // Debug.Log(_disableProgress);
    }

    public override void Exit()
    {
        // Восстанавливаем скорость анимации
        _animator.speed = _normalAnimationSpeed;
        
        // Физику (isStopped = false) восстановит следующий стейт сам при вызове MoveTo
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