using UnityEngine;
using UnityEngine.AI;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
public class EnemyAudioManager : MonoBehaviour
{
    // --- Структуры данных для чистоты Инспектора ---
    [System.Serializable]
    public struct MovementData
    {
        public EventReference Step;   // Звук шага (OneShot)
        public EventReference Servo;  // Звук работы приводов (OneShot)
        public EventReference Land;   // Звук приземления (OneShot)
    }

    [System.Serializable]
    public struct CombatData
    {
        public EventReference LaserAccumulate; // Подготовка к выстрелу (OneShot)
        public EventReference LaserLoop;       // Сам луч (Loop)
        public EventReference LaserImpact;     // Звук плавления стен (Loop)
        public EventReference Overheat;        // Перегрев/Отказ (OneShot)
        public EventReference Reboot;          // Восстановление (OneShot)
    }

    [System.Serializable]
    public struct VoiceData
    {
        public EventReference StateVoice; // Реакция (OneShot)
        public EventReference Damage;     // Получение урона (OneShot)
        public EventReference Death;      // Смерть (OneShot)
    }

    [System.Serializable]
    public struct IdleData
    {
        public EventReference Scanner;    // Сканирование (Loop)
        public EventReference Foley;      // Лязг металла (OneShot)
    }

    [Header("Audio Events Configuration")]
    [SerializeField] private MovementData _movement;
    [SerializeField] private CombatData _combat;
    [SerializeField] private VoiceData _voice;
    [SerializeField] private IdleData _idle;

    [Header("Settings")]
    [Tooltip("Скорость, выше которой шаг считается бегом (для параметра FMOD)")]
    [SerializeField] private float _runThreshold = 3.5f;

    private NavMeshAgent _agent;
    private Rigidbody _rb;

    // FMOD Instances
    private EventInstance _laserLoopInstance;
    private EventInstance _impactLoopInstance;
    private EventInstance _scannerLoopInstance;

    // Cached Parameter IDs
    private PARAMETER_ID _paramMovementStateId; // 0=Walk, 1=Run
    private PARAMETER_ID _paramEnemyStateId;    // 0=Calm, 1=Alert, 2=Chase
    private PARAMETER_ID _paramOverheatId;      // 0..1

    private const string P_MOVEMENT = "Enemy_Movement_State";
    private const string P_STATE = "Enemy_state";
    private const string P_OVERHEAT = "Overheat_Value";

