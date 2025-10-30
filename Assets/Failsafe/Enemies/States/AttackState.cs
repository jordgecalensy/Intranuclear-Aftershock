using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using UnityEngine.AI;

public class AttackState : BehaviorState
{
    private Sensor[] _sensors;
    private Transform _transform;
    private Transform _target;
    private Transform _targetPoint;
    private NavMeshAgent _navMeshAgent;
    private Enemy_ScriptableObject _enemyConfig;

    private float _attackProgress = 0f;
    private bool _delayOver = false;
    private bool _onCooldown = false;
    private bool _attackFired = false;
    private bool _targetPointLocked = false;

    private EnemyNavMeshActions _enemyNavMeshActions;
    private EnemyAnimator _enemyAnimator;

    private float _distanceToPlayer;
    private LaserBeamController _activeLaser;
    private GameObject _laserPrefab;
    private GameObject _laserProjectilePrefab;
    private Transform _laserOrigin;

    // Новое: кэш и флаг видимости на кадр
    private DamageableComponent _targetDamageable;
    private bool _hasLOSThisFrame;

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

        // Берём префабы из конфига
        if (_enemyConfig != null)
        {
            _laserPrefab = _enemyConfig._laserVfxPrefab != null ? _enemyConfig._laserVfxPrefab.gameObject : null;
            _laserProjectilePrefab = _enemyConfig._laserProjectilePrefab;
        }
    }

    public bool PlayerOutOfAttackRange()
    {
        return (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax)
               && !_onCooldown && !_attackFired;
    }

    public override void Enter()
    {
        base.Enter();
        _attackProgress = 0f;
        _delayOver = false;
        _onCooldown = false;
        _attackFired = false;
        _targetPointLocked = false;
        _targetDamageable = null;
        _enemyNavMeshActions.StopMoving();
        _enemyAnimator.isAttacking();
    }

    public override void Update()
    {
        _attackProgress += Time.deltaTime;
        _hasLOSThisFrame = false; // сброс флага видимости на кадр

        if (!_delayOver && _attackProgress > _enemyConfig.AttackDelay)
        {
            _delayOver = true;
            _attackProgress = 0f;
        }

        foreach (var sensor in _sensors)
        {
            if (sensor is VisualSensor visual && visual.IsActivated())
            {
                _target = visual.Target;

                if (!_targetPointLocked)
                {
                    _targetPoint = visual.GetBestVisiblePointWithChestOverride();
                    _targetPointLocked = _targetPoint != null;

                    if (_targetPointLocked)
                    {
                        _targetDamageable = _target != null
                            ? _target.GetComponentInChildren<DamageableComponent>()
                            : null;

                        Debug.Log($"🎯 Цель зафиксирована: {_targetPoint.name}");
                    }
                }

                if (_targetPoint == null) continue;

                _distanceToPlayer = Vector3.Distance(_transform.position, _targetPoint.position);
                _enemyNavMeshActions.RotateToPoint(_targetPoint.position, 5f);

                // отмечаем наличие прямой видимости этим сенсором
                if (visual.SignalInAttackRay(_targetPoint.position))
                    _hasLOSThisFrame = true;

                if (_delayOver && !_onCooldown && !_attackFired)
                {
                    _enemyAnimator.TryAttack();

                    switch (_enemyConfig.attackType)
                    {
                        case Enemy_ScriptableObject.AttackType.LaserBeam:
                            if (_activeLaser == null)
                            {
                                if (_laserPrefab == null || _laserOrigin == null)
                                {
                                    Debug.LogError("[AttackState] Laser VFX prefab or origin is not assigned.");
                                    break;
                                }

                                var laserGO = GameObject.Instantiate(_laserPrefab, _laserOrigin.position, _laserOrigin.rotation);
                                _activeLaser = laserGO.GetComponent<LaserBeamController>();
                                if (_activeLaser != null)
                                {
                                    _activeLaser.Initialize(_laserOrigin, _targetPoint);
                                }
                                else
                                {
                                    Debug.LogError("[AttackState] Laser prefab has no LaserBeamController component.");
                                    GameObject.Destroy(laserGO);
                                }
                            }
                            break;

                        case Enemy_ScriptableObject.AttackType.Projectile:
                            if (_laserProjectilePrefab != null && _laserOrigin != null)
                            {
                                var projectileGO = GameObject.Instantiate(_laserProjectilePrefab, _laserOrigin.position, Quaternion.identity);
                                var projectile = projectileGO.GetComponent<LaserProjectile>();
                                if (projectile != null)
                                {
                                    Vector3 direction = (_targetPoint.position - _laserOrigin.position).normalized;
                                    projectile.Initialize(direction);
                                }
                            }
                            break;
                    }

                    _attackFired = true;

                    // ВАЖНО: урон от лазера больше НЕ наносим здесь единоразово.
                }
            }
        }

        // Постоянный тик урона для лазера — пока активен луч, цель в радиусе и есть LOS в этом кадре
        if (_enemyConfig.attackType == Enemy_ScriptableObject.AttackType.LaserBeam
            && _activeLaser != null
            && !_onCooldown
            && _targetPoint != null
            && _targetDamageable != null
            && _distanceToPlayer <= _enemyConfig.AttackRangeMax
            && _hasLOSThisFrame)
        {
            _targetDamageable.TakeDamage(new FlatDamage(_enemyConfig.Damage * Time.deltaTime));
        }

        // Завершение атаки по длительности
        if (_attackFired && _attackProgress > _enemyConfig.AttackDuration)
        {
            if (_enemyConfig.attackType == Enemy_ScriptableObject.AttackType.LaserBeam && _activeLaser != null)
            {
                GameObject.Destroy(_activeLaser.gameObject);
                _activeLaser = null;
            }

            _onCooldown = true;
            _enemyAnimator.TryReload();
            _enemyAnimator.isReloading(true);
        }

        // Завершение кулдауна
        if (_attackProgress > _enemyConfig.AttackDuration + _enemyConfig.AttackCooldown)
        {
            _onCooldown = false;
            _enemyAnimator.isReloading(false);
            _attackProgress = 0f;
            _attackFired = false;

            _targetPoint = null;
            _targetPointLocked = false;
            _targetDamageable = null;
        }
    }

    public override void Exit()
    {
        base.Exit();

        if (_activeLaser != null)
        {
            GameObject.Destroy(_activeLaser.gameObject);
            _activeLaser = null;
        }

        _enemyNavMeshActions.ResumeMoving();
        _targetPoint = null;
        _targetPointLocked = false;
        _targetDamageable = null;
    }
}