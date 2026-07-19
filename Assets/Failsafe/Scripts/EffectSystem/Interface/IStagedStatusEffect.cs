namespace Failsafe.Scripts.EffectSystem
{
    public interface IStagedStatusEffect : IRegisteredStatusEffect
    {
        int CurrentStage { get; }
        float BuildUpValue { get; }
    }
}