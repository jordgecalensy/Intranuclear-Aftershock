using UnityEngine;

/// <summary>
/// VIEW: Отвечает ТОЛЬКО за параметры аниматора.
/// Получает данные от EnemyMovement.
/// </summary>
public class EnemyAnimator
{
    private readonly Animator _animator;
    private readonly EnemyMovement _movement;

    // Хеши
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int PlayIdleHash = Animator.StringToHash("PlayIdle");
    private static readonly int AlertHash = Animator.StringToHash("Alert");
    // Сглаживание
    private float _currentTurnAngle;
    private float _turnVelocity;
    private const float TurnSmoothTime = 0.1f;

    // Idle
    private readonly int _idleAnimationCount = 3;
    private bool _isIdlePlaying;

    public EnemyAnimator(Animator animator, EnemyMovement movement)
    {
        _animator = animator;
        _movement = movement;
    }

    public void UpdateAnimator()
    {
        if (IsInAction())
        {
            _isIdlePlaying = false;
            return;
        }

        UpdateLocomotion();
        HandleIdleAnimations();
    }

    private void UpdateLocomotion()
    {
        // 1. Угол: Вычисляем визуальный наклон тела
        // Берем вектор желаемой скорости от Мотора
        Vector3 targetDir = _movement.DesiredVelocity;
        float rawAngle = 0f;
        
        if (targetDir.sqrMagnitude > 0.1f)
        {
            rawAngle = Vector3.SignedAngle(_animator.transform.forward, targetDir, Vector3.up);
        }

        _currentTurnAngle = Mathf.SmoothDampAngle(_currentTurnAngle, rawAngle, ref _turnVelocity, TurnSmoothTime);
        _animator.SetFloat(TurnAngleHash, Mathf.Clamp(_currentTurnAngle, -90f, 90f));

        // 2. Скорость: 
        // Если Мотор говорит "Я кручусь на месте", мы ставим Speed=1, чтобы BlendTree играл Turn Animation
        if (_movement.IsRotatingInPlace)
        {
            _animator.SetFloat(SpeedHash, 1f); 
        }
        else
        {
            _animator.SetFloat(SpeedHash, _movement.CurrentSpeed);
        }
    }

    public void OnAnimatorMove()
    {
        // Передаем дельту перемещения в Мотор
        _movement.ApplyRootMotion(_animator.deltaPosition);
    }

    // --- Idle и Боевка ---

    public void HandleIdleAnimations()
    {
        // Условия для Idle: Скорость ~0, не крутимся, не прыгаем
        if (_movement.CurrentSpeed < 0.1f && !_movement.IsRotatingInPlace && !_movement.IsBusy && !_isIdlePlaying)
        {
            PlayRandomIdle();
        }
    }

    private void PlayRandomIdle()
    {
        if (_idleAnimationCount <= 0) return;
        _animator.SetInteger(IdleIndexHash, Random.Range(0, _idleAnimationCount));
        _animator.SetTrigger(PlayIdleHash);
        _isIdlePlaying = true; 
    }
    
    public void SetCombatState(bool attacking, bool reloading)
    {
        if (attacking) reloading = false;
        _animator.SetBool(IsAttackingHash, attacking);
        _animator.SetBool(IsReloadingHash, reloading);
    }
    public void SetAttacking(bool v) => SetCombatState(v, false);
    public void SetReloading(bool v) => SetCombatState(false, v);
    public void ClearCombat() => SetCombatState(false, false);
    
    public bool IsInAction()
    {
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.IsTag("Attack") || state.IsTag("Reload");
    }
    public void TryAlert()
    {
        // Сбрасываем другие триггеры, чтобы не было накладок (опционально)
        _animator.ResetTrigger(PlayIdleHash); 
        _animator.SetTrigger(AlertHash);
    }
    // Триггеры
    public void TryStun() => _animator.SetTrigger("Stun");
    public void IsInStun(bool state) => _animator.SetBool("isInStun", state);
    public void TryDeath() => _animator.SetTrigger("Die");
}