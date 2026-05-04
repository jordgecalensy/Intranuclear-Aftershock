using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(StudioEventEmitter))]
public class SendAudioOcclusion : MonoBehaviour
{
    [Header("FMOD Event Emmiter")]
    [SerializeField] StudioEventEmitter _emitter;

    [Header("Occlusion Settings")]
    [SerializeField, Range(0f, 10f)] private float _soundSpread = 1.8f;
    [SerializeField, Range(0f, 10f)] private float _listenerSpread = 1.8f;
    [SerializeField] private LayerMask _occlusionLayer;

    [Header("Performance")]
    [SerializeField] private float _checkInterval = 0.1f;
    [SerializeField] private int _raycastCount = 9; // опционально: 5, 9, 13 лучей

    [Header("FMOD Parameters")]
    [SerializeField] private string _occlusionParam = "Occlusion";
    [SerializeField, Range(0f, 1f)] private float _maxOcclusionValue = 1f;
    
    private EventInstance _eventInstance;
    private EventDescription _eventDesc;
    private StudioListener _listener;
    private float _maxDistance;
    private float _minDistance;
    private float _nextCheckTime;
    private int _totalRays;

    // Кэшированные точки для лучей (чтобы не считать каждый кадр)
    private Vector3[] _soundOffsets;
    private Vector3[] _listenerOffsets;

    private void Start()
    {

        _eventInstance = _emitter.EventInstance;
        _eventDesc = _emitter.EventDescription; 

        // Получаем описание для чтения настроек
        _eventDesc.getMinMaxDistance(out _minDistance, out _maxDistance);

        // Поиск слушателя
        _listener = FindObjectOfType<StudioListener>();


        // Предварительный расчёт точек для лучей
        PrecomputeRayOffsets();

        _totalRays = _raycastCount;
        _nextCheckTime = Time.time;
    }

    private void PrecomputeRayOffsets()
    {
        // Генерируем смещения для лучей по кругу (более равномерно, чем крест)
        _soundOffsets = GenerateCircleOffsets(_soundSpread, _raycastCount);
        _listenerOffsets = GenerateCircleOffsets(_listenerSpread, _raycastCount);
    }

    private Vector3[] GenerateCircleOffsets(float radius, int count)
    {
        var offsets = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (i * 360f / count) * Mathf.Deg2Rad;
            offsets[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                0, // Y оставляем 0 для стабильности
                Mathf.Sin(angle) * radius
            );
        }
        return offsets;
    }

    private void Update()
    {
        // Оптимизация: проверка не каждый кадр, а по таймеру
        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + _checkInterval;

        // Ранний выход: если звук виртуальный или вне дистанции
        if (!ShouldCheckOcclusion()) return;

        PerformOcclusionCheck();
    }

    private bool ShouldCheckOcclusion()
    {
        if (_eventInstance.isVirtual(out bool isVirtual) == FMOD.RESULT.OK && isVirtual)
            return false;

        _eventInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING) return false;

        float distance = Vector3.Distance(transform.position, _listener.transform.position);
        return distance <= _maxDistance;
    }

    private void PerformOcclusionCheck()
    {
        int occludedRays = 0;
        Vector3 soundPos = transform.position;
        Vector3 listenerPos = _listener.transform.position;
        Vector3 direction = (listenerPos - soundPos).normalized;

        // Центральный луч + лучи по периметру
        for (int i = 0; i < _totalRays; i++)
        {
            Vector3 start = soundPos + _soundOffsets[i];
            Vector3 end = listenerPos + _listenerOffsets[i];

            if (Physics.Linecast(start, end, _occlusionLayer))
                occludedRays++;
        }

        // Нормализуем значение: 0.0 (нет окклюзии) → 1.0 (полная)
        float occlusionValue = Mathf.Clamp01((float)occludedRays / _totalRays);

        // Применяем параметр в FMOD
        _eventInstance.setParameterByName(_occlusionParam, occlusionValue * _maxOcclusionValue);
        Debug.Log(occlusionValue);

        // Отладка: визуализация лучей (только в Editor)
#if UNITY_EDITOR
        DebugOcclusionRays(soundPos, listenerPos, occludedRays);
#endif
    }

#if UNITY_EDITOR
    private void DebugOcclusionRays(Vector3 start, Vector3 end, int occludedCount)
    {
        for (int i = 0; i < _totalRays; i++)
        {
            Vector3 s = start + _soundOffsets[i];
            Vector3 e = end + _listenerOffsets[i];
            bool hit = Physics.Linecast(s, e, _occlusionLayer);
            Debug.DrawLine(s, e, hit ? Color.red : Color.green, _checkInterval);
        }
    }
#endif

}