using UnityEngine;

namespace Failsafe.Scripts.EffectSystem
{
    public interface IImpactImpulseReceiver
    {
        void AddImpactImpulse(
            Vector3 impulse,
            Vector3 impactPoint,
            GameObject source);
    }
}