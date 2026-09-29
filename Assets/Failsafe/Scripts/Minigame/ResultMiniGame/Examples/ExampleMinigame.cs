using UnityEngine;
using UnityEngine.Events;

public class ExampleMinigame : MinigameBase
{
    [SerializeField] protected UnityEvent _onStart;
    [SerializeField] protected UnityEvent _onExit;

    public void StartGame()
    {
        OnGameStart();
    }

    public void ExitGame()
    {
        OnGameExit();
    }

    public void Win()
    {
        OnWin();
    }

    public void Fail()
    {
        OnFail();
    }

    protected override void OnGameStart()
    {
        global::Failsafe.Debugging.GameplayLog.Trace("Game start");
        PerformAction(_onStart);
    }
    protected override void OnGameExit()
    {
        global::Failsafe.Debugging.GameplayLog.Trace("Game Exit");
        PerformAction(_onExit);
    }

    protected override void OnWin()
    {
        global::Failsafe.Debugging.GameplayLog.Trace("Perform win actions");
        PerformAction(_onWin);
    }

    protected override void OnFail()
    {
        global::Failsafe.Debugging.GameplayLog.Trace("Perform fail actions");
        PerformAction(_onFail);
    }
}
