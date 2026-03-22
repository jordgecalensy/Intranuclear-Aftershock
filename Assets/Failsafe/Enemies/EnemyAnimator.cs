using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Animator _animator;
    
    private EnemyMovement _movement;
    
    // Хеши параметров
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int PlayIdleHash = Animator.StringToHash("PlayIdle");
    private static readonly int AlertHash = Animator.StringToHash("Alert");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

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

    public void Initialize(EnemyMovement movement)
    {
        _movement = movement;
    }

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
   
    // --- БОЕВАЯ ЛОГИКА ---

    public void PlayAttackTrigger() 
    {
        _animator.SetTrigger(AttackTriggerHash);
    }
    
    public void StartReloading() => SetCombatState(true, true); 
    public void StopReloading() => SetCombatState(true, false); 
    public void StartAttacking() => SetCombatState(true, false);
    public void StopAttacking() => SetCombatState(false, false);

    private void SetCombatState(bool attacking, bool reloading)
    {
        _animator.SetBool(IsAttackingHash, attacking);
        
        _animator.SetBool(IsReloadingHash, reloading); 
    }
    
    public void ClearCombat() => _animator.SetBool(IsAttackingHash, false);
    
    public bool IsInAction()
    {
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        
        // Проверяем, есть ли на текущей анимации тег Attack или Reload
        bool hasTag = state.IsTag("Attack") || state.IsTag("Reload");
        
        // Проверяем, стоит ли уже галочка стрельбы (даже если анимация еще переходит)
        bool isAttackingParam = _animator.GetBool(IsAttackingHash);

        // Если хоть что-то из этого true — мы в бою, Idle играть нельзя!
        return hasTag || isAttackingParam;
    }
    public void SetAttacking(bool v) => _animator.SetBool(IsAttackingHash, v);
    public void SetReloading(bool v) => _animator.SetBool(IsReloadingHash, v);
    
    public void TryAlert() 
    {
        _animator.ResetTrigger(PlayIdleHash); 
        _animator.SetTrigger(AlertHash);
    }

    public void TryStun() => _animator.SetTrigger("Stun");
    
    public void IsInStun(bool state) => _animator.SetBool("isInStun", state);
    
    public void TryDeath() => _animator.SetTrigger("Die");
    
    public void Jump() => _animator.SetTrigger("Jump");
    
    public void Land() => _animator.SetTrigger("Land");
}