
/// <summary>
/// Принудительное состояние
/// Состояние, которое задается снаружи машины состояний
/// </summary>
public abstract class BehaviorForcedState : BehaviorState
{
    /// <summary>
    /// Предыдущее состояние на момент переключения на это состояние
    /// </summary>
    protected BehaviorState PreviousState;

    /// <summary>
    /// Продолжительность состояния
    /// </summary>
    protected float? StateDuration;

    /// <summary>
    /// Вызывается в момент переключения на это состояние
    /// </summary>
    /// <param name="previousState">Предыдущее состояние на момент переключения на это состояние</param>
    public virtual void Enter(BehaviorState previousState, float? stateDuration)
    {
        PreviousState = previousState;
        StateDuration = stateDuration;
        Enter();
    }
}
