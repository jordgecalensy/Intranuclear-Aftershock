using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;
using UnityEngine.AI;

public class AttackState : BehaviorState
{
    private enum Phase { Delay, Attack, Reload }
    
    // Ссылки на компоненты
    private Sensor[] _sensors;
    private Transform _transform;
    private Transform _target;
    private Transform _targetPoint;
    private Enemy_ScriptableObject _enemyConfig;
    private EnemyMovement _movement;
    private EnemyAnimator _enemyAnimator;
    
    // Новая главная ссылка
    private WeaponController _weaponController; 

    // Переменные состояния
    private DamageableComponent _targetDamageable;
    private bool _hasLOSThisFrame;
    private bool _targetPointLocked;
    private Phase _phase = Phase.Delay;
    private float _phaseTimer = 0f;
    private float _distanceToPlayer;

    // Конструктор (Обратите внимание: я убрал laserOrigin и префабы, они теперь в WeaponController)
    public AttackState(Sensor[] sensors, Transform currentTransform, EnemyMovement movement,
        EnemyAnimator enemyAnimator, Enemy_ScriptableObject enemyConfig)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _enemyConfig = enemyConfig;
    }

    public bool PlayerOutOfAttackRange()
    {
        return _phase == Phase.Delay && (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax);
    }

    public override void Enter()
    {
        base.Enter();
        _movement.Stop(); // Останавливаем движение при атаке
        
        // Получаем компонент оружия (он должен висеть на том же объекте)
        _weaponController = _transform.GetComponent<WeaponController>();
        if (_weaponController == null)
        {
            Debug.LogError($"На враге {_transform.name} нет компонента WeaponController! Атака невозможна.");
        }

        ResetTargetLock();
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _enemyAnimator.ClearCombat();
    }

    public override void Update()
    {
        _phaseTimer += Time.deltaTime;
        _hasLOSThisFrame = false;

        UpdateTargetFromSensors();

        // --- ЛОГИКА ПЕРЕЗАРЯДКИ ---
        // Если контроллер сам перезаряжается (кончились патроны), мы просто смотрим на врага
        if (_weaponController != null && _weaponController.IsReloading)
        {
            if (_targetPoint != null) _movement.LookAt(_targetPoint.position);
            _enemyAnimator.SetReloading(true); // Синхронизируем анимацию, если нужно
            return;
        }

        // Если цель потеряна или далеко — выходим
        if (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax)
        {
            CancelCombatToDelay();
            return;
        }

        // Поворачиваемся всем телом к цели
        _movement.LookAt(_targetPoint.position);

        switch (_phase)
        {
            case Phase.Delay:
                _enemyAnimator.ClearCombat();
                if (_phaseTimer >= _enemyConfig.AttackDelay) EnterAttackPhase();
                break;

            case Phase.Attack:
                _enemyAnimator.SetAttacking(true);
                
                // ГЛАВНОЕ ИЗМЕНЕНИЕ: Просто просим контроллер выстрелить
                // Он сам разберется: лазер это или пуля, и сам повернет AimPivot вертикально
                if (_weaponController != null)
                {
                    _weaponController.TryShoot(_targetPoint.position);
                }

                if (_phaseTimer >= _enemyConfig.AttackDuration) EnterReloadPhase();
                break;

            case Phase.Reload:
                _enemyAnimator.SetReloading(true);
                
                // В этой фазе мы прекращаем огонь
                if (_weaponController != null) _weaponController.StopShooting();

                if (_phaseTimer >= _enemyConfig.AttackCooldown) EnterDelayPhase();
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        // Гарантированно выключаем стрельбу (лазер) при выходе из стейта
        if (_weaponController != null) _weaponController.StopShooting();
        
        _enemyAnimator.ClearCombat();
        ResetTargetLock();
        _target = null;
    }

    private void EnterAttackPhase()
    {
        _phase = Phase.Attack;
        _phaseTimer = 0f;
        _enemyAnimator.SetAttacking(true);
    }

    private void EnterReloadPhase()
    {
        _phase = Phase.Reload;
        _phaseTimer = 0f;
        _enemyAnimator.SetReloading(true);
        if (_weaponController != null) _weaponController.StopShooting();
        ResetTargetLock();
    }

    private void EnterDelayPhase()
    {
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _enemyAnimator.ClearCombat();
        ResetTargetLock();
    }

    private void CancelCombatToDelay()
    {
        if (_weaponController != null) _weaponController.StopShooting();
        _enemyAnimator.ClearCombat();

        _phase = Phase.Delay;
        _phaseTimer = 0f;

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
            ResetTargetLock();
            _distanceToPlayer = float.PositiveInfinity;
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
            return;
        }

        _distanceToPlayer = Vector3.Distance(_transform.position, _targetPoint.position);

        if (visual.SignalInAttackRay(_targetPoint.position))
            _hasLOSThisFrame = true;
    }

    private void ResetTargetLock()
    {
        _targetPointLocked = false;
        _targetPoint = null;
        _targetDamageable = null;
    }
}