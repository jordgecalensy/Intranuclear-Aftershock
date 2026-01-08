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
    private Enemy_ScriptableObject _enemyConfig;

    private EnemyMovement _movement; // <-- Новый класс
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

    public AttackState(Sensor[] sensors, Transform currentTransform, EnemyMovement movement,
        EnemyAnimator enemyAnimator, Transform laserOrigin, Enemy_ScriptableObject enemyConfig)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _enemyConfig = enemyConfig;
        _laserOrigin = laserOrigin;

        if (_enemyConfig != null)
        {
            _laserPrefab = _enemyConfig._laserVfxPrefab != null ? _enemyConfig._laserVfxPrefab.gameObject : null;
            _laserProjectilePrefab = _enemyConfig._laserProjectilePrefab;
        }
    }

    public bool PlayerOutOfAttackRange()
    {
        return _phase == Phase.Delay && (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax);
    }

    public override void Enter()
    {
        base.Enter();
        _movement.Stop(); // Останавливаемся
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

        if (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax)
        {
            CancelCombatToDelay();
            return;
        }

        // Прицеливание стоя
        _movement.LookAt(_targetPoint.position);

        switch (_phase)
        {
            case Phase.Delay:
                _enemyAnimator.ClearCombat();
                if (_phaseTimer >= _enemyConfig.AttackDelay) EnterAttackPhase();
                break;

            case Phase.Attack:
                _enemyAnimator.SetAttacking(true);
                if (!_attackSpawned) { SpawnAttackOnce(); _attackSpawned = true; }
                TickLaserDamageIfNeeded();
                if (_phaseTimer >= _enemyConfig.AttackDuration) EnterReloadPhase();
                break;

            case Phase.Reload:
                _enemyAnimator.SetReloading(true);
                if (_phaseTimer >= _enemyConfig.AttackCooldown) EnterDelayPhase();
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        DestroyLaserIfAny();
        _enemyAnimator.ClearCombat();
        ResetTargetLock();
        _target = null;
    }
    /// <summary>
    /// Локальный метод для поворота стоящего агента к цели.
    /// Заменяет удаленный RotateToPoint из NavMeshActions.
    /// </summary>
    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _transform.position;
        direction.y = 0f; // Игнорируем высоту, чтобы враг не наклонялся

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // Скорость поворота можно вынести в конфиг, пока hardcoded 5f
            _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    private void EnterAttackPhase()
    {
        _phase = Phase.Attack;
        _phaseTimer = 0f;
        _attackSpawned = false;
        _enemyAnimator.SetAttacking(true);
    }

    private void EnterReloadPhase()
    {
        _phase = Phase.Reload;
        _phaseTimer = 0f;

        DestroyLaserIfAny();
        _enemyAnimator.SetReloading(true);
        
        // Сброс лока, чтобы при следующем выстреле выбрать актуальную точку
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