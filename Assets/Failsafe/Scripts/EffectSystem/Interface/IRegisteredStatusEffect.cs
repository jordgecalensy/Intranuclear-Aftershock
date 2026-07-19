namespace Failsafe.Scripts.EffectSystem
{
    public interface IRegisteredStatusEffect
    {
        StatusEffectType StatusType { get; }

        void ForceClearFromStatusState();
    }
}