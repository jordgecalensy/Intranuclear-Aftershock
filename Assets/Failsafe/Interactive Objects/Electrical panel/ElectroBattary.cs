using Failsafe.Player.Scripts.Interaction;
using UnityEngine;

[RequireComponent(typeof(Insertable))]
public sealed class ElectroBattary : MonoBehaviour, IInsertable, ICarryUsable
{
    public void OnInserted()
    {
    }

    public void OnEjected()
    {
    }

    public void OnGrabbed(Transform grabPoint)
    {
    }

    public void OnUseStart()
    {
    }

    public void UseTick(float dt)
    {
    }

    public void OnUseStop()
    {
    }

    public void OnDropped()
    {
    }
}