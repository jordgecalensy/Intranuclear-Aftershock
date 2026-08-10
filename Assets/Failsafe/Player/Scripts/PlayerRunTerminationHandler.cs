using System;
using Failsafe.Scripts.Health;
using Failsafe.Scripts.SaveSystem;
using VContainer.Unity;

namespace Failsafe.Player.Scripts
{
    public sealed class PlayerRunTerminationHandler : IInitializable, IDisposable
    {
        private readonly IHealth _health;
        private readonly IRunSaveService _runSaveService;

        public PlayerRunTerminationHandler(
            IHealth health,
            IRunSaveService runSaveService)
        {
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _runSaveService =
                runSaveService ?? throw new ArgumentNullException(nameof(runSaveService));
        }

        public void Initialize()
        {
            _health.OnDeath += HandlePlayerDeath;
        }

        public void Dispose()
        {
            _health.OnDeath -= HandlePlayerDeath;
        }

        private void HandlePlayerDeath()
        {
            RunSaveOperationResult result =
                _runSaveService.EndRun(RunEndReasons.PlayerDeath);

            if (!result.Succeeded)
            {
                RunSaveLog.Error(
                    RunSaveLog.Player,
                    $"{nameof(PlayerRunTerminationHandler)}: " +
                    $"Failed to persist the ended run: {result.Error}");
            }
        }
    }
}
