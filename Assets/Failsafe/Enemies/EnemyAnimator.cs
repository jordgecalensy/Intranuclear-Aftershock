using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Animator _animator;
    
    private EnemyMovement _movement;
    private bool _hasTurnAngle;
    private int _meleeLayerIndex;
    private int _meleeRecoilHash;
    private int _spinRecoilHash;
    private int _combatIdleHash;
    private int _chargePoseHash;
    
    // Хеши параметров
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
    private static readonly int IdleIndexHash = Animator.StringToHash("IdleIndex");
    private static readonly int PlayIdleHash = Animator.StringToHash("PlayIdle");
    private static readonly int AlertHash = Animator.StringToHash("Alert");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int MeleeWindupSpeedHash = Animator.StringToHash("MeleeWindupSpeed");
    private static readonly int SpinAttackHash = Animator.StringToHash("SpinAttack");
    private static readonly int IsInStunHash = Animator.StringToHash("isInStun");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
    private const float MeleeLayerBlendDuration = 0.1f;

    public event System.Action MeleeWindowOpened;
    public event System.Action MeleeWindowClosed;
    public event System.Action MeleeAttackFinished;

    // Animation Events клипа мили-атаки.
    public void OpenMeleeHitWindow() => MeleeWindowOpened?.Invoke();
    public void CloseMeleeHitWindow() => MeleeWindowClosed?.Invoke();
    public void FinishMeleeAttack() => MeleeAttackFinished?.Invoke();

    public void BeginChargePreparation()
    {
        CancelMeleeAttack();
        _animator.ResetTrigger(AlertHash);
        _animator.Play(IdleStateHash, 0, 0f);
        StartAttacking();
        _animator.Play(_chargePoseHash, _meleeLayerIndex, 0.3f);
    }

    public void PlayChargeDash() => _animator.SetLayerWeight(_meleeLayerIndex, 1f);

    public void FinishCharge() => CancelMeleeAttack();

    public void PlayMeleeRecoil(float secondsBeforeCurrentPose)
    {
        var state = _animator.GetCurrentAnimatorStateInfo(_meleeLayerIndex);
        if (_animator.IsInTransition(_meleeLayerIndex) &&
            _animator.GetNextAnimatorStateInfo(_meleeLayerIndex).IsTag("Attack"))
            state = _animator.GetNextAnimatorStateInfo(_meleeLayerIndex);

        float contactTime = Mathf.Clamp01(state.normalizedTime -
            secondsBeforeCurrentPose * _animator.speed / Mathf.Max(0.001f, state.length));
        _animator.ResetTrigger(AttackTriggerHash);
        // У состояния со Speed = -1 время отсчитывается от конца клипа.
        int recoilHash = state.shortNameHash == SpinAttackHash ? _spinRecoilHash : _meleeRecoilHash;
        _animator.Play(recoilHash, _meleeLayerIndex, 1f - contactTime);
        // Вызывается из LateUpdate hitbox: сразу возвращаем позу к месту контакта.
        _animator.Update(0f);
    }

    public void FinishMeleeRecoil() =>
        _animator.CrossFadeInFixedTime(_combatIdleHash, 0.1f, _meleeLayerIndex, 0f);

    public void SetMeleeWindupSpeed(float speed) => _animator.SetFloat(MeleeWindupSpeedHash, speed);

    public void CancelMeleeAttack()
    {
        _animator.ResetTrigger(AttackTriggerHash);
        StopAttacking();
        if (_meleeLayerIndex > 0)
            _animator.SetLayerWeight(_meleeLayerIndex, 0f);
        // Прерванный клип не должен продолжиться после повторного включения Enemy.
        if (_animator.isActiveAndEnabled &&
            (_animator.GetCurrentAnimatorStateInfo(_meleeLayerIndex).IsTag("Attack") ||
             _animator.GetCurrentAnimatorStateInfo(_meleeLayerIndex).IsTag("Charge") ||
             (_animator.IsInTransition(_meleeLayerIndex) &&
              _animator.GetNextAnimatorStateInfo(_meleeLayerIndex).IsTag("Attack"))))
            _animator.Play(_combatIdleHash, _meleeLayerIndex, 0f);
    }

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
        // Контроллеры без слоя Melee продолжают использовать полный клип на Base Layer.
        _meleeLayerIndex = Mathf.Max(0, _animator.GetLayerIndex("Melee"));
        string layerName = _animator.GetLayerName(_meleeLayerIndex);
        _meleeRecoilHash = Animator.StringToHash(layerName + ".AttackRecoil");
        _spinRecoilHash = Animator.StringToHash(layerName + ".SpinAttackRecoil");
        _combatIdleHash = Animator.StringToHash(layerName + ".CombatIdle");
        _chargePoseHash = Animator.StringToHash(layerName + ".ChargePose");
    }

    public void Initialize(EnemyMovement movement)
    {
        _movement = movement;
        // Не каждый контроллер содержит анимацию поворота на месте (например, паук).
        _hasTurnAngle = false;
        if (_animator != null)
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
                if (parameter.nameHash == TurnAngleHash && parameter.type == AnimatorControllerParameterType.Float)
                    _hasTurnAngle = true;
    }

    public void ManualUpdate(bool retreating = false)
    {
        if (_movement == null) return;

        bool isInAction = IsInAction();
        if (_meleeLayerIndex > 0)
        {
            float targetWeight = _animator.GetBool(IsAttackingHash) &&
                !_animator.GetBool(IsInStunHash) ? 1f : 0f;
            _animator.SetLayerWeight(_meleeLayerIndex, Mathf.MoveTowards(
                _animator.GetLayerWeight(_meleeLayerIndex), targetWeight,
                Time.deltaTime / MeleeLayerBlendDuration));
        }

        // У слоистого мили ноги продолжают получать скорость во время замаха.
        if (_meleeLayerIndex > 0 || !isInAction)
            UpdateLocomotion(retreating);

        if (isInAction)
        {
            _isIdlePlaying = false;
            return;
        }

        HandleIdleAnimations();
    }

    private void UpdateLocomotion(bool retreating)
    {
        Vector3 targetDir = _movement.DesiredVelocity;
        float rawAngle = 0f;
        
        if (targetDir.sqrMagnitude > 0.1f)
        {
            rawAngle = Vector3.SignedAngle(transform.forward, targetDir, Vector3.up);
        }

        _currentTurnAngle = Mathf.SmoothDampAngle(_currentTurnAngle, rawAngle, ref _turnVelocity, TurnSmoothTime);
        if (_hasTurnAngle)
            _animator.SetFloat(TurnAngleHash, Mathf.Clamp(_currentTurnAngle, -90f, 90f));

        if (_movement.IsRotatingInPlace)
            _animator.SetFloat(SpeedHash, 1f); 
        else
            _animator.SetFloat(SpeedHash, retreating ? -_movement.CurrentSpeed : _movement.CurrentSpeed);
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
        var state = _animator.GetCurrentAnimatorStateInfo(_meleeLayerIndex);
        
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
