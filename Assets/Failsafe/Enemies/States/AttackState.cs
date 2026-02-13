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

    private EnemyMovement _movement;
    private EnemyAnimator _enemyAnimator;
    private EnemyAudioManager _audio; // Аудио менеджер

    private float _distanceToPlayer;
    private LaserBeamController _activeLaser;
    private GameObject _laserPrefab;
    private GameObject _laserProjectilePrefab;
    private Transform _laserOrigin;

    private DamageableComponent _targetDamageable;
    private bool _hasLOSThisFrame;
    private bool _targetPointLocked;
    
    // Защита
    private float _losLostTimer; 
    private const float LOS_GRACE_TIME = 1.0f; 

    // Таймер звука удара
    private float _impactSoundTimer = 0f;
    private const float IMPACT_SOUND_INTERVAL = 0.1f;

    private Phase _phase = Phase.Delay;
    private float _phaseTimer = 0f;
    private bool _attackSpawned = false;

    // Конструктор
    public AttackState(Sensor[] sensors, Transform currentTransform, EnemyMovement movement,
        EnemyAnimator enemyAnimator, Transform laserOrigin, Enemy_ScriptableObject enemyConfig, 
        EnemyAudioManager audioManager)
    {
        _sensors = sensors;
        _transform = currentTransform;
        _movement = movement;
        _enemyAnimator = enemyAnimator;
        _enemyConfig = enemyConfig;
        _laserOrigin = laserOrigin;
        _audio = audioManager;

        if (_enemyConfig != null)
        {
            _laserPrefab = _enemyConfig._laserVfxPrefab != null ? _enemyConfig._laserVfxPrefab.gameObject : null;
            _laserProjectilePrefab = _enemyConfig._laserProjectilePrefab;
        }
    }

    public bool PlayerOutOfAttackRange()
    {
        if (_phase == Phase.Attack || _phase == Phase.Reload) return false;
        return _targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax;
    }

    public override void Enter()
    {
        base.Enter();
        _movement.Stop();
        ResetTargetLock();
        DestroyLaserIfAny();
        
        _audio.StopLaserLoop(); // Гарантированный стоп при входе

        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _losLostTimer = 0f;
        _impactSoundTimer = 0f;
        _attackSpawned = false;
        _enemyAnimator.ClearCombat();
    }

    public override void Update()
    {
        _phaseTimer += Time.deltaTime;
        _hasLOSThisFrame = false;

        UpdateTargetFromSensors();

        // Если цель потеряна
        if (_targetPoint == null || _distanceToPlayer > _enemyConfig.AttackRangeMax)
        {
            if (_phase == Phase.Attack)
            {
                // Если убежал недалеко, продолжаем стрелять "вслепую", если далеко - перезарядка
                if (_distanceToPlayer > _enemyConfig.AttackRangeMax * 1.2f) 
                {
                    ForceToReload();
                    return;
                }
            }
            else
            {
                CancelCombatToDelay();
                return;
            }
        }

        if (_targetPoint != null)
        {
            _movement.LookAt(_targetPoint.position);
        }

        switch (_phase)
        {
            case Phase.Delay:
                _enemyAnimator.ClearCombat();
                if (_phaseTimer >= _enemyConfig.AttackDelay) EnterAttackPhase();
                break;

            case Phase.Attack:
                _enemyAnimator.SetAttacking(true);
                
                if (!_attackSpawned) 
                { 
                    SpawnAttackOnce(); 
                    _attackSpawned = true; 
                    // Старт лазера
                    if (_enemyConfig.attackType == Enemy_ScriptableObject.AttackType.LaserBeam)
                        _audio.StartLaserLoop();
                }
                
                TickLaserLogic();
                
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
        // Сначала глушим всё
        _audio.StopLaserLoop();
        _audio.StopImpactLoop(); // <-- Глушим импакт при выходе
        
        base.Exit();
        DestroyLaserIfAny();
        _enemyAnimator.ClearCombat();
        ResetTargetLock();
        _target = null;
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
        
        _audio.StopLaserLoop();
        _audio.StopImpactLoop(); // <-- Глушим импакт при перезарядке
        _audio.PlayOverheat();
        
        _enemyAnimator.SetReloading(true);
    }

    private void EnterDelayPhase()
    {
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _attackSpawned = false;
        _enemyAnimator.ClearCombat();
        
        
        ResetTargetLock(); 
    }

    private void ForceToReload()
    {
        if (_phase != Phase.Reload) EnterReloadPhase();
    }

    private void CancelCombatToDelay()
    {
        DestroyLaserIfAny();
        
        _audio.StopLaserLoop();
        _audio.StopImpactLoop(); // <-- Глушим импакт при отмене
        
        _enemyAnimator.ClearCombat();
        _phase = Phase.Delay;
        _phaseTimer = 0f;
        _attackSpawned = false;
        ResetTargetLock();
    }
    private void TickLaserLogic()
    {
        if (_enemyConfig.attackType != Enemy_ScriptableObject.AttackType.LaserBeam) return;
        if (_activeLaser == null) return;
        
        // Питч
        float overheat = Mathf.Clamp01(_phaseTimer / _enemyConfig.AttackDuration);
        _audio.UpdateLaserOverheat(overheat);

        // --- ЛОГИКА ИМПАКТА (Raycast каждый кадр) ---
        // Raycast дешев, делать его каждый кадр для одного врага — нормально.
        if (_targetPoint != null)
        {
            Vector3 direction = (_targetPoint.position - _laserOrigin.position).normalized;
            // Raycast чуть дальше макс дистанции, чтобы ловить стены
            if (Physics.Raycast(_laserOrigin.position, direction, out RaycastHit hit, _enemyConfig.AttackRangeMax * 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Сообщаем менеджеру: "Мы попали в точку hit.point, играй/двигай звук"
                _audio.UpdateImpactLoop(true, hit.point);
            }
            else
            {
                // Сообщаем менеджеру: "Мы стреляем в небо, выключи звук попадания"
                _audio.UpdateImpactLoop(false, Vector3.zero);
            }
        }
        else
        {
            _audio.UpdateImpactLoop(false, Vector3.zero);
        }

        // Урон
        if (_targetDamageable != null && _hasLOSThisFrame)
        {
            _targetDamageable.TakeDamage(new FlatDamage(_enemyConfig.Damage * Time.deltaTime));
        }
    }

    
    private void SpawnAttackOnce()
    {
         switch (_enemyConfig.attackType)
        {
            case Enemy_ScriptableObject.AttackType.LaserBeam:
                if (_activeLaser != null) return;
                if (_laserPrefab == null || _laserOrigin == null || _targetPoint == null) return;

                var laserGO = GameObject.Instantiate(_laserPrefab, _laserOrigin.position, _laserOrigin.rotation);
                _activeLaser = laserGO.GetComponent<LaserBeamController>();
                if (_activeLaser != null)
                    _activeLaser.Initialize(_laserOrigin, _targetPoint);
                else
                    GameObject.Destroy(laserGO);
                break;
            // ... projectile case ...
             case Enemy_ScriptableObject.AttackType.Projectile:
                if (_laserProjectilePrefab == null || _laserOrigin == null || _targetPoint == null) return;
                var projectileGO = GameObject.Instantiate(_laserProjectilePrefab, _laserOrigin.position, Quaternion.identity);
                var projectile = projectileGO.GetComponent<LaserProjectile>();
                if (projectile != null)
                    projectile.Initialize((_targetPoint.position - _laserOrigin.position).normalized);
                break;
        }
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
    
    // ... (UpdateTargetFromSensors) ...
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
            _losLostTimer += Time.deltaTime;
            if (_losLostTimer > LOS_GRACE_TIME)
            {
                _target = null;
                _targetPoint = null;
                _distanceToPlayer = float.PositiveInfinity;
                _hasLOSThisFrame = false;
            }
            return;
        }
        _losLostTimer = 0f;
        _target = visual.Target;
        if (!_targetPointLocked)
        {
            _targetPoint = visual.GetBestVisiblePointWithChestOverride();
            _targetPointLocked = _targetPoint != null;
            _targetDamageable = _target != null ? _target.GetComponentInChildren<DamageableComponent>() : null;
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
}