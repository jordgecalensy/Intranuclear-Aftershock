using System.Collections.Generic;
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
        private const string NextContactTickKey = "beam_next_contact_tick";
        private const string NextBeamFieldTickKey = "beam_next_field_tick";

        public GameObject laserVfxPrefab;

        [SerializeField] private float _effectTickInterval = 0.1f;

        [Header("Continuous Contact")]
        [SerializeField] private EffectBundle _contactDamageEffects;
        [SerializeField, Min(0.01f)] private float _contactTickInterval = 0.1f;

        [Header("Beam Physics Field")]
        [SerializeField, Min(0.01f)] private float _beamFieldTickInterval = 0.1f;
        [SerializeField, Min(0f)] private float _beamFieldRadius = 1.25f;
        [Tooltip("Сила отталкивания Rigidbody вдоль луча за одну секунду.")]
        [SerializeField, Min(0f)] private float _beamFieldForce = 20f;
        [SerializeField] private LayerMask _beamFieldMask = ~0;

        private readonly HashSet<Rigidbody> _beamFieldBodies =
            new HashSet<Rigidbody>();

        public override bool Fire(WeaponController controller, Vector3 targetPoint)
        {
            if (controller.firePoint == null)
                return false;

            if (stats == null)
            {
                Debug.LogError($"[{nameof(LaserBeamStrategy)}] WeaponStats is not assigned.", this);
                return false;
            }

            LaserBeamController beam = controller.GetRuntimeObject<LaserBeamController>(ActiveBeamKey);
            Transform targetHelper = controller.GetRuntimeObject<Transform>(BeamTargetKey);

            if (beam == null)
            {
                if (laserVfxPrefab == null)
                {
                    Debug.LogError($"[{nameof(LaserBeamStrategy)}] Laser VFX prefab is not assigned.", this);
                    return false;
                }

                GameObject beamObject = Instantiate(
                    laserVfxPrefab,
                    controller.firePoint.position,
                    Quaternion.identity);

                beam = beamObject.GetComponent<LaserBeamController>();

                if (beam == null)
                {
                    Debug.LogError(
                        $"[{nameof(LaserBeamStrategy)}] Laser VFX prefab must contain LaserBeamController.",
                        laserVfxPrefab);

                    Destroy(beamObject);
                    return false;
                }

                GameObject helperObject = new GameObject("LaserTargetHelper");
                targetHelper = helperObject.transform;
                targetHelper.position = targetPoint;

                beam.Initialize(controller.firePoint, targetHelper);
                beam.SetMaximumLength(stats.range);

                controller.SetRuntimeObject(ActiveBeamKey, beam);
                controller.SetRuntimeObject(BeamTargetKey, targetHelper);
            }

            if (targetHelper != null)
                targetHelper.position = targetPoint;

            bool hasHit = beam.TryGetCurrentHit(out RaycastHit hit);
            UpdateCollisionAudio(controller, hasHit, hit);
            TryApplyEffects(controller, beam, hasHit, hit);
            TryApplyContactEffects(controller, beam, hasHit, hit);
            TryApplyBeamFieldEffects(
                controller,
                beam,
                hasHit ? hit.collider : null);

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

            WalkerAudioManager audioManager =
                controller.GetComponentInParent<WalkerAudioManager>();
            audioManager?.StopLaserCollision();

            controller.ClearRuntimeObject(ActiveBeamKey);
            controller.ClearRuntimeObject(BeamTargetKey);
            controller.ClearRuntimeObject(NextEffectTickKey);
            controller.ClearRuntimeObject(NextContactTickKey);
            controller.ClearRuntimeObject(NextBeamFieldTickKey);
        }

        private void TryApplyEffects(
            WeaponController controller,
            LaserBeamController beam,
            bool hasHit,
            RaycastHit hit)
        {
            float tickInterval = Mathf.Max(0.01f, _effectTickInterval);
            float nextTick = controller.GetRuntimeValue<float>(NextEffectTickKey);

            if (Time.time < nextTick)
                return;

            controller.SetRuntimeObject(
                NextEffectTickKey,
                Time.time + tickInterval);

            if (!hasHit)
                return;

            float power = CalculateTickPower(stats.damage, tickInterval);
            EffectContext context = CreateContext(controller, beam, hit, power);

            controller.Effects?.Apply(impactEffects, context);
        }

        private void TryApplyContactEffects(
            WeaponController controller,
            LaserBeamController beam,
            bool hasHit,
            RaycastHit hit)
        {
            float tickInterval = Mathf.Max(0.01f, _contactTickInterval);
            float nextTick = controller.GetRuntimeValue<float>(NextContactTickKey);

            if (Time.time < nextTick)
                return;

            controller.SetRuntimeObject(
                NextContactTickKey,
                Time.time + tickInterval);

            if (!hasHit)
                return;

            if (_contactDamageEffects != null)
            {
                EffectContext damageContext = CreateContext(
                    controller,
                    beam,
                    hit,
                    CalculateTickPower(stats.damage, tickInterval));

                controller.Effects?.Apply(_contactDamageEffects, damageContext);
            }

        }

        private void TryApplyBeamFieldEffects(
            WeaponController controller,
            LaserBeamController beam,
            Collider directlyHitCollider)
        {
            if (_beamFieldRadius <= 0f ||
                _beamFieldMask.value == 0)
            {
                return;
            }

            float tickInterval = Mathf.Max(0.01f, _beamFieldTickInterval);
            float nextTick = controller.GetRuntimeValue<float>(NextBeamFieldTickKey);

            if (Time.time < nextTick)
                return;

            controller.SetRuntimeObject(
                NextBeamFieldTickKey,
                Time.time + tickInterval);

            Vector3 beamDirection = beam.CurrentDirection;

            if (beamDirection.sqrMagnitude <= 0.0001f)
                return;

            Vector3 start = controller.firePoint.position;
            Vector3 end = beam.CurrentEndPoint;

            if ((end - start).sqrMagnitude <= 0.0001f)
                return;

            Collider[] colliders = Physics.OverlapCapsule(
                start,
                end,
                _beamFieldRadius,
                _beamFieldMask,
                QueryTriggerInteraction.Ignore);

            _beamFieldBodies.Clear();
            Transform ownerRoot = controller.transform.root;
            Vector3 impulse = CalculateBeamFieldImpulse(
                beamDirection,
                _beamFieldForce,
                tickInterval);

            if (impulse.sqrMagnitude <= 0.0001f)
                return;

            TryApplyBeamFieldImpulse(
                directlyHitCollider,
                ownerRoot,
                impulse);

            for (int index = 0; index < colliders.Length; index++)
                TryApplyBeamFieldImpulse(colliders[index], ownerRoot, impulse);
        }

        private void TryApplyBeamFieldImpulse(
            Collider collider,
            Transform ownerRoot,
            Vector3 impulse)
        {
            Rigidbody body = ResolveRigidbody(collider);

            if (body == null ||
                body.isKinematic ||
                IsInHierarchy(body.transform, ownerRoot) ||
                !_beamFieldBodies.Add(body))
            {
                return;
            }

            body.WakeUp();
            body.AddForce(impulse, ForceMode.Impulse);
        }

        private static Rigidbody ResolveRigidbody(Collider collider)
        {
            if (collider == null)
                return null;

            return collider.attachedRigidbody ??
                   collider.GetComponentInParent<Rigidbody>();
        }

        private static bool IsInHierarchy(Transform candidate, Transform root)
        {
            return root != null &&
                   (candidate == root || candidate.IsChildOf(root));
        }

        private static EffectContext CreateContext(
            WeaponController controller,
            LaserBeamController beam,
            RaycastHit hit,
            float power)
        {
            Vector3 direction = beam.CurrentDirection;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = controller.firePoint.forward;

            return new EffectContext(
                controller.gameObject,
                hit.collider,
                hit.point,
                hit.normal,
                direction,
                power);
        }

        private static void UpdateCollisionAudio(
            WeaponController controller,
            bool hasHit,
            RaycastHit hit)
        {
            WalkerAudioManager audioManager =
                controller.GetComponentInParent<WalkerAudioManager>();

            if (audioManager == null)
                return;

            audioManager.UpdateLaserCollision(
                hasHit,
                hasHit ? hit.point : controller.firePoint.position);
        }

        public static float CalculateTickPower(float valuePerSecond, float tickInterval)
        {
            return Mathf.Max(0f, valuePerSecond) * Mathf.Max(0.01f, tickInterval);
        }

        public static Vector3 CalculateBeamFieldImpulse(
            Vector3 direction,
            float forcePerSecond,
            float tickInterval)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            return direction.normalized *
                   CalculateTickPower(forcePerSecond, tickInterval);
        }

        private void OnValidate()
        {
            _effectTickInterval = Mathf.Max(0.01f, _effectTickInterval);
            _contactTickInterval = Mathf.Max(0.01f, _contactTickInterval);
            _beamFieldTickInterval = Mathf.Max(0.01f, _beamFieldTickInterval);
            _beamFieldRadius = Mathf.Max(0f, _beamFieldRadius);
            _beamFieldForce = Mathf.Max(0f, _beamFieldForce);
        }
    }
}
