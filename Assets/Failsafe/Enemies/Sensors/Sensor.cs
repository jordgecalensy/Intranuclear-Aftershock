using System;
using UnityEngine;

/// <summary>
/// Сенсор, который использую враги для обнаружения игрока
/// </summary>
public abstract class Sensor : MonoBehaviour
{
    private float _signalStrength;

    /// <summary>
    /// Время на фокусировку сенсора <para/>
    /// Используется для симуляции процесса распознования сигнала, 
    /// например апартная задерка между камерой и мозгом врага
    /// </summary>
    [SerializeField]
    protected float FocusingTime = 1f;
    [SerializeField]
    protected float FocusingProgress;

    /// <summary>
    /// Дистанция на которой работает сенсор
    /// </summary>
    [SerializeField]
    protected float Distance;

    /// <summary>
    /// Значение сигнала попавшего в зону видимости сенсора от 0 до 1.
    /// </summary>
    /// <returns>0 если не попал в зону видимости, если попал то сила полученого сигнала от 0 до 1</returns>
    protected abstract float SignalInFieldOfView();

    /// <summary>
    /// Значение сигнала попавшего в зону атакующего луча 0 либо 1.
    /// </summary>
    /// <returns>0 если не попал , 1 если попал</returns>
    public abstract bool SignalInAttackRay(Vector3 targetPosition);

    /// <summary>
    /// Сенсор активирован
    /// </summary>
    /// <returns></returns>
    public bool IsActivated() => SignalStrength > 0;

    /// <summary>
    /// Сила обнаруженного сингала от 0 до 1
    /// </summary>
    public float SignalStrength => FocusingProgress >= FocusingTime ? _signalStrength : 0;

    /// <summary>
    /// Координаты источник сигнала
    /// </summary>
    public abstract Vector3? SignalSourcePosition { get; }

    protected virtual void Update()
    {
        // Получаем силу сигнала от конкретной реализации сенсора
        _signalStrength = Mathf.Clamp(SignalInFieldOfView(), 0f, 1f);
        if (_signalStrength > 0f)
        {
            // Увеличиваем прогресс, но не выше лимита FocusingTime
            FocusingProgress = Mathf.Min(FocusingProgress + Time.deltaTime, FocusingTime);
        }
        else
        {
            // Уменьшаем прогресс, но не ниже 0
            FocusingProgress = Mathf.Max(FocusingProgress - Time.deltaTime, 0f);
        }
    }
    public virtual void SetFocusingTime(float value)
    {
        FocusingTime = value;
    }

    public virtual void SetDistance(float value)
    {
        Distance = value;
    }
    
}
