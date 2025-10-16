using UnityEngine;

public class EnemyDeathState : BehaviorForcedState
{
    private EnemyAnimator _enemyAnimator;

    public EnemyDeathState(EnemyAnimator enemyAnimator)
    {
        _enemyAnimator = enemyAnimator;
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("Enter DeathState");
        _enemyAnimator.TryDeath();
    }
}