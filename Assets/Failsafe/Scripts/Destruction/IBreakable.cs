namespace Failsafe.Scripts.Destruction
{
    public interface IBreakable
    {
        bool IsBroken { get; }

        void Break();
    }
}
