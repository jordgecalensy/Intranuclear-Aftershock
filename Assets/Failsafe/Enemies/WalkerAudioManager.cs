using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class WalkerAudioManager : EnemyAudioManagerBase
{
    [System.Serializable]
    public struct WalkerMovementData { public EventReference Servo; public EventReference Step; }
    
    [System.Serializable]
    public struct WalkerIdleData { public EventReference ServoStart; public EventReference Servo; public EventReference Foley; public EventReference ScannerLoop; }
    
    [System.Serializable]
    public struct WalkerCombatData { public EventReference Accumulate; public EventReference LaserShotLoop; public EventReference CollisionLoop; public EventReference Overheat; public EventReference Reboot; }
    
    [System.Serializable]
    public struct WalkerVoiceData { public EventReference StateVoice; public EventReference DamageVoice; public EventReference Death; }

    [Header("=== ЗВУКИ ВОЛКЕРА ===")]
    [SerializeField] private WalkerMovementData _movement;
    [SerializeField] private WalkerIdleData _idle;
    [SerializeField] private WalkerCombatData _combat;
    [SerializeField] private WalkerVoiceData _voice;

    private PARAMETER_ID _paramOverheatId;
    private const string P_OVERHEAT = "Overheat_Value";

    // Лупы (зацикленные звуки)
    private EventInstance _scannerLoop;
    private EventInstance _laserLoop;
    private EventInstance _collisionLoop;

    public override void Initialize()
    {
        base.Initialize(); // Инициализируем базу
        
        if (RuntimeManager.IsInitialized)
        {
            RuntimeManager.StudioSystem.getParameterDescriptionByName(P_OVERHEAT, out var heatDesc);
            _paramOverheatId = heatDesc.id;
        }
    }

    // --- Реализация Базовых Методов ---
    
    public override void PlayStateVoice(int stateIndex)
    {
        PlayOneShotAttached(_voice.StateVoice, (instance) => { instance.setParameterByID(_paramEnemyStateId, (float)stateIndex); });
    }
    
    public override void PlayDamageVoice() => PlayOneShotAttached(_voice.DamageVoice);
    
    public override void PlayDeathSound()
    {
        StopAllLoops();
        PlayOneShotAttached(_voice.Death);
    }

    // --- Анимационные Ивенты (Вызывать из Animation Window) ---

    public void WalkerStep()
    {
        float speed = _agent != null ? _agent.velocity.magnitude : 0f;
        float paramValue = speed > _runThreshold ? 1f : 0f;
        PlayOneShotAttached(_movement.Step, (inst) => inst.setParameterByID(_paramMovementStateId, paramValue));
    }
    
    public void WalkerServo() => PlayOneShotAttached(_movement.Servo);
    public void WalkerIdleServoStart() => PlayOneShotAttached(_idle.ServoStart);
    public void WalkerIdleServo() => PlayOneShotAttached(_idle.Servo);
    public void WalkerIdleFoley() => PlayOneShotAttached(_idle.Foley);
    public void WalkerLaserAccumulate() => PlayOneShotAttached(_combat.Accumulate);
    public void WalkerOverheat() => PlayOneShotAttached(_combat.Overheat);
    public void WalkerReboot() => PlayOneShotAttached(_combat.Reboot);

    // --- Логика Лазера и Сканнера (Вызывать из StateMachine) ---

    public void StartScannerLoop() { if (!IsPlaying(_scannerLoop)) { _scannerLoop = RuntimeManager.CreateInstance(_idle.ScannerLoop); RuntimeManager.AttachInstanceToGameObject(_scannerLoop, transform, _rb); _scannerLoop.start(); } }
    public void StopScannerLoop() { if (_scannerLoop.isValid()) { _scannerLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); _scannerLoop.release(); } }

    public void StartLaserLoop() { StopLaserLoop(); _laserLoop = RuntimeManager.CreateInstance(_combat.LaserShotLoop); RuntimeManager.AttachInstanceToGameObject(_laserLoop, transform, _rb); _laserLoop.start(); }
    public void UpdateLaserOverheat(float value) { if (_laserLoop.isValid()) _laserLoop.setParameterByID(_paramOverheatId, Mathf.Clamp01(value)); }
    public void StopLaserLoop() { if (_laserLoop.isValid()) { _laserLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); _laserLoop.release(); } }

    public void UpdateLaserCollision(bool isHitting, Vector3 hitPosition)
    {
        if (isHitting)
        {
            if (!IsPlaying(_collisionLoop)) { _collisionLoop = RuntimeManager.CreateInstance(_combat.CollisionLoop); _collisionLoop.start(); }
            _collisionLoop.set3DAttributes(RuntimeUtils.To3DAttributes(hitPosition));
        }
        else StopLaserCollision();
    }
    public void StopLaserCollision() { if (_collisionLoop.isValid()) { _collisionLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); _collisionLoop.release(); } }

    private void StopAllLoops()
    {
        StopScannerLoop();
        StopLaserLoop();
        StopLaserCollision();
    }

    protected override void OnDestroy()
    {
        StopAllLoops();
        base.OnDestroy();
    }
}