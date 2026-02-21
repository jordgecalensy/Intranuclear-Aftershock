using UnityEngine;

public class EnemyDeathState : BehaviorForcedState
{
    private EnemyAnimator _enemyAnimator;
    private EnemyMovement _movement; // <-- Новый класс
    private Animator _animator;
    private AnimationEvent _replaceEvent = new AnimationEvent();
    private readonly int _deathStateHash = Animator.StringToHash("Base Layer.Death");

    // Обновленный конструктор
    public EnemyDeathState(EnemyAnimator enemyAnimator, EnemyMovement movement, Animator animator)
    {
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _animator = animator;
    }

    public override void Enter()
    {
        base.Enter();
        Debug.Log("Enter DeathState");
        
        // Останавливаем движение через Мотор
        _movement.Stop();
        
        // Запускаем триггер смерти
        _enemyAnimator.TryDeath();
    }

    public override void Update()
    {
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        // Ждем, пока проиграется анимация смерти
        if (stateInfo.fullPathHash == _deathStateHash)
        {
            AnimatorClipInfo[] clips = _animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0)
            {
                AnimationClip clip = clips[0].clip;
                
                // Проверяем, чтобы не добавлять событие каждый кадр (оптимизация)
                if (_replaceEvent.functionName != "ReplaceWithDummy" || _replaceEvent.time != clip.length)
                {
                    _replaceEvent.time = clip.length;
                    _replaceEvent.functionName = "ReplaceWithDummy";
                    clip.AddEvent(_replaceEvent);
                }
            }
        }
    }
}