using UnityEngine;

public static class BorzoiGrenadeAim
{
    private const float LandingTolerance = 0.45f;
    private const float GrenadeRadius = 0.09f;
    private const int TrajectorySteps = 8;
    private static readonly float[] SearchRadii = { 0.45f, 0.8f };
    private static readonly RaycastHit[] TrajectoryHits = new RaycastHit[16];

    public static bool TryFindVelocity(Vector3 origin, Vector3 playerPosition, float blastRadius,
        float flightTime, Transform thrower, bool flash, out Vector3 velocity)
    {
        velocity = default;
        if (blastRadius <= LandingTolerance)
            return false;

        int robotMask = LayerMask.GetMask("Enemy");
        int worldMask = Physics.DefaultRaycastLayers & ~(robotMask | LayerMask.GetMask("Player", "PlayerHead"));
        int groundMask = worldMask & ~LayerMask.GetMask("CarryObjects");

        Vector3 towardThrower = Vector3.ProjectOnPlane(origin - playerPosition, Vector3.up).normalized;
        if (TryAim(playerPosition + towardThrower * 1.25f, out velocity) ||
            TryAim(playerPosition, out velocity))
            return true;

        foreach (float fraction in SearchRadii)
        {
            for (int angle = 0; angle < 360; angle += 45)
            {
                Vector3 offset = Quaternion.AngleAxis(angle, Vector3.up) * towardThrower *
                    (blastRadius * fraction);
                if (TryAim(playerPosition + offset, out velocity))
                    return true;
            }
        }

        return false;

        bool TryAim(Vector3 candidate, out Vector3 launchVelocity)
        {
            launchVelocity = default;
            if (!Physics.Raycast(candidate + Vector3.up, Vector3.down, out RaycastHit floor,
                    2.25f, groundMask, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.5f)
                return false;

            Vector3 landing = floor.point + Vector3.up * (GrenadeRadius + 0.04f);
            Vector3 playerCenter = playerPosition + Vector3.up * (flash ? 1.6f : 0.8f);
            float targetRadius = blastRadius - LandingTolerance;
            if ((playerCenter - landing).sqrMagnitude > targetRadius * targetRadius ||
                Physics.Linecast(landing, playerCenter, worldMask, QueryTriggerInteraction.Ignore) ||
                HasRobotInRadius(landing, blastRadius + LandingTolerance, robotMask, thrower, flash))
                return false;

            Vector3 gravity = Physics.gravity;
            Vector3 initialVelocity = (landing - origin) / flightTime - gravity * (0.5f * flightTime);
            Vector3 previous = origin;
            for (int step = 1; step <= TrajectorySteps; step++)
            {
                float time = flightTime * step / TrajectorySteps;
                Vector3 point = origin + initialVelocity * time + gravity * (0.5f * time * time);
                Vector3 segment = point - previous;
                if (IsTrajectoryBlocked(previous, segment, thrower))
                    return false;

                previous = point;
            }

            launchVelocity = initialVelocity;
            return true;
        }
    }

    private static bool IsTrajectoryBlocked(Vector3 from, Vector3 segment, Transform thrower)
    {
        float length = segment.magnitude;
        if (length <= 0.001f)
            return false;

        int count = Physics.SphereCastNonAlloc(from, GrenadeRadius, segment / length, TrajectoryHits,
            length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == TrajectoryHits.Length)
            return true;

        for (int i = 0; i < count; i++)
        {
            if (!TrajectoryHits[i].collider.transform.IsChildOf(thrower))
                return true;
        }

        return false;
    }

    private static bool HasRobotInRadius(Vector3 landing, float radius, int robotMask, Transform thrower, bool flash)
    {
        foreach (var collider in Physics.OverlapSphere(landing, radius, robotMask, QueryTriggerInteraction.Ignore))
        {
            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.CurrentState is EnemyDeathState)
                continue;

            // The flash only affects the player. Allow its thrower to stand within the flash radius.
            if (flash && enemy.transform == thrower)
                continue;

            return true;
        }

        return false;
    }
}