    public void Initialize()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        CacheParameters();
    }

    private void CacheParameters()
    {
        if (!RuntimeManager.IsInitialized) return;

        RuntimeManager.StudioSystem.getParameterDescriptionByName(P_MOVEMENT, out var moveDesc);
        _paramMovementStateId = moveDesc.id;

        RuntimeManager.StudioSystem.getParameterDescriptionByName(P_STATE, out var stateDesc);
        _paramEnemyStateId = stateDesc.id;

        RuntimeManager.StudioSystem.getParameterDescriptionByName(P_OVERHEAT, out var heatDesc);
        _paramOverheatId = heatDesc.id;
    }

    private void OnDestroy()
    {
        StopLaserLoop();
        StopImpactLoop();
        StopScannerLoop();
    }

    // ========================================================================
    // METHODS FOR ANIMATION EVENTS (Вызываются из окна Animation)
    // ========================================================================

    /// <summary>
    /// Anim Event: Момент касания ногой земли.
    /// Автоматически определяет Walk/Run на основе скорости агента.
    /// </summary>
    public void StepEvent()
    {
        if (_movement.Step.IsNull) return;

        // Если агент не существует или стоит - используем 0, иначе проверяем скорость
        float currentSpeed = _agent != null ? _agent.velocity.magnitude : 0f;
        float paramValue = currentSpeed > _runThreshold ? 1f : 0f;

        PlayOneShotAttached(_movement.Step, (instance) =>
        {
            instance.setParameterByID(_paramMovementStateId, paramValue);
        });
    }

    /// <summary>
    /// Anim Event: Момент приземления после прыжка.
    /// </summary>
    public void LandEvent() => PlayOneShotAttached(_movement.Land);

    /// <summary>
    /// Anim Event: Начало анимации атаки (подготовка/зарядка).
    /// </summary>
    public void ShootStartEvent() => PlayOneShotAttached(_combat.LaserAccumulate);

    /// <summary>
    /// Anim Event: Момент, когда робот закончил перезарядку/встал в стойку.
    /// </summary>
    public void ReloadEvent() => PlayOneShotAttached(_combat.Reboot);

    /// <summary>
    /// Anim Event: Начало падения при смерти.
    /// </summary>
    public void DeathEvent()
    {
        StopLaserLoop();
        StopImpactLoop();
        StopScannerLoop();
        PlayOneShotAttached(_voice.Death);
    }

    // ========================================================================
    // PUBLIC API (Вызываются из StateMachine)
    // ========================================================================

    public void PlayStateVoice(int stateIndex)
    {
        PlayOneShotAttached(_voice.StateVoice, (instance) =>
        {
            instance.setParameterByID(_paramEnemyStateId, (float)stateIndex);
        });
    }

    public void PlayDamageVoice() => PlayOneShotAttached(_voice.Damage);

    public void PlayOverheat() => PlayOneShotAttached(_combat.Overheat);

    // --- LASER LOGIC ---

    public void StartLaserLoop()
    {
        // Ядерная защита от двойного запуска
        StopLaserLoop();

        _laserLoopInstance = RuntimeManager.CreateInstance(_combat.LaserLoop);
        RuntimeManager.AttachInstanceToGameObject(_laserLoopInstance, transform, _rb);
        _laserLoopInstance.start();
    }

    public void UpdateLaserOverheat(float value)
    {
        if (_laserLoopInstance.isValid())
        {
            _laserLoopInstance.setParameterByID(_paramOverheatId, Mathf.Clamp01(value));
        }
    }

    public void StopLaserLoop()
    {
        if (_laserLoopInstance.isValid())
        {
            _laserLoopInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _laserLoopInstance.release();
            _laserLoopInstance.clearHandle();
        }
    }

    // --- IMPACT LOGIC ---

    public void UpdateImpactLoop(bool isHitting, Vector3 hitPosition)
    {
        if (_combat.LaserImpact.IsNull) return;

        if (isHitting)
        {
            if (!IsPlaying(_impactLoopInstance))
            {
                _impactLoopInstance = RuntimeManager.CreateInstance(_combat.LaserImpact);
                _impactLoopInstance.start();
            }
            // Двигаем звук в точку попадания
            _impactLoopInstance.set3DAttributes(RuntimeUtils.To3DAttributes(hitPosition));
        }
        else
        {
            StopImpactLoop();
        }
    }

    public void StopImpactLoop()
    {
        if (IsPlaying(_impactLoopInstance))
        {
            _impactLoopInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _impactLoopInstance.release();
            _impactLoopInstance.clearHandle();
        }
    }

    // --- SCANNER LOGIC ---

    public void StartScannerLoop()
    {
        if (IsPlaying(_scannerLoopInstance)) return;
        
        _scannerLoopInstance = RuntimeManager.CreateInstance(_idle.Scanner);
        RuntimeManager.AttachInstanceToGameObject(_scannerLoopInstance, transform, _rb);
        _scannerLoopInstance.start();
    }

    public void StopScannerLoop()
    {
        if (_scannerLoopInstance.isValid())
        {
            _scannerLoopInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _scannerLoopInstance.release();
            _scannerLoopInstance.clearHandle();
        }
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private void PlayOneShotAttached(EventReference eventRef, System.Action<EventInstance> configure = null)
    {
        if (eventRef.IsNull) return;

        EventInstance instance = RuntimeManager.CreateInstance(eventRef);
        RuntimeManager.AttachInstanceToGameObject(instance, transform, _rb);
        configure?.Invoke(instance);
        instance.start();
        instance.release();
    }

    private bool IsPlaying(EventInstance instance)
    {
        if (!instance.isValid()) return false;
        instance.getPlaybackState(out var state);
        return state != PLAYBACK_STATE.STOPPED;
    }
}