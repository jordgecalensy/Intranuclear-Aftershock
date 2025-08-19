using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
// Конечная точка
public class PowerEndPoint : PowerNode
{
    public UnityEvent onPowered; // событие, которое вызывается при питании
    public UnityEvent offPowered;

    protected override void OnPowered()
    {
        base.OnPowered();
        Debug.Log($"{name} запитан!");
        onPowered?.Invoke();
    }
    protected override void OnPowerLost()
    {
        base.OnPowerLost();
        offPowered?.Invoke();
    }
}
