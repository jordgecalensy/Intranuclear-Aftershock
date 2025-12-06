using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimator
{
    private readonly NavMeshAgent _navMeshAgent;
    private readonly Animator _animator;
    private readonly Transform _transform;
    private EnemyAudioManager _audioManager;
    private readonly MonoBehaviour _coroutineRunner; // Для запуска корутин
    private Coroutine _traversalCoroutine = null; // Ссылка на активную корутину
    private readonly bool _useRootMotion;

    private bool _isTurning = false;
    private bool _waitingForTurnToFinish = false;
    private bool _inCooldown = false;
    private bool _inAttack = false;
    private bool _wasGrounded = true;
    private bool _wasOnLink = false;

    // --- Поля для случайных Idle анимаций ---
    private readonly int _idleAnimationCount = 3; // Количество ваших idle анимаций
    private bool _isIdleAnimationPlaying = false;
    // --------------------------------------------

    public EnemyAnimator( NavMeshAgent navMeshAgent, Animator animator, Transform transform, bool useRootMotion)
    {
        _navMeshAgent = navMeshAgent;
        _animator = animator;
        _transform = transform;
        _useRootMotion = useRootMotion;
        _navMeshAgent.updatePosition = false;
        _navMeshAgent.updateRotation = false;
    }

    public void UpdateAnimator()
    {
        if (IsInAction())
        {
            _isIdleAnimationPlaying = false; // Сбрасываем флаг, когда враг атакует или перезаряжается
            return;
        }

        HandleIdleAnimations();

        if (_useRootMotion)
        {
            if (_isTurning)
            {
                var state = _animator.GetCurrentAnimatorStateInfo(0);

                if (state.IsTag("Turn") && state.normalizedTime >= 0.98f)
                {
                    _isTurning = false;
                    _animator.SetFloat("TurnAngle", 0f);
                }

                _animator.SetFloat("Speed", 0f);
                return;
            }

            // Вход в поворот
            if (ShouldStartTurn(out float clampedAngle))
            {
                _isTurning = true;
                _animator.SetFloat("TurnAngle", clampedAngle);
                _animator.CrossFade("TurnInPlace", 0.1f);
                _animator.SetFloat("Speed", 0f);
                return;
            }
        }

        UpdateSpeedBlend();
    }
    private bool ShouldStartTurn(out float clampedAngle)
    {
        clampedAngle = 0f;

        Vector3 desiredDirection = _navMeshAgent.desiredVelocity;
        if (desiredDirection.sqrMagnitude < 0.01f)
            return false;

        if (_navMeshAgent.velocity.magnitude > 0.1f)
            return false;

        float signedAngle = Vector3.SignedAngle(_transform.forward, desiredDirection.normalized, Vector3.up);

        if (Mathf.Abs(signedAngle) < 25f)
            return false; 

        clampedAngle = Mathf.Clamp(signedAngle, -180f, 180f);
        return true;
    }
    
    public void ApplyRoot()
    {
        _animator.applyRootMotion = true;

    }
   

    private void UpdateSpeedBlend()
    {
        if (_isTurning)
        {
            _animator.SetFloat("Speed", 0f);
            return;
        }

        float velocity = _navMeshAgent.velocity.magnitude;
        
        _animator.SetFloat("Speed", velocity);
    }


    public void ApplyRootMotion()
    {
        

        // Получаем текущую позицию агента на навмеш
        Vector3 agentNextPos = _navMeshAgent.nextPosition;

        // Считаем дельту из root motion
        Vector3 rootDelta = _animator.deltaPosition;
        rootDelta.y = 0f;

        // Предлагаемую новую позицию
        Vector3 proposedPos = _transform.position + rootDelta;

        // Обновляем позицию агента
        _navMeshAgent.nextPosition = proposedPos;

        // Перемещаем трансформ только в пределах навмеша
        _transform.position = _navMeshAgent.nextPosition;

        // Поворот
        if (_isTurning)
        {
            _transform.rotation = _animator.rootRotation;
        }
        else
        {
            Vector3 desiredVelocity = _navMeshAgent.desiredVelocity;
            if (desiredVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredVelocity.normalized);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }

    public void TryStun()
    {
        _animator.SetTrigger("Stun");
    }

    public void isInStun(bool state)
    {
        _animator.SetBool("isInStun", state);
    }
    public void TryAttack()
    {
        _animator.SetTrigger("Attack");
    }

    public void TryDeath()
    {
        _animator.SetTrigger("Die");
    }

    public bool IsInAction()
    {
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        return (state.IsTag("Attack") || state.IsTag("Reload")) && _inAttack;
    }

    public void TryReload()
    {
        _animator.SetTrigger("Reload");
    }

    public void isReloading(bool isReloading)
    {
        _inCooldown = isReloading;
        _animator.SetBool("isReloading", isReloading);
    }

    public void isAttacking()
    {
        _animator.SetTrigger("isAttacking");
    }

    public void StartMove(float speed)
    {
        _animator.SetFloat("Speed", speed );
    }

    public void HandleIdleAnimations()
    {
        var currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        _isIdleAnimationPlaying = currentStateInfo.IsTag("Idle");

        // Если враг стоит на месте и сейчас не проигрывается idle-анимация, запустить новую.
        if (_navMeshAgent.velocity.magnitude < 0.1f && !_isIdleAnimationPlaying)
        {
            PlayRandomIdleAnimation();
        }
    }

    public void PlayRandomIdleAnimation()
    {
        if (_idleAnimationCount <= 0) return;
        int randomIndex = Random.Range(0, _idleAnimationCount);
        _animator.SetInteger("IdleIndex", randomIndex);
        _animator.SetTrigger("PlayIdle");
    }

    public void IsActive()
    {
        _animator.SetBool("IsActivated", true);
    }

    public void IsOff()
    {
        _animator.SetBool("IsActivated", false);

    }
}
