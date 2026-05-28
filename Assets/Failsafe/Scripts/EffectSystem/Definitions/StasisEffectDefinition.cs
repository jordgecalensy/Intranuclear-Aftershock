using Failsafe.Scripts.EffectSystem.Effects;
using Failsafe.Scripts.EffectSystem.Targets;
using FMODUnity;
using UnityEngine;

namespace Failsafe.Scripts.EffectSystem.Definitions
{
    [CreateAssetMenu(menuName = "Effects/Stasis/Stasis")]
    public sealed class StasisEffectDefinition : EffectDefinition
    {
        [SerializeField] private float _duration = 3f;
        [SerializeField] private bool _restoreVelocityAfterEnd = false;
        [SerializeField] private Material _stasisMaterial;
        [SerializeField] private EventReference _stasisEndSound;

        [Header("Target filters")]
        [SerializeField] private bool _affectRigidbodies = true;
        [SerializeField] private bool _affectEnemies = true;
        [SerializeField] private bool _affectDamageObstacles = true;
        [SerializeField] private bool _affectCustomResponders = true;

        public override bool CanApply(EffectContext context)
        {
            if (_affectCustomResponders && context.TryGet<IStasisResponder>(out _))
                return true;

            if (_affectEnemies && context.TryGet<Enemy>(out _))
                return true;

            if (_affectDamageObstacles && context.TryGet<DamageObstacle>(out _))
                return true;

            if (_affectRigidbodies && context.TryGetRigidbody(out var rb))
                return rb != null;

            return false;
        }

        public override Effect CreateEffect(EffectContext context)
        {
            context.TryGetRigidbody(out var rb);
            context.TryGet<Enemy>(out var enemy);
            context.TryGet<DamageObstacle>(out var obstacle);
            context.TryGet<IStasisResponder>(out var responder);

            GameObject target = context.TargetObject;

            Renderer[] renderers = target != null
                ? target.GetComponentsInChildren<Renderer>(true)
                : null;

            return new StasisEffect(
                rb,
                enemy,
                obstacle,
                responder,
                renderers,
                target,
                _duration,
                _restoreVelocityAfterEnd,
                _stasisMaterial,
                _stasisEndSound);
        }

        public override string GetStackKey(EffectContext context)
        {
            return "status.stasis";
        }
    }
}