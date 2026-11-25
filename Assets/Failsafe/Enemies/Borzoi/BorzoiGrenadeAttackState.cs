using UnityEngine;

public sealed class BorzoiGrenadeAttackState : BehaviorState
{
    private enum Phase { Ready, Throwing, Reloading }

    private const float AnimationTimeout = 7f;
    private const float AimRetryDelay = 0.4f;
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int ThrowState = Animator.StringToHash("Base Layer.Throw");
    private static readonly int ReloadState = Animator.StringToHash("Base Layer.Reload");

    private readonly BorzoiGrenadeLauncher _launcher;
    private readonly VisualSensor _vision;
    private readonly EnemyMovement _movement;
    private readonly EnemyAnimator _enemyAnimator;
    private readonly Animator _animator;
    private Phase _phase;
    private float _nextThrowTime;
    private float _animationDeadline;
    private bool _released;
    private bool _animationFailed;

    public event System.Action GrenadeThrown;

    public BorzoiGrenadeAttackState(BorzoiGrenadeLauncher launcher, VisualSensor vision,
        EnemyMovement movement, EnemyAnimator enemyAnimator, Animator animator)
    {
        _launcher = launcher;
        _vision = vision;
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _animator = animator;
    }

    public bool CanEnter() =>
        !_animationFailed && _launcher.IsReady && !_launcher.IsDepleted &&
        (_launcher.NeedsReload || TryGetTarget(out _));

    public bool CanLeave() => _phase == Phase.Ready &&
        (_animationFailed || !_launcher.IsReady || _launcher.IsDepleted ||
            (!_launcher.NeedsReload && !TryGetTarget(out _)));

    public override void Enter()
    {
        base.Enter();
        _phase = Phase.Ready;
        _movement.Stop();
        _launcher.ReleaseRequested += Release;
        _launcher.ThrowFinished += CompleteThrow;
        _launcher.ReloadFinished += CompleteReload;
    }

    public override void Update()
    {
        if (_animationFailed || !_launcher.IsReady)
        {
            _phase = Phase.Ready;
            return;
        }

        bool hasTarget = TryGetTarget(out Vector3 target);
        if (hasTarget)
            _movement.LookAt(target);

        if (_phase != Phase.Ready)
        {
            if (Time.time >= _animationDeadline)
            {
                Debug.LogError($"Borzoi {_phase} animation did not send its completion event.", _launcher);
                _animationFailed = true;
                _phase = Phase.Ready;
            }
            return;
        }

        if (_launcher.NeedsReload)
        {
            _phase = Phase.Reloading;
            _animationDeadline = Time.time + AnimationTimeout;
            _enemyAnimator.StartReloading();
            _animator.Play(ReloadState, 0, 0f);
        }
        else if (!_launcher.IsDepleted && hasTarget && Time.time >= _nextThrowTime)
        {
            if (!_launcher.CanPlanThrow(target))
            {
                _nextThrowTime = Time.time + AimRetryDelay;
                return;
            }

            _phase = Phase.Throwing;
            _released = false;
            _animationDeadline = Time.time + AnimationTimeout;
            _enemyAnimator.StartAttacking();
            _animator.Play(ThrowState, 0, 0f);
        }
    }

    public override void Exit()
    {
        _launcher.ReleaseRequested -= Release;
        _launcher.ThrowFinished -= CompleteThrow;
        _launcher.ReloadFinished -= CompleteReload;
        _phase = Phase.Ready;
        if (_animator.isActiveAndEnabled)
        {
            _enemyAnimator.StopAttacking();
            _animator.Play(IdleState, 0, 0f);
        }
        base.Exit();
    }

    private bool TryGetTarget(out Vector3 position)
    {
        position = default;
        if (_vision == null || !_vision.SeenPlayer || !_vision.SignalSourcePosition.HasValue)
            return false;

        position = _vision.Target.position;
        return _launcher.IsInRange(position);
    }

    private void Release()
    {
        if (_phase != Phase.Throwing || _released)
            return;

        _released = true;
        bool thrown = TryGetTarget(out Vector3 target) && _launcher.TryThrow(target);
        if (thrown)
            GrenadeThrown?.Invoke();
        _nextThrowTime = Time.time + (thrown ? _launcher.ThrowCooldown : AimRetryDelay);
    }

    private void CompleteThrow()
    {
        if (_phase != Phase.Throwing)
            return;

        _phase = Phase.Ready;
        if (!_released)
        {
            Debug.LogError("Borzoi throw clip is missing its grenade release event.", _launcher);
            _animationFailed = true;
        }
    }

    private void CompleteReload()
    {
        if (_phase != Phase.Reloading)
            return;

        _launcher.CompleteReload();
        _enemyAnimator.StopReloading();
        _phase = Phase.Ready;
    }
}
