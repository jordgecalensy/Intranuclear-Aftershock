using System;
using Cysharp.Threading.Tasks;
using Failsafe.Player.UI;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.SaveSystem;
using VContainer.Unity;

namespace Failsafe.Player.Scripts
{
    public sealed class DeathScreenPresenter : IInitializable, IDisposable
    {
        private readonly IHealth _health;
        private readonly DeathScreenView _view;
        private readonly IRunSessionCoordinator _runSessionCoordinator;
        private readonly global::CursorLock _cursorLock;

        private bool _isNavigationInProgress;
        private bool _isDisposed;

        public DeathScreenPresenter(
            IHealth health,
            DeathScreenView view,
            IRunSessionCoordinator runSessionCoordinator,
            global::CursorLock cursorLock)
        {
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _runSessionCoordinator =
                runSessionCoordinator ??
                throw new ArgumentNullException(nameof(runSessionCoordinator));
            _cursorLock =
                cursorLock ?? throw new ArgumentNullException(nameof(cursorLock));
        }

        public void Initialize()
        {
            _view.Hide();
            _view.NewRunRequested += HandleNewRunRequested;
            _view.MainMenuRequested += HandleMainMenuRequested;
            _health.OnDeath += HandlePlayerDeath;

            if (_health.IsDead)
                HandlePlayerDeath();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _view.NewRunRequested -= HandleNewRunRequested;
            _view.MainMenuRequested -= HandleMainMenuRequested;
            _health.OnDeath -= HandlePlayerDeath;
            _isDisposed = true;
        }

        private void HandlePlayerDeath()
        {
            _cursorLock.SetCursorLocked(false);
            _view.Show();

            RunSaveLog.Info(
                RunSaveLog.DeathScreen,
                "Death screen opened.");
        }

        private void HandleNewRunRequested()
        {
            if (!TryBeginNavigation())
                return;

            StartNewRunAsync().Forget();
        }

        private void HandleMainMenuRequested()
        {
            if (!TryBeginNavigation())
                return;

            ReturnToMainMenuAsync().Forget();
        }

        private bool TryBeginNavigation()
        {
            if (_isDisposed ||
                _isNavigationInProgress ||
                _runSessionCoordinator.IsBusy)
            {
                return false;
            }

            _isNavigationInProgress = true;
            _view.SetInteractable(false);
            return true;
        }

        private async UniTask StartNewRunAsync()
        {
            try
            {
                RunSaveOperationResult result =
                    await _runSessionCoordinator.StartNewRunAsync();

                HandleNavigationResult(result, "start a new run");
            }
            catch (Exception exception)
            {
                HandleUnexpectedNavigationFailure(
                    "start a new run",
                    exception);
            }
        }

        private async UniTask ReturnToMainMenuAsync()
        {
            try
            {
                RunSaveOperationResult result =
                    await _runSessionCoordinator.ReturnToMainMenuAsync();

                HandleNavigationResult(result, "return to the main menu");
            }
            catch (Exception exception)
            {
                HandleUnexpectedNavigationFailure(
                    "return to the main menu",
                    exception);
            }
        }

        private void HandleNavigationResult(
            RunSaveOperationResult result,
            string operation)
        {
            if (result.Succeeded)
                return;

            RunSaveLog.Error(
                RunSaveLog.DeathScreen,
                $"Failed to {operation}: {result.Error}");

            RestoreInteractionIfAvailable();
        }

        private void HandleUnexpectedNavigationFailure(
            string operation,
            Exception exception)
        {
            RunSaveLog.Error(
                RunSaveLog.DeathScreen,
                $"Unexpected failure while trying to {operation}: " +
                $"{exception.Message}");

            RestoreInteractionIfAvailable();
        }

        private void RestoreInteractionIfAvailable()
        {
            _isNavigationInProgress = false;

            if (!_isDisposed && _view != null)
                _view.SetInteractable(true);
        }
    }
}
