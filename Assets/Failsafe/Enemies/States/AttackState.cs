using Failsafe.Scripts.Damage;
using Failsafe.Scripts.Damage.Implementation;
using UnityEngine;

public class AttackState : BehaviorState
{
    private enum Phase { Delay, Attack, Cooldown } 
    
    private Sensor[] _sensors;
    private Transform _transform;
    private Transform _target;
    private Transform _targetPoint;
    private Enemy_ScriptableObject _enemyConfig;
    private EnemyMovement _movement;
    private EnemyAnimator _enemyAnimator;
    private WeaponController _weaponController; 

    private Phase _phase = Phase.Delay;
    private float _phaseTimer = 0f;
    private float _distanceToPlayer;
    private bool _wasReloading;

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
        _movement.Stop();
        
        if (_weaponController == null)
            _weaponController = _transform.GetComponent<WeaponController>();
        
        ResetTargetLock();
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        
        // Защита при входе, если пушка уже в процессе перезарядки
        if (_weaponController != null && _weaponController.IsReloading)
        {
            _wasReloading = true;
            _enemyAnimator.SetAttacking(false); 
        }
        else
        {
            _wasReloading = false;
            _enemyAnimator.StartAttacking(); 
        }
    }

    public override void Update()
    {
        // 1. ПЕРВЫМ ДЕЛОМ ВСЕГДА обновляем зрение, чтобы машина состояний не сломалась!
        UpdateTargetFromSensors();

        // 2. Узнаем, идет ли сейчас перезарядка
        bool isReloading = _weaponController != null && _weaponController.IsReloading;

        // 3. Логика во время перезарядки (стоим, крутим анимацию и смотрим)
        if (isReloading)
        {
            if (!_wasReloading) 
            {
                _enemyAnimator.StartReloading();
                _wasReloading = true;
            }
            
            // Если видим игрока, провожаем его взглядом, пока заряжаем пушку
            if (_targetPoint != null) 
                _movement.LookAt(_targetPoint.position);
            
            // Выходим отсюда, не прерывая перезарядку
            return; 
        }

        // 4. Успешно перезарядились - возвращаемся в боевую стойку
        if (_wasReloading)
        {
            _enemyAnimator.StartAttacking();
            _wasReloading = false;
        }

        // 5. Проверка потери цели (срабатывает только если мы НЕ перезаряжаемся)
        if (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax)
        {
            CancelCombatToDelay();
            return;
        }

        // 6. Обычная фаза боя
        _phaseTimer += Time.deltaTime;
        _movement.LookAt(_targetPoint.position);

        switch (_phase)
        {
            case Phase.Delay:
                if (_phaseTimer >= _enemyConfig.AttackDelay) EnterAttackPhase();
                break;

            case Phase.Attack:
                if (_weaponController != null)
                {
                    if (_weaponController.TryShoot(_targetPoint.position))
                    {
                        // Дергаем курок только для НЕ-лазерного оружия
                        if (!_weaponController.weaponStrategy.isContinuousFire)
                            _enemyAnimator.PlayAttackTrigger();
                    }
                }
                if (_phaseTimer >= _enemyConfig.AttackDuration) EnterCooldownPhase();
                break;

            case Phase.Cooldown:
                if (_phaseTimer >= _enemyConfig.AttackCooldown) EnterDelayPhase();
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (_weaponController != null) _weaponController.StopShooting();
        
        _enemyAnimator.ClearCombat(); 
        ResetTargetLock();
    }

    private void EnterAttackPhase()
    {
        _phase = Phase.Attack;
        _phaseTimer = 0f;
    }

    private void EnterCooldownPhase()
    {
        _phase = Phase.Cooldown;
        _phaseTimer = 0f;
        
        if (_weaponController != null) _weaponController.StopShooting();
        _enemyAnimator.StopAttacking(); 
    }

    private void EnterDelayPhase()
    {
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _enemyAnimator.StartAttacking(); 
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
            if (_sensors[i] is VisualSensor v && v.IsActivated()) { visual = v; break; }
        }

        if (visual == null) { ResetTargetLock(); _distanceToPlayer = float.PositiveInfinity; return; }

        _target = visual.Target;
        _targetPoint = visual.GetBestVisiblePointWithChestOverride();

        if (_targetPoint == null) { _distanceToPlayer = float.PositiveInfinity; return; }
        
        _distanceToPlayer = Vector3.Distance(_transform.position, _targetPoint.position);
    }

    private void ResetTargetLock()
    {
        _targetPoint = null; 
    }
}