using UnityEngine;

namespace Failsafe.Scripts.Damage
{
    public enum DamageType
    {
        Physical,
        Fire,
        Poison,
        Electric,
        Explosion,
        Environment
    }

    public enum DamageApplicationKind
    {
        Instant,
        Contact,
        DotTick,
        Explosion
    }

    public readonly struct DamageInfo : IDamage
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly DamageApplicationKind ApplicationKind;
        public readonly GameObject Source;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly float Power;

        public DamageInfo(
            float amount,
            DamageType type,
            DamageApplicationKind applicationKind = DamageApplicationKind.Instant,
            GameObject source = null,
            Vector3 point = default,
            Vector3 direction = default,
            float power = 1f)
        {
            Amount = amount;
            Type = type;
            ApplicationKind = applicationKind;
            Source = source;
            Point = point;
            Direction = direction;
            Power = power;
        }
    }
}