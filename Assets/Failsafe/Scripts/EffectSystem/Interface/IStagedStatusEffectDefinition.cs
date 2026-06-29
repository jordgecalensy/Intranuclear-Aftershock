namespace Failsafe.Scripts.EffectSystem
{
    public interface IStagedStatusEffectDefinition : IStatusEffectDefinition
    {
        int PredictStageAfterApply(StatusEffectState state, EffectContext context);
    }
}