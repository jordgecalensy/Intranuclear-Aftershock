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
    private bool _stepActive;
    private Vector3 _previousStepPosition;
    private float _stepDistanceMoved;
    private float _previousStoppingDistance;

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

    private float CombatRange => Mathf.Max(_config.AttackRangeMin, _config.AttackRangeMax);

    public bool PlayerOutOfAttackRange() => _phase == Phase.Delay && !TargetInRange();

    public override void Enter()
    {
        base.Enter();
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
        if (_config.AttackDelay <= 0f && TargetInRange())
            StartSwing();
        else
            _movement.Stop();
    }

    public override void Update()
    {
        UpdateTarget();
        if (!_directionLocked && _target != null)
            _movement.LookAt(_target.position);

        // После запуска клипа выход игрока из радиуса означает промах, а не отмену взмаха.
        if (_phase == Phase.Swing)
        {
            UpdateStep();
            return;
        }
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
        if (TargetInRange() && _timer >= _config.AttackDelay)
            StartSwing();
    }

    public override void Exit()
    {
        StopStep();
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
        StopStep();
        _hitbox.OpenWindow();
    }

    private void CloseHitWindow()
    {
        if (_phase == Phase.Swing) _hitbox.CloseWindow();
    }

    private void Recoil(float secondsBeforeCurrentPose)
    {
        if (_phase != Phase.Swing) return;
        StopStep();
        _hitbox.EndSwing();
        _phase = Phase.Recoil;
        _timer = 0f;
        _directionLocked = true;
        _animator.PlayMeleeRecoil(secondsBeforeCurrentPose);
    }

    private void FinishSwing()
    {
        if (_phase != Phase.Swing) return;
        StopStep();
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
        Vector3.Distance(_transform.position, _target.position) <= CombatRange;

    private void StartSwing()
    {
        _phase = Phase.Swing;
        _directionLocked = false;
        _hitbox.BeginSwing();
        _animator.PlayAttackTrigger();

        if (_config.MeleeStepDistance <= 0f || _config.MeleeStepSpeed <= 0f)
        {
            _movement.Stop();
            return;
        }

        _previousStepPosition = _transform.position;
        _stepDistanceMoved = 0f;
        _previousStoppingDistance = _movement.StoppingDistance;
        _movement.StoppingDistance = Mathf.Clamp(
            _config.MeleeStepStoppingDistance, 0f, _config.AttackRangeMin);
        _stepActive = true;
        UpdateStep();
    }

    private void UpdateStep()
    {
        if (!_stepActive) return;

        _stepDistanceMoved += Vector3.Distance(_previousStepPosition, _transform.position);
        _previousStepPosition = _transform.position;
        float remainingDistance = _config.MeleeStepDistance - _stepDistanceMoved;
        if (_target == null || remainingDistance <= 0f)
        {
            StopStep();
            return;
        }

        // Обновляем путь и вблизи игрока: если он отступит в этом же замахе,
        // агент снова пойдёт к нему без отмены атаки.
        float speed = Mathf.Min(_config.MeleeStepSpeed,
            remainingDistance / Mathf.Max(0.001f, Time.deltaTime));
        _movement.MoveTo(_target.position, speed);
    }

    private void StopStep()
    {
        _movement.Stop();
        if (!_stepActive) return;

        _movement.StoppingDistance = _previousStoppingDistance;
        _stepActive = false;
    }

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
