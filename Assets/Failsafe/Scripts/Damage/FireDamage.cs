using System;
namespace Failsafe.Scripts.Damage.Implementation
{
    [Serializable]
    public class FireDamage : IDamage
    {
        public float DamagePerTick { get; private set; }
        public FireDamage(float perTick) { DamagePerTick = perTick; }
    }
}