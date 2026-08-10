using Cysharp.Threading.Tasks;
using Failsafe.Scripts.SaveSystem;
using UnityEngine;
using VContainer;

public class MainMenu : MonoBehaviour
{
    private IRunSessionCoordinator _runSessionCoordinator;

    [Inject]
    public void Construct(IRunSessionCoordinator runSessionCoordinator)
    {
        _runSessionCoordinator = runSessionCoordinator;
    }

    public void PlayGame()
    {
        StartNewRunAsync().Forget();
    }

    public void ContinueGame()
    {
        ContinueRunAsync().Forget();
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    private async UniTask StartNewRunAsync()
    {
        if (!TryGetCoordinator())
            return;

        RunSaveOperationResult result = await _runSessionCoordinator.StartNewRunAsync();
        LogResult(result, "New run started.");
    }

    private async UniTask ContinueRunAsync()
    {
        if (!TryGetCoordinator())
            return;

        RunSaveOperationResult result = await _runSessionCoordinator.ContinueRunAsync();
        LogResult(result, "Run continued.");
    }

    private bool TryGetCoordinator()
    {
        if (_runSessionCoordinator != null)
            return true;

        RunSaveLog.Error(
            RunSaveLog.Menu,
            "IRunSessionCoordinator was not injected into MainMenu. " +
            "Add MainMenuLifetimeScope to the menu scene and make RootLifetimeScope its parent.",
            this);
        return false;
    }

    private void LogResult(RunSaveOperationResult result, string successMessage)
    {
        if (!result.Succeeded)
        {
            RunSaveLog.Error(RunSaveLog.Menu, result.Error, this);
            return;
        }

        string backupSuffix = result.LoadedFromBackup
            ? " The backup save was used."
            : string.Empty;
        RunSaveLog.Info(RunSaveLog.Menu, successMessage + backupSuffix, this);
    }
}
