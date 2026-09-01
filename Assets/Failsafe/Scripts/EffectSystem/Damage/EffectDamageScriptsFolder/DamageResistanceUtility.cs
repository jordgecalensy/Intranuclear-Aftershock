using Failsafe.Scripts.Damage;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public static class DamageResistanceUtility
    {
        public static DamageInfo ApplyResistance(
            DamageTarget target,
            DamageInfo damage,
            bool ignoreResistance = false,
            bool log = false)
        {
            if (!target.IsValid)
                return damage;

            if (ignoreResistance)
                return damage;

            DamageResistanceComponent resistance =
                ResolveResistanceComponent(target.GameObject);

            if (resistance == null)
                return damage;

            float baseAmount = Mathf.Max(0f, damage.Amount);
            float multiplier = resistance.GetDamageMultiplier(damage.Type);
            float finalAmount = baseAmount * multiplier;

            if (log)
            {
                EffectLog.Info(EffectLog.Resistance,
                    $"[DamageResistanceUtility] {target.GameObject.name}: {damage.Type} damage {baseAmount:0.###} x {multiplier:0.###} = {finalAmount:0.###}",
                    target.GameObject);
            }

            return new DamageInfo(
                finalAmount,
                damage.Type,
                damage.ApplicationKind,
                damage.Source,
                damage.Point,
                damage.Direction,
                damage.Power);
        }

        public static void ApplyDamage(
            DamageTarget target,
            DamageInfo damage,
            bool ignoreResistance = false,
            bool log = false)
        {
            if (!target.IsValid)
                return;

            DamageInfo finalDamage = ApplyResistance(
                target,
                damage,
                ignoreResistance,
                log);

            if (finalDamage.Amount <= 0f)
            {
                if (log && target.GameObject != null)
                {
                    EffectLog.Info(EffectLog.Resistance,
                        $"[DamageResistanceUtility] {target.GameObject.name}: {finalDamage.Type} damage blocked.",
                        target.GameObject);
                }

                return;
            }

            target.TakeDamage(finalDamage);
        }

        public static DamageResistanceComponent ResolveResistanceComponent(GameObject targetObject)
        {
            if (targetObject == null)
                return null;

            return targetObject.GetComponent<DamageResistanceComponent>() ??
                   targetObject.GetComponentInParent<DamageResistanceComponent>() ??
                   targetObject.GetComponentInChildren<DamageResistanceComponent>(true);
        }

        public static DamageResistanceComponent ResolveOrAddResistanceComponent(GameObject targetObject)
        {
            if (targetObject == null)
                return null;

            DamageResistanceComponent component =
                ResolveResistanceComponent(targetObject);

            if (component != null)
                return component;

            return targetObject.AddComponent<DamageResistanceComponent>();
        }
    }
}