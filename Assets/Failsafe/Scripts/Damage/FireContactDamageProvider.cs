using System;

namespace Failsafe.Scripts.Damage.Implementation
{
    [Serializable]
    public sealed class FireContactDamage : IDamage
    {
        public float Amount { get; }
        public object Source { get; }  // можно хранить ссылку на очаг

        public FireContactDamage(float amount, object source = null)
        {
            Amount = amount;
            Source = source;
        }
    }
}