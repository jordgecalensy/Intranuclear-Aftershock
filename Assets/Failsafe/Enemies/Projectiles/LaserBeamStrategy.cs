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
        private const int BeamFieldColliderCapacity = 64;
        private const int BeamFieldOcclusionCapacity = 32;
        private const float BeamFieldOcclusionOriginOffset = 0.02f;

        public GameObject laserVfxPrefab;

        [SerializeField] private float _effectTickInterval = 0.1f;

        [Header("Continuous Contact")]
        [SerializeField] private EffectBundle _contactDamageEffects;
        [SerializeField] private EffectBundle _contactPhysicsEffects;
        [SerializeField, Min(0.01f)] private float _contactTickInterval = 0.1f;

        [Header("Beam Physics Field")]
        [SerializeField, Min(0f)] private float _beamFieldRadius = 1.25f;
        [SerializeField, Min(0.01f)] private float _beamFieldFalloffExponent = 1f;
        [SerializeField] private LayerMask _beamFieldMask = ~0;

        private readonly Collider[] _beamFieldColliders =
            new Collider[BeamFieldColliderCapacity];

        private readonly RaycastHit[] _beamFieldOcclusionHits =
            new RaycastHit[BeamFieldOcclusionCapacity];

        private readonly Dictionary<Rigidbody, BeamFieldCandidate> _beamFieldCandidates =
            new Dictionary<Rigidbody, BeamFieldCandidate>();

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

            if (hasHit && _contactDamageEffects != null)
            {
                EffectContext damageContext = CreateContext(
                    controller,
                    beam,
                    hit,
                    CalculateTickPower(stats.damage, tickInterval));

                controller.Effects?.Apply(_contactDamageEffects, damageContext);
            }

            if (hasHit && _contactPhysicsEffects != null)
            {
                EffectContext physicsContext = CreateContext(
                    controller,
                    beam,
                    hit,
                    CalculateTickPower(stats.hitForce, tickInterval));

                controller.Effects?.Apply(_contactPhysicsEffects, physicsContext);
            }

            Rigidbody primaryHitBody = hasHit
                ? ResolveRigidbody(hit.collider)
                : null;

            TryApplyBeamFieldEffects(
                controller,
                beam,
                primaryHitBody,
                tickInterval);
        }

        private void TryApplyBeamFieldEffects(
            WeaponController controller,
            LaserBeamController beam,
            Rigidbody primaryHitBody,
            float tickInterval)
        {
            if (controller.Effects == null ||
                _contactPhysicsEffects == null ||
                _beamFieldRadius <= 0f ||
                _beamFieldMask.value == 0)
            {
                return;
            }

            Vector3 beamDirection = beam.CurrentDirection;

            if (beamDirection.sqrMagnitude <= 0.0001f)
                return;

            Vector3 start = controller.firePoint.position;
            Vector3 end = beam.CurrentEndPoint;

            if ((end - start).sqrMagnitude <= 0.0001f)
                return;

            int colliderCount = Physics.OverlapCapsuleNonAlloc(
                start,
                end,
                _beamFieldRadius,
                _beamFieldColliders,
                _beamFieldMask,
                QueryTriggerInteraction.Ignore);

            _beamFieldCandidates.Clear();
            Transform ownerRoot = controller.transform.root;

            for (int index = 0; index < colliderCount; index++)
            {
                Collider collider = _beamFieldColliders[index];
                Rigidbody body = ResolveRigidbody(collider);

                if (collider == null ||
                    body == null ||
                    body.isKinematic ||
                    body == primaryHitBody ||
                    IsInHierarchy(collider.transform, ownerRoot))
                {
                    continue;
                }

                Vector3 axisPoint = ClosestPointOnSegment(
                    start,
                    end,
                    body.worldCenterOfMass);

                Vector3 surfacePoint = collider.ClosestPoint(axisPoint);
                float distance = Vector3.Distance(axisPoint, surfacePoint);
                float falloff = CalculateBeamFieldFalloff(
                    distance,
                    _beamFieldRadius,
                    _beamFieldFalloffExponent);

                if (falloff <= 0f ||
                    !IsBeamFieldPathClear(
                        axisPoint,
                        surfacePoint,
                        beamDirection,
                        collider,
                        body,
                        ownerRoot))
                {
                    continue;
                }

                var candidate = new BeamFieldCandidate(
                    collider,
                    axisPoint,
                    surfacePoint,
                    falloff);

                if (!_beamFieldCandidates.TryGetValue(body, out BeamFieldCandidate existing) ||
                    candidate.Falloff > existing.Falloff)
                {
                    _beamFieldCandidates[body] = candidate;
                }
            }

            float basePower = CalculateTickPower(stats.hitForce, tickInterval);

            foreach (KeyValuePair<Rigidbody, BeamFieldCandidate> pair in _beamFieldCandidates)
            {
                Rigidbody body = pair.Key;
                BeamFieldCandidate candidate = pair.Value;
                Vector3 radialDirection = body.worldCenterOfMass - candidate.AxisPoint;

                if (radialDirection.sqrMagnitude <= 0.0001f)
                    radialDirection = Vector3.Cross(beamDirection, Vector3.up);

                if (radialDirection.sqrMagnitude <= 0.0001f)
                    radialDirection = Vector3.Cross(beamDirection, Vector3.right);

                radialDirection.Normalize();

                var context = new EffectContext(
                    controller.gameObject,
                    candidate.Collider,
                    candidate.SurfacePoint,
                    -radialDirection,
                    radialDirection,
                    basePower * candidate.Falloff);

                controller.Effects.Apply(_contactPhysicsEffects, context);
            }
        }

        private bool IsBeamFieldPathClear(
            Vector3 axisPoint,
            Vector3 surfacePoint,
            Vector3 beamDirection,
            Collider targetCollider,
            Rigidbody targetBody,
            Transform ownerRoot)
        {
            Vector3 origin = axisPoint -
                             beamDirection.normalized * BeamFieldOcclusionOriginOffset;
            Vector3 toTarget = surfacePoint - origin;
            float distance = toTarget.magnitude;

            if (distance <= 0.0001f)
                return true;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                toTarget / distance,
                _beamFieldOcclusionHits,
                distance + BeamFieldOcclusionOriginOffset,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = _beamFieldOcclusionHits[index].collider;

                if (collider == null ||
                    collider == targetCollider ||
                    ResolveRigidbody(collider) == targetBody ||
                    IsInHierarchy(collider.transform, ownerRoot))
                {
                    continue;
                }

                return false;
            }

            return true;
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

        public static Vector3 ClosestPointOnSegment(
            Vector3 start,
            Vector3 end,
            Vector3 point)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;

            if (lengthSquared <= 0.0001f)
                return start;

            float t = Vector3.Dot(point - start, segment) / lengthSquared;
            return start + segment * Mathf.Clamp01(t);
        }

        public static float CalculateBeamFieldFalloff(
            float distance,
            float radius,
            float exponent)
        {
            if (radius <= 0f)
                return 0f;

            float normalizedDistance = Mathf.Clamp01(Mathf.Max(0f, distance) / radius);
            return Mathf.Pow(
                1f - normalizedDistance,
                Mathf.Max(0.01f, exponent));
        }

        private void OnValidate()
        {
            _effectTickInterval = Mathf.Max(0.01f, _effectTickInterval);
            _contactTickInterval = Mathf.Max(0.01f, _contactTickInterval);
            _beamFieldRadius = Mathf.Max(0f, _beamFieldRadius);
            _beamFieldFalloffExponent = Mathf.Max(0.01f, _beamFieldFalloffExponent);
        }

        private readonly struct BeamFieldCandidate
        {
            public readonly Collider Collider;
            public readonly Vector3 AxisPoint;
            public readonly Vector3 SurfacePoint;
            public readonly float Falloff;

            public BeamFieldCandidate(
                Collider collider,
                Vector3 axisPoint,
                Vector3 surfacePoint,
                float falloff)
            {
                Collider = collider;
                AxisPoint = axisPoint;
                SurfacePoint = surfacePoint;
                Falloff = falloff;
            }
        }
    }
}
