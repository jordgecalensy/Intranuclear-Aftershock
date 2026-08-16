using Failsafe.Scripts.Modifiebles;

namespace Failsafe.Player.Model
{
    /// <summary>
    /// Runtime-копия параметров, которые могут изменяться эффектами во время одного рана.
    /// </summary>
    public sealed class PlayerRuntimeParameters
    {
        public ModifiableField<float> MaxHealth { get; }
        public ModifiableField<float> MaxStamina { get; }
        public ModifiableField<float> HealthRegenerationPerSecond { get; }
        public ModifiableField<float> StaminaRegenerationPerSecond { get; }
        public ModifiableField<float> NoiseStrengthMultiplier { get; }

        public PlayerRuntimeParameters(PlayerModelParameters source)
        {
            MaxHealth = source.MaxHealth;
            MaxStamina = source.MaxStamina;
            HealthRegenerationPerSecond = source.RegenerateHealthPerSecond;
            StaminaRegenerationPerSecond = source.RegenerateStaminaPerSecond;
            NoiseStrengthMultiplier = 1f;
        }
    }
}
