using Failsafe.Scripts.Health;
using UnityEngine;
using VContainer.Unity;

namespace Failsafe.Player.Model
{
    /// <summary>
    /// Пассивно восстанавливает здоровье с настраиваемой скоростью.
    /// Нулевое значение полностью отключает регенерацию.
    /// </summary>
    public sealed class PlayerHealthRegenerationController : IFixedTickable
    {
        private readonly IHealth _health;
        private readonly PlayerRuntimeParameters _runtimeParameters;

        public PlayerHealthRegenerationController(
            IHealth health,
            PlayerRuntimeParameters runtimeParameters)
        {
            _health = health;
            _runtimeParameters = runtimeParameters;
        }

        public void FixedTick()
        {
            if (_health == null || _health.IsDead)
                return;

            if (_health.CurrentHealth >= _health.MaxHealth)
                return;

            float regenerationPerSecond = Mathf.Max(
                0f,
                _runtimeParameters.HealthRegenerationPerSecond);

            if (regenerationPerSecond <= 0f)
                return;

            _health.AddHealth(regenerationPerSecond * Time.fixedDeltaTime);
        }
    }
}
