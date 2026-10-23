using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    public struct ShotPlan
    {
        public Vector2 origin;
        public Vector2 direction;      // initial firing direction
        public Vector2 bouncePoint;    // projectile centre where it meets the surface
        public Vector2 target;         // point the reflected leg passes through
        public float pathLength;       // origin → bounce → target
        public float signedMiss;       // perpendicular offset of the target from the reflected leg
    }

    /// <summary>
    /// Finds single-ricochet shots ("bank shots") onto a target point by ray tracing:
    /// fan out rays from the shooter, perfectly reflect each off the first surface it meets
    /// (floor, ceiling, wall or any other level geometry), then measure how far the reflected ray
    /// passes from the target. Wherever that signed miss distance changes sign between two
    /// neighbouring rays, bisection homes in on the exact angle.
    /// </summary>
    public static class BankShotSolver
    {
        const int RayCount = 240;            // 1.5° between rays
        const int BisectionSteps = 18;
        const float MaxRange = 45f;
        const float HitTolerance = 0.1f;     // a solution must pass at least this close to the target

        static readonly bool[] s_Valid = new bool[RayCount];
        static readonly float[] s_Miss = new float[RayCount];

        /// <summary>Fills <paramref name="results"/> with every bank shot from <paramref name="origin"/> that hits <paramref name="target"/>.</summary>
        public static int FindBankShots(Vector2 origin, Vector2 target, float radius, Team shooterTeam, List<ShotPlan> results)
        {
            results.Clear();
            const float step = 360f / RayCount;

            for (int i = 0; i < RayCount; i++)
            {
                s_Valid[i] = Trace(origin, AngleToDir(i * step), target, radius, shooterTeam, out var plan);
                s_Miss[i] = plan.signedMiss;
            }

            for (int i = 0; i < RayCount; i++)
            {
                int j = (i + 1) % RayCount;
                if (!s_Valid[i] || !s_Valid[j] || Mathf.Sign(s_Miss[i]) == Mathf.Sign(s_Miss[j]))
                    continue;

                // Sign changes also happen where neighbouring rays hit different surfaces; those fail the tolerance check.
                if (Bisect(origin, target, radius, shooterTeam, i * step, s_Miss[i], step, out var plan)
                    && Mathf.Abs(plan.signedMiss) <= HitTolerance)
                    results.Add(plan);
            }
            return results.Count;
        }

        static bool Bisect(Vector2 origin, Vector2 target, float radius, Team team, float lowAngle, float lowMiss, float width, out ShotPlan plan)
        {
            plan = default;
            float lo = lowAngle, hi = lowAngle + width;
            for (int k = 0; k < BisectionSteps; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (!Trace(origin, AngleToDir(mid), target, radius, team, out plan))
                    return false;
                if (Mathf.Sign(plan.signedMiss) == Mathf.Sign(lowMiss))
                    lo = mid;
                else
                    hi = mid;
            }
            return true;
        }

        /// <summary>Follows one ray through a single perfect reflection and scores it against the target.</summary>
        static bool Trace(Vector2 origin, Vector2 dir, Vector2 target, float radius, Team team, out ShotPlan plan)
        {
            plan = default;

            if (!ShotPhysics.Cast(origin, dir, MaxRange, radius, team, false, out var first))
                return false;
            if (first.target != null)
                return false; // would hit the player directly — enemies only take ricochet shots

            Vector2 bounceDir = ShotPhysics.Reflect(dir, first.normal);
            Vector2 legStart = first.centroid + first.normal * ShotPhysics.Skin;
            Vector2 toTarget = target - legStart;
            float along = Vector2.Dot(toTarget, bounceDir);
            if (along <= 0f)
                return false; // target is behind the reflected ray

            // Anything solid between the bounce and the target blocks the shot.
            if (ShotPhysics.Cast(legStart, bounceDir, along, radius, team, true, out _))
                return false;

            plan = new ShotPlan
            {
                origin = origin,
                direction = dir,
                bouncePoint = first.centroid,
                target = target,
                pathLength = first.distance + along,
                signedMiss = bounceDir.x * toTarget.y - bounceDir.y * toTarget.x,
            };
            return true;
        }

        static Vector2 AngleToDir(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
