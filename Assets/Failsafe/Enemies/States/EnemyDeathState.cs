using UnityEngine;

public class EnemyDeathState : BehaviorForcedState
{
    private EnemyAnimator _enemyAnimator;
    private EnemyMovement _movement; 
    private Animator _animator;
    private Enemy_ScriptableObject _enemyConfig; // Добавили конфиг, чтобы брать оттуда префаб
    
    private readonly int _deathStateHash = Animator.StringToHash("Die");
    private bool _isReplaced; // Предохранитель от мульти-спавна

    // Обновленный конструктор: теперь мы передаем сюда еще и конфиг врага
    public EnemyDeathState(EnemyAnimator enemyAnimator, EnemyMovement movement, Animator animator, Enemy_ScriptableObject config)
    {
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _animator = animator;
        _enemyConfig = config;
    }

    public override void Enter()
    {
        base.Enter();
        _isReplaced = false; // Сбрасываем предохранитель при входе
        
        // Останавливаем движение
        _movement.Stop();
        
        // Запускаем анимацию смерти
        _enemyAnimator.TryDeath();
    }

    public override void Update()
    {
        // Если кукла уже заспавнена - выходим, ничего больше не делаем
        if (_isReplaced) return;

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        // Проверяем, что сейчас играет именно анимация смерти
        if (stateInfo.fullPathHash == _deathStateHash)
        {
            // normalizedTime = 1.0f это самый конец анимации. Мы берем 0.95f для надежности
            if (stateInfo.normalizedTime >= 0.95f)
            {
                ReplaceWithDummy();
            }
        }
    }

    private void ReplaceWithDummy()
    {
        _isReplaced = true; // Защелкиваем предохранитель

        // Проверяем, назначен ли префаб куклы в конфиге
        if (_enemyConfig.DummyPrefab != null)
        {
            // Создаем мертвую куклу на месте живого паука
            Object.Instantiate(_enemyConfig.DummyPrefab, _animator.transform.position, _animator.transform.rotation);
        }
        else
        {
            Debug.LogWarning("RagdollPrefab не назначен в Enemy_ScriptableObject!");
        }

        // Удаляем живого паука из игры
        // Предполагается, что _animator висит на главном объекте паука
        Object.Destroy(_animator.gameObject); 
    }
}