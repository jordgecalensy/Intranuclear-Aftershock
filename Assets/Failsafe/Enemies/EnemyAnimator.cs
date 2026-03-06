using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Animator _animator;
    
    // EnemyMovement остается обычным классом, поэтому получаем его через Init
    private EnemyMovement _movement;
    
    // Хеши параметров
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
    [SerializeField] private int _idleAnimationCount = 3;
    private bool _isIdlePlaying;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
    }

    // Инициализация зависимости от мотора (вызывается из Enemy.cs)
    public void Initialize(EnemyMovement movement)
    {
        _movement = movement;
    }

    // Вызывается каждый кадр из Enemy.cs, чтобы синхронизировать порядок выполнения
    public void ManualUpdate()
    {
        if (_movement == null) return;

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
        // Логика движения без изменений
        Vector3 targetDir = _movement.DesiredVelocity;
        float rawAngle = 0f;
        
        if (targetDir.sqrMagnitude > 0.1f)
        {
            rawAngle = Vector3.SignedAngle(transform.forward, targetDir, Vector3.up);
        }

        _currentTurnAngle = Mathf.SmoothDampAngle(_currentTurnAngle, rawAngle, ref _turnVelocity, TurnSmoothTime);
        _animator.SetFloat(TurnAngleHash, Mathf.Clamp(_currentTurnAngle, -90f, 90f));

        if (_movement.IsRotatingInPlace)
            _animator.SetFloat(SpeedHash, 1f); 
        else
            _animator.SetFloat(SpeedHash, _movement.CurrentSpeed);
    }

    public void OnAnimatorMove()
    {
        if (_movement != null)
            _movement.ApplyRootMotion(_animator.deltaPosition);
    }

    public void HandleIdleAnimations()
    {
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
   
    // --- API для Unity Events (Inspector) ---
    // Эти методы мы будем вызывать из WeaponController через Inspector

    public void StartReloading() => SetCombatState(false, true);
    public void StopReloading() => SetCombatState(false, false);
    public void StartAttacking() => SetCombatState(true, false);
    public void StopAttacking() => SetCombatState(false, false);

    // Внутренняя логика
    private void SetCombatState(bool attacking, bool reloading)
    {
        if (attacking) reloading = false;
        _animator.SetBool(IsAttackingHash, attacking);
        _animator.SetBool(IsReloadingHash, reloading);
    }
    
    public void ClearCombat() => SetCombatState(false, false);
    
    public bool IsInAction()
    {
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.IsTag("Attack") || state.IsTag("Reload");
    }

    public void SetAttacking(bool v) => SetCombatState(v, false);
    public void SetReloading(bool v) => SetCombatState(false, v);

    // Триггеры
    public void TryAlert() 
    {
        _animator.ResetTrigger(PlayIdleHash); 
        _animator.SetTrigger(AlertHash);
    }
    public void TryStun() => _animator.SetTrigger("Stun");
    public void IsInStun(bool state) => _animator.SetBool("isInStun", state);
    public void TryDeath() => _animator.SetTrigger("Die");
}