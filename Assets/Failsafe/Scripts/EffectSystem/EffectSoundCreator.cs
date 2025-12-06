using UnityEngine;
using FMODUnity;

namespace Failsafe.Scripts.EffectSystem
{
    /// <summary>
    /// Отвечает за звуки включения, выключения и фонового лупа визора.
    /// </summary>

    public class EffectSoundCreator : MonoBehaviour
    {
        [Header("FMOD Events")]
        private EventReference _visorOnEvent;
        private EventReference _visorOffEvent;
        private EventReference _visorLoopEvent;

        private StudioEventEmitter _loopEmitter;

        public void Initialize(EventReference onEvent, EventReference offEvent, EventReference loopEvent)
        {
            _visorOnEvent = onEvent;
            _visorOffEvent = offEvent;
            _visorLoopEvent = loopEvent;
        }

        private void OnEnable()
        {
            // Проигрываем звук включения
            if (_visorOnEvent.IsNull == false)
                RuntimeManager.PlayOneShot(_visorOnEvent);

            // Создаём и запускаем луп
            _loopEmitter = gameObject.AddComponent<StudioEventEmitter>();
            _loopEmitter.EventReference = _visorLoopEvent;
            _loopEmitter.Play();
        }

        private void OnDisable()
        {
            // Останавливаем луп и проигрываем выключение
            if (_loopEmitter != null)
                _loopEmitter.Stop();

            if (_visorOffEvent.IsNull == false)
                RuntimeManager.PlayOneShot(_visorOffEvent);
        }
    }
}
