namespace Failsafe.Scripts.EffectSystem.Targets
{
    public interface IStasisResponder
    {
        void OnStasisStart();
        void OnStasisEnd();
    }
}