using UnityEngine;

public class EnemyDeathState : BehaviorForcedState
{
    private EnemyAnimator _enemyAnimator;
    private EnemyNavMeshActions _enemyNavMeshActions;
    private Animator _animator;
    private AnimationEvent _replaceEvent = new AnimationEvent();
    private readonly int _deathStateHash = Animator.StringToHash("Base Layer.Death");

    public EnemyDeathState(EnemyAnimator enemyAnimator, EnemyNavMeshActions enemyNavMeshActions, Animator animator)
    {
        _enemyNavMeshActions = enemyNavMeshActions;
        _enemyAnimator = enemyAnimator;
        _animator = animator;
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("Enter DeathState");
        _enemyNavMeshActions.StopMoving();
        _enemyAnimator.TryDeath();
    }

    public override void Update()
    {
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        // Use the hashed name for comparison with the current state's full path hash
        if (stateInfo.fullPathHash == _deathStateHash)
        {
            // Get the animation clip and add the AnimationEvent
            AnimationClip clip = _animator.GetCurrentAnimatorClipInfo(0)[0].clip;

            _replaceEvent.time = clip.length;
            _replaceEvent.functionName = "ReplaceWithDummy";

            clip.AddEvent(_replaceEvent);
        }
    }
}