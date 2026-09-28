using UnityEngine;

namespace Failsafe.PlayerMovements.States
{
    /// <summary>
    /// Взаимодействие с объектами
    /// </summary>
    public class InteractState : BehaviorState
    {
        public override void Enter()
        {
            global::Failsafe.Debugging.GameplayLog.Trace("Enter " + nameof(InventoryState));
        }
        // TODO вызывается когда игрок взаимодействует с объектами окружения
        // в этом состоянии игрок не должен двигаться, состояние прекращается после определенного времени
    }
}
