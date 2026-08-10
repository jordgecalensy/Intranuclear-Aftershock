namespace Failsafe.Scripts.EffectSystem.Targets
{
    public interface IMovementSpeedModifierTarget
    {
        void SetSpeedModifier(int modifierId, float multiplier);
        void RemoveSpeedModifier(int modifierId);
    }
}