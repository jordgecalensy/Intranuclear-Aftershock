using Failsafe.Scripts.EffectSystem;
using UnityEngine;

namespace Failsafe.Enemies.Projectiles
{
    [CreateAssetMenu(menuName = "Combat/Strategies/LaserBeam")]
    public class LaserBeamStrategy : WeaponStrategy
    {
        private const string ActiveBeamKey = "active_beam";
        private const string BeamTargetKey = "beam_target";
        private const string NextEffectTickKey = "beam_next_effect_tick";

        public GameObject laserVfxPrefab;

        [SerializeField] private float _effectTickInterval = 0.1f;

        public override bool Fire(WeaponController controller, Vector3 targetPoint)
        {
            if (controller.firePoint == null)
                return false;

            LaserBeamController beam = controller.GetRuntimeObject<LaserBeamController>(ActiveBeamKey);
            Transform targetHelper = controller.GetRuntimeObject<Transform>(BeamTargetKey);

            if (beam == null)
            {
                if (laserVfxPrefab == null)
                {
                    Debug.LogError($"[{nameof(LaserBeamStrategy)}] Laser VFX prefab is not assigned.", this);
                    return false;
                }

                var go = Instantiate(
                    laserVfxPrefab,
                    controller.firePoint.position,
                    Quaternion.identity);

                beam = go.GetComponent<LaserBeamController>();

                if (beam == null)
                {
                    Debug.LogError(
                        $"[{nameof(LaserBeamStrategy)}] Laser VFX prefab must contain LaserBeamController.",
                        laserVfxPrefab);

                    Destroy(go);
                    return false;
                }

                var helperObj = new GameObject("LaserTargetHelper");
                targetHelper = helperObj.transform;

                beam.Initialize(controller.firePoint, targetHelper);

                controller.SetRuntimeObject(ActiveBeamKey, beam);
                controller.SetRuntimeObject(BeamTargetKey, targetHelper);
            }

            if (targetHelper != null)
                targetHelper.position = targetPoint;

            TryApplyEffects(controller, targetPoint);

            return true;
        }

        public override void StopFiring(WeaponController controller)
        {
            LaserBeamController beam = controller.GetRuntimeObject<LaserBeamController>(ActiveBeamKey);
            Transform targetHelper = controller.GetRuntimeObject<Transform>(BeamTargetKey);

            if (beam != null)
                Destroy(beam.gameObject);

            if (targetHelper != null)
                Destroy(targetHelper.gameObject);

            controller.ClearRuntimeObject(ActiveBeamKey);
            controller.ClearRuntimeObject(BeamTargetKey);
            controller.ClearRuntimeObject(NextEffectTickKey);
        }

        private void TryApplyEffects(WeaponController controller, Vector3 targetPoint)
        {
            float nextTick = controller.GetRuntimeValue<float>(NextEffectTickKey);

            if (Time.time < nextTick)
                return;

            controller.SetRuntimeObject(
                NextEffectTickKey,
                Time.time + Mathf.Max(0.01f, _effectTickInterval));

            Vector3 origin = controller.firePoint.position;
            Vector3 direction = targetPoint - origin;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = controller.firePoint.forward;

            direction.Normalize();

            float distance = Vector3.Distance(origin, targetPoint);

            if (!Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    distance + 0.5f,
                    stats.hitMask))
            {
                return;
            }

            var context = new EffectContext(
                controller.gameObject,
                hit.collider,
                hit.point,
                hit.normal,
                direction);

            controller.Effects?.Apply(impactEffects, context);
        }
    }
}