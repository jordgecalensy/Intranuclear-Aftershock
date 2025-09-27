using System;

namespace Failsafe.Scripts.Damage.Implementation
{
    [Serializable]
    public sealed class FireDotTickDamage : IDamage
    {
        public float Amount { get; }
        public float Intensity { get; }
        public object Source { get; }

        public FireDotTickDamage(float amount, float intensity, object source = null)
        {
            Amount = amount;
            Intensity = intensity;
            Source = source;
        }
    }
}