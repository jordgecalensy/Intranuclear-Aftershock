using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using UnityEngine.AI;

public class AttackState : BehaviorState
{
    private enum Phase { Delay, Attack, Reload }

    private Sensor[] _sensors;
    private Transform _transform;
    private Transform _target;
    private Transform _targetPoint;
    private NavMeshAgent _navMeshAgent;
    private Enemy_ScriptableObject _enemyConfig;

    private EnemyNavMeshActions _enemyNavMeshActions;
    private EnemyAnimator _enemyAnimator;

    private float _distanceToPlayer;

    private LaserBeamController _activeLaser;
    private GameObject _laserPrefab;
    private GameObject _laserProjectilePrefab;
    private Transform _laserOrigin;

    private DamageableComponent _targetDamageable;
    private bool _hasLOSThisFrame;
    private bool _targetPointLocked;

    private Phase _phase = Phase.Delay;
    private float _phaseTimer = 0f;
    private bool _attackSpawned = false;

    public AttackState(
        Sensor[] sensors,
        Transform currentTransform,
        EnemyNavMeshActions enemyNavMeshActions,
        EnemyAnimator enemyAnimator,
        Transform laserOrigin,
        Enemy_ScriptableObject enemyConfig)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _enemyNavMeshActions = enemyNavMeshActions;
        _enemyAnimator = enemyAnimator;
        _enemyConfig = enemyConfig;
        _laserOrigin = laserOrigin;
        _navMeshAgent = null;

