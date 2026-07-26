namespace Failsafe.Scripts.SaveSystem
{
    public interface IRunPersistentStateProvider
    {
        string StateTypeId { get; }
        int StateVersion { get; }

        string CapturePersistentState();
        void RestorePersistentState(string serializedState, int stateVersion);
    }
}
