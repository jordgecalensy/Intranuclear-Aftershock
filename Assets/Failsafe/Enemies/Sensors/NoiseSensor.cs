using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

/// <summary>
/// Реализация обнаружения игрока на слух
/// </summary>
public class NoiseSensor : Sensor
{
    [SerializeField]
    private float _minSoundStrength = 1;
    /// <summary>
    /// Уровень шума, для которого устанавливается максимальная сила сигнала
    /// </summary>
    [SerializeField]
    private float _maxSoundStrength = 10;
    [Inject] private SignalManager _signalManager;
    //TODO: Раскомментировать эту строку, когда враги на всех сценах будут создаваться через Спаунер
    //private List<ISignal> AudioSignals => _signalManager.PlayerNoiseChanel.GetAllActive();
    private List<ISignal> AudioSignals => SignalManager.Instance.PlayerNoiseChanel.GetAllActive();
    private ISignal _detectedSignal;

    public override Vector3? SignalSourcePosition => _detectedSignal?.SourcePosition;

    protected override float SignalInFieldOfView()
    {
        var signals = AudioSignals;
    
        // 1. Проверяем, есть ли сигналы в канале вообще
        if (signals == null || signals.Count == 0)
        {
            // Debug.Log("Сигналов шума нет в SignalManager");
            return 0;
        }

        ISignal maxAudioSignal = null;
        float maxDetectedStrength = 0;

        for (int i = 0; i < signals.Count; i++)
        {
            ISignal signal = signals[i];
        
            // 2. Проверяем базовую силу сигнала
            if (signal.SignalStrength < _minSoundStrength) {
                Debug.Log($"Сигнал слишком тихий: {signal.SignalStrength} < {_minSoundStrength}");
                continue;
            }

            float detectedSoundStrength = CalculateSignalStrength(signal);
        
            // 3. Проверяем силу с учетом расстояния
            if (detectedSoundStrength < _minSoundStrength) {
                Debug.Log($"Сигнал затух на расстоянии: {detectedSoundStrength}");
                continue;
            }

            if (detectedSoundStrength > maxDetectedStrength)
            {
                maxAudioSignal = signal;
                maxDetectedStrength = detectedSoundStrength;
            }
        }

        _detectedSignal = maxAudioSignal;
    
        float finalSignal = Mathf.Clamp(maxDetectedStrength / _maxSoundStrength, 0f, 1f);
        if (finalSignal > 0) Debug.Log($"Сенсор услышал шум! Сила: {finalSignal}");
    
        return finalSignal;
    }
    public override bool SignalInAttackRay(Vector3 targetPosition)
    {
        return false;
    }

    private float CalculateSignalStrength(ISignal signal)
    {
        var distanceToSignal = Vector3.Distance(transform.position, signal.SourcePosition);

        if (distanceToSignal > Distance)
        {
            // Если сигнал за пределами зоны слышимости, то громкость сигнала уменьшается от расстояния
            var effectiveDistance = distanceToSignal - Distance;
            var detectedSoundStrength = signal.SignalStrength / effectiveDistance;
            return detectedSoundStrength;
        }
        // Если сигнал в пределах зоны слышимости то обычная сила сигнала
        // Возможно нужно поменять формулу, чтобы сигналы ближе к сенсору казались громче
        return signal.SignalStrength;
    }

    public void SetMinMaxStrength(float minStrength,  float maxStrength)
    {
        _minSoundStrength = minStrength;
        _maxSoundStrength = maxStrength;
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Distance);
    }
}
