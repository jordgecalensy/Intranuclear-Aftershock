using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(ElevatorController))]
public class ElevatorMoveSound : MonoBehaviour
{
    [Header("FMOD Events")]
    [SerializeField] private EventReference _startShot; // one-shot on start
    [SerializeField] private EventReference _loop;      // loop while moving
    [SerializeField] private EventReference _stopShot;  // one-shot on stop

    private ElevatorController _controller;
    private EventInstance _loopInstance;
    private Rigidbody _rb;

    private void Awake()
    {
        _controller = GetComponent<ElevatorController>();
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _controller.OnMoveStart += HandleMoveStart;
        _controller.OnMoveStop += HandleMoveStop;
    }

    private void OnDisable()
    {
        _controller.OnMoveStart -= HandleMoveStart;
        _controller.OnMoveStop -= HandleMoveStop;

        // На всякий случай: если объект выключили в движении
        StopLoopImmediate();
    }

    private void HandleMoveStart()
    {
        // 1) стартовый шот
        if (!_startShot.IsNull)
        {
            // Для one-shot лучше не хранить instance — FMOD сам всё утилизирует
            RuntimeManager.PlayOneShotAttached(_startShot, gameObject);
        }

        // 2) луп
        if (_loop.IsNull)
            return;

        // гарантированно не оставляем старый луп (если вдруг повторный старт)
        StopLoopImmediate();

        _loopInstance = RuntimeManager.CreateInstance(_loop);
        RuntimeManager.AttachInstanceToGameObject(_loopInstance, transform, _rb);
        _loopInstance.start();
    }

    private void HandleMoveStop()
    {
        // 1) останавливаем луп
        StopLoopAllowFade();

        // 2) шот остановки
        if (!_stopShot.IsNull)
        {
            RuntimeManager.PlayOneShotAttached(_stopShot, gameObject);
        }
    }

    private void StopLoopAllowFade()
    {
        if (!_loopInstance.isValid())
            return;

        // Если в FMOD у loop есть fade-out / AHDSR, лучше ALLOWFADEOUT
        _loopInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _loopInstance.release();
        _loopInstance.clearHandle();
    }

    private void StopLoopImmediate()
    {
        if (!_loopInstance.isValid())
            return;

        _loopInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _loopInstance.release();
        _loopInstance.clearHandle();
    }
}