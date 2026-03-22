using UnityEngine;
using UnityEngine.AI;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
public abstract class EnemyAudioManagerBase : MonoBehaviour
{
    [Header("Базовые настройки (Base)")]
    [Tooltip("Скорость, выше которой шаг считается бегом")]
    [SerializeField] protected float _runThreshold = 3.5f;

    protected NavMeshAgent _agent;
    protected Rigidbody _rb;

    // Общие параметры FMOD
    protected PARAMETER_ID _paramMovementStateId;
    protected PARAMETER_ID _paramEnemyStateId;

    protected const string P_MOVEMENT = "Enemy_Movement_State";
    protected const string P_STATE = "Enemy_state";

    public virtual void Initialize()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();
        CacheBaseParameters();
    }

    protected virtual void CacheBaseParameters()
    {
        if (!RuntimeManager.IsInitialized) return;

        RuntimeManager.StudioSystem.getParameterDescriptionByName(P_MOVEMENT, out var moveDesc);
        _paramMovementStateId = moveDesc.id;

        RuntimeManager.StudioSystem.getParameterDescriptionByName(P_STATE, out var stateDesc);
        _paramEnemyStateId = stateDesc.id;
    }

    // ==========================================
    // ПУБЛИЧНОЕ API (Должны реализовать все наследники)
    // ==========================================
    
    public abstract void PlayStateVoice(int stateIndex);
    public abstract void PlayDamageVoice();
    public abstract void PlayDeathSound();

    // ==========================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (Доступны всем)
    // ==========================================
    
    protected void PlayOneShotAttached(EventReference eventRef, System.Action<EventInstance> configure = null)
    {
        if (eventRef.IsNull) return;
        EventInstance instance = RuntimeManager.CreateInstance(eventRef);
        RuntimeManager.AttachInstanceToGameObject(instance, transform, _rb);
        configure?.Invoke(instance);
        instance.start();
        instance.release();
    }

    protected bool IsPlaying(EventInstance instance)
    {
        if (!instance.isValid()) return false;
        instance.getPlaybackState(out var state);
        return state != PLAYBACK_STATE.STOPPED;
    }

    protected virtual void OnDestroy() { }
}