using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SpiderAudioManager : EnemyAudioManagerBase
{
    [System.Serializable] public struct SpiderMovementData { public EventReference Step; public EventReference Servo; public EventReference Jump; public EventReference Landing; }
    [System.Serializable] public struct SpiderAnimData { public EventReference Activation1; public EventReference Activation2; public EventReference Idle1; public EventReference Idle2; public EventReference DeathFoley; }
    [System.Serializable] public struct SpiderCombatData { public EventReference FoleyPt1; public EventReference Accumulate; public EventReference Shot; public EventReference FoleyPt2; }
    [System.Serializable] public struct SpiderVoiceData { public EventReference VoiceLoop; public EventReference FanNoiseLoop; public EventReference TakeDamage; public EventReference DeathVoice; }

    [Header("=== ЗВУКИ ПАУКА ===")]
    [SerializeField] private SpiderMovementData _movement;
    [SerializeField] private SpiderAnimData _anims;
    [SerializeField] private SpiderCombatData _combat;
    [SerializeField] private SpiderVoiceData _voice;

    private EventInstance _spiderVoiceLoop;
    private EventInstance _spiderFanLoop;

    public override void Initialize()
    {
        base.Initialize(); // Инициализируем базу

        // Паук сразу включает фоновые системы при рождении
        StartSpiderLoops();
    }

    // --- Реализация Базовых Методов ---

    public override void PlayStateVoice(int stateIndex)
    {
        // У паука параметр меняет постоянный гул и голос, а не вызывает разовый крик
        if (_spiderVoiceLoop.isValid()) _spiderVoiceLoop.setParameterByID(_paramEnemyStateId, (float)stateIndex);
        if (_spiderFanLoop.isValid()) _spiderFanLoop.setParameterByID(_paramEnemyStateId, (float)stateIndex);
    }

    public override void PlayDamageVoice() => PlayOneShotAttached(_voice.TakeDamage);

    public override void PlayDeathSound()
    {
        StopSpiderLoops();
        PlayOneShotAttached(_voice.DeathVoice);
        PlayOneShotAttached(_anims.DeathFoley);
    }

    // --- Анимационные Ивенты (Вызывать из Animation Window) ---

    public void SpiderStep()
    {
        float speed = _agent != null ? _agent.velocity.magnitude : 0f;
        float paramValue = speed > _runThreshold ? 1f : 0f;
        PlayOneShotAttached(_movement.Step, (inst) => inst.setParameterByID(_paramMovementStateId, paramValue));
    }

    public void SpiderServo() => PlayOneShotAttached(_movement.Servo);
    public void SpiderJump() => PlayOneShotAttached(_movement.Jump);
    public void SpiderLanding() => PlayOneShotAttached(_movement.Landing);
    
    public void SpiderActivation1() => PlayOneShotAttached(_anims.Activation1);
    public void SpiderActivation2() => PlayOneShotAttached(_anims.Activation2);
    public void SpiderIdle1() => PlayOneShotAttached(_anims.Idle1);
    public void SpiderIdle2() => PlayOneShotAttached(_anims.Idle2);

    public void SpiderShootFoley1() => PlayOneShotAttached(_combat.FoleyPt1);
    public void SpiderShootAccumulate() => PlayOneShotAttached(_combat.Accumulate);
    public void SpiderShootFire() => PlayOneShotAttached(_combat.Shot);
    public void SpiderShootFoley2() => PlayOneShotAttached(_combat.FoleyPt2);

    // --- Внутренняя логика ---

    private void StartSpiderLoops()
    {
        if (!_voice.VoiceLoop.IsNull) { _spiderVoiceLoop = RuntimeManager.CreateInstance(_voice.VoiceLoop); RuntimeManager.AttachInstanceToGameObject(_spiderVoiceLoop, transform, _rb); _spiderVoiceLoop.start(); }
        if (!_voice.FanNoiseLoop.IsNull) { _spiderFanLoop = RuntimeManager.CreateInstance(_voice.FanNoiseLoop); RuntimeManager.AttachInstanceToGameObject(_spiderFanLoop, transform, _rb); _spiderFanLoop.start(); }
    }

    private void StopSpiderLoops()
    {
        if (_spiderVoiceLoop.isValid()) { _spiderVoiceLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); _spiderVoiceLoop.release(); }
        if (_spiderFanLoop.isValid()) { _spiderFanLoop.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); _spiderFanLoop.release(); }
    }

    protected override void OnDestroy()
    {
        StopSpiderLoops();
        base.OnDestroy();
    }
}