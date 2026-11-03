using UnityEngine;

public class MeleeAttackState : BehaviorState
{
    private enum Phase { Delay, Swing, Recoil, Cooldown }

    private readonly Sensor[] _sensors;
    private readonly Transform _transform;
    private readonly EnemyMovement _movement;
    private readonly EnemyAnimator _animator;
    private readonly Enemy_ScriptableObject _config;
    private readonly MeleeHitbox _hitbox;
    private Transform _target;
    private Phase _phase;
    private float _timer;
    private bool _directionLocked;

    public MeleeAttackState(Sensor[] sensors, Transform transform, EnemyMovement movement,
        EnemyAnimator animator, Enemy_ScriptableObject config, MeleeHitbox hitbox)
    {
        _sensors = sensors;
        _transform = transform;
        _movement = movement;
        _animator = animator;
        _config = config;
        _hitbox = hitbox;
    }

    public bool PlayerOutOfAttackRange() => _phase == Phase.Delay && !TargetInRange();

    public override void Enter()
    {
        base.Enter();
        _movement.Stop();
        _hitbox.EndSwing();
        _phase = Phase.Delay;
        _timer = 0f;
        _directionLocked = false;
        UpdateTarget();
        _animator.CancelMeleeAttack();
        _animator.MeleeWindowOpened += OpenHitWindow;
        _animator.MeleeWindowClosed += CloseHitWindow;
        _animator.MeleeAttackFinished += FinishSwing;
        _hitbox.ObstacleHit += Recoil;
        _animator.StartAttacking();
    }

    public override void Update()
    {
        UpdateTarget();
        if (!_directionLocked && _target != null)
            _movement.LookAt(_target.position);

        // После запуска клипа выход игрока из радиуса означает промах, а не отмену взмаха.
        if (_phase == Phase.Swing) return;
        _timer += Time.deltaTime;

        if (_phase == Phase.Recoil)
        {
            if (_timer >= _config.MeleeRecoilDuration)
            {
                _animator.FinishMeleeRecoil();
                EnterCooldown();
            }
            return;
        }

        if (_phase == Phase.Cooldown)
        {
            if (_timer < _config.AttackCooldown) return;
            _phase = Phase.Delay;
            _timer = 0f;
        }
        else if (TargetInRange() && _timer >= _config.AttackDelay)
        {
            _phase = Phase.Swing;
            _directionLocked = false;
            _hitbox.BeginSwing();
            _animator.PlayAttackTrigger();
        }
    }

    public override void Exit()
    {
        _animator.MeleeWindowOpened -= OpenHitWindow;
        _animator.MeleeWindowClosed -= CloseHitWindow;
        _animator.MeleeAttackFinished -= FinishSwing;
        _hitbox.ObstacleHit -= Recoil;
        _hitbox.EndSwing();
        _animator.CancelMeleeAttack();
        _target = null;
        base.Exit();
    }

    private void OpenHitWindow()
    {
        if (_phase != Phase.Swing || _directionLocked) return;
        _directionLocked = true;
        _hitbox.OpenWindow();
    }

    private void CloseHitWindow()
    {
        if (_phase == Phase.Swing) _hitbox.CloseWindow();
    }

    private void Recoil(float secondsBeforeCurrentPose)
    {
        if (_phase != Phase.Swing) return;
        _hitbox.EndSwing();
        _phase = Phase.Recoil;
        _timer = 0f;
        _directionLocked = true;
        _animator.PlayMeleeRecoil(secondsBeforeCurrentPose);
    }

    private void FinishSwing()
    {
        if (_phase != Phase.Swing) return;
        _hitbox.EndSwing();
        EnterCooldown();
    }

    private void EnterCooldown()
    {
        _phase = Phase.Cooldown;
        _timer = 0f;
        _directionLocked = false;
    }

    private bool TargetInRange() => _target != null &&
        Vector3.Distance(_transform.position, _target.position) <= _config.AttackRangeMin;

    private void UpdateTarget()
    {
        _target = null;
        foreach (var sensor in _sensors)
        {
            if (sensor is VisualSensor visual && visual.IsActivated() && visual.Target != null)
            {
                _target = visual.Target;
                break;
            }
        }
    }
}