        if (_enemyConfig != null)
        {
            _laserPrefab = _enemyConfig._laserVfxPrefab != null ? _enemyConfig._laserVfxPrefab.gameObject : null;
            _laserProjectilePrefab = _enemyConfig._laserProjectilePrefab;
        }
    }

    public bool PlayerOutOfAttackRange()
    {
        // даём стейт-машине выйти только когда мы в ожидании (Delay) и реально не можем атаковать
        return _phase == Phase.Delay && (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax);
    }

    public override void Enter()
    {
        base.Enter();

        _enemyNavMeshActions.StopMoving();

        ResetTargetLock();
        DestroyLaserIfAny();

        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _attackSpawned = false;

        _enemyAnimator.ClearCombat();
    }

    public override void Update()
    {
        _phaseTimer += Time.deltaTime;
        _hasLOSThisFrame = false;

        UpdateTargetFromSensors();

        // нет цели/точки или вне радиуса — отменяем атаку и держим locomotion
        if (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax)
        {
            CancelCombatToDelay();
            return;
        }

        _enemyNavMeshActions.RotateToPoint(_targetPoint.position, 5f);

        switch (_phase)
        {
            case Phase.Delay:
            {
                _enemyAnimator.ClearCombat();

                if (_phaseTimer >= _enemyConfig.AttackDelay)
                    EnterAttackPhase();

                break;
            }

            case Phase.Attack:
            {
                _enemyAnimator.SetAttacking(true);

                if (!_attackSpawned)
                {
                    SpawnAttackOnce();
                    _attackSpawned = true;
                }

                TickLaserDamageIfNeeded();

                if (_phaseTimer >= _enemyConfig.AttackDuration)
                    EnterReloadPhase();

                break;
            }

            case Phase.Reload:
            {
                _enemyAnimator.SetReloading(true);

                if (_phaseTimer >= _enemyConfig.AttackCooldown)
                    EnterDelayPhase();

                break;
            }
        }
    }

    public override void Exit()
    {
        base.Exit();

        DestroyLaserIfAny();
        _enemyAnimator.ClearCombat();

        _enemyNavMeshActions.ResumeMoving();

        ResetTargetLock();
        _target = null;
    }

    private void EnterAttackPhase()
    {
        _phase = Phase.Attack;
        _phaseTimer = 0f;
        _attackSpawned = false;
        // анимация включится через bool в Update (или можно сразу тут)
        _enemyAnimator.SetAttacking(true);
    }

    private void EnterReloadPhase()
    {
        _phase = Phase.Reload;
        _phaseTimer = 0f;

        // заканчиваем эффекты атаки
        DestroyLaserIfAny();

        // фиксируем reload loop
        _enemyAnimator.SetReloading(true);

        // если хочешь, чтобы каждый цикл заново искал "лучший" targetPoint
        ResetTargetLock();
    }

    private void EnterDelayPhase()
    {
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _attackSpawned = false;

        _enemyAnimator.ClearCombat();

        ResetTargetLock();
    }

    private void CancelCombatToDelay()
    {
        DestroyLaserIfAny();
        _enemyAnimator.ClearCombat();

        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _attackSpawned = false;

        ResetTargetLock();
    }

    private void UpdateTargetFromSensors()
    {
        VisualSensor visual = null;
        for (int i = 0; i < _sensors.Length; i++)
        {
            if (_sensors[i] is VisualSensor v && v.IsActivated())
            {
                visual = v;
                break;
            }
        }

        if (visual == null)
        {
            _target = null;
            _targetPoint = null;
            _distanceToPlayer = float.PositiveInfinity;
            _hasLOSThisFrame = false;
            return;
        }

        _target = visual.Target;

        if (!_targetPointLocked)
        {
            _targetPoint = visual.GetBestVisiblePointWithChestOverride();
            _targetPointLocked = _targetPoint != null;

            _targetDamageable = _target != null
                ? _target.GetComponentInChildren<DamageableComponent>()
                : null;
        }

        if (_targetPoint == null)
        {
            _distanceToPlayer = float.PositiveInfinity;
            _hasLOSThisFrame = false;
            return;
        }

        _distanceToPlayer = Vector3.Distance(_transform.position, _targetPoint.position);

        if (visual.SignalInAttackRay(_targetPoint.position))
            _hasLOSThisFrame = true;
    }

    private void SpawnAttackOnce()
    {
        switch (_enemyConfig.attackType)
        {
            case Enemy_ScriptableObject.AttackType.LaserBeam:
            {
                if (_activeLaser != null) return;
                if (_laserPrefab == null || _laserOrigin == null || _targetPoint == null) return;

                var laserGO = GameObject.Instantiate(_laserPrefab, _laserOrigin.position, _laserOrigin.rotation);
                _activeLaser = laserGO.GetComponent<LaserBeamController>();
                if (_activeLaser != null)
                    _activeLaser.Initialize(_laserOrigin, _targetPoint);
                else
                    GameObject.Destroy(laserGO);

                break;
            }

            case Enemy_ScriptableObject.AttackType.Projectile:
            {
                if (_laserProjectilePrefab == null || _laserOrigin == null || _targetPoint == null) return;

                var projectileGO = GameObject.Instantiate(_laserProjectilePrefab, _laserOrigin.position, Quaternion.identity);
                var projectile = projectileGO.GetComponent<LaserProjectile>();
                if (projectile != null)
                {
                    Vector3 direction = (_targetPoint.position - _laserOrigin.position).normalized;
                    projectile.Initialize(direction);
                }

                break;
            }
        }
    }

    private void TickLaserDamageIfNeeded()
    {
        if (_enemyConfig.attackType != Enemy_ScriptableObject.AttackType.LaserBeam) return;
        if (_activeLaser == null) return;
        if (_targetPoint == null || _targetDamageable == null) return;
        if (_distanceToPlayer > _enemyConfig.AttackRangeMax) return;
        if (!_hasLOSThisFrame) return;

        _targetDamageable.TakeDamage(new FlatDamage(_enemyConfig.Damage * Time.deltaTime));
    }

    private void DestroyLaserIfAny()
    {
        if (_activeLaser != null)
        {
            GameObject.Destroy(_activeLaser.gameObject);
            _activeLaser = null;
        }
    }

    private void ResetTargetLock()
    {
        _targetPointLocked = false;
        _targetPoint = null;
        _targetDamageable = null;
    }
}
