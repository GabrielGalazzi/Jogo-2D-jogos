using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    public struct ShotHit
    {
        public Vector2 centroid;   // projectile centre at the moment of contact
        public Vector2 normal;
        public float distance;
        public IShootable target;  // null when the hit is level geometry
    }

    /// <summary>
    /// Circle-cast and reflection math shared by projectiles and the enemy aim solver,
    /// so a path the solver plans is exactly the path a real projectile travels.
    /// </summary>
    public static class ShotPhysics
    {
        /// <summary>How far a projectile is pushed off a surface after bouncing, so the next cast doesn't start inside it.</summary>
        public const float Skin = 0.02f;

        /// <summary>Noisy reflections are clamped to leave the surface at no less than this angle.</summary>
        const float MinExitAngle = 4f;

        static readonly List<RaycastHit2D> s_Hits = new List<RaycastHit2D>(16);
        static readonly System.Comparison<RaycastHit2D> s_ByDistance = (a, b) => a.distance.CompareTo(b.distance);
        static ContactFilter2D s_Filter = CreateFilter();

        static ContactFilter2D CreateFilter()
        {
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            return filter;
        }

        /// <summary>
        /// Sweeps a circle and returns the closest relevant hit. Characters on <paramref name="ownTeam"/> are passed through;
        /// with <paramref name="wallsOnly"/> every character is passed through.
        /// </summary>
        public static bool Cast(Vector2 origin, Vector2 dir, float distance, float radius, Team ownTeam, bool wallsOnly, out ShotHit hit)
        {
            hit = default;
            s_Hits.Clear();
            int count = Physics2D.CircleCast(origin, radius, dir, s_Filter, s_Hits, distance);
            if (count == 0)
                return false;
            if (count > 1)
                s_Hits.Sort(s_ByDistance);

            foreach (var h in s_Hits)
            {
                // Surfaces we're moving away from (e.g. the one we just bounced off) can't be impacts.
                if (Vector2.Dot(dir, h.normal) >= 0f)
                    continue;
                var target = h.collider.GetComponentInParent<IShootable>();
                if (target != null && (wallsOnly || target.Team == ownTeam))
                    continue;

                hit = new ShotHit { centroid = h.centroid, normal = h.normal, distance = h.distance, target = target };
                return true;
            }
            return false;
        }

        /// <summary>Perfect mirror reflection: angle of incidence equals angle of reflection.</summary>
        public static Vector2 Reflect(Vector2 dir, Vector2 normal) => Vector2.Reflect(dir, normal).normalized;

        /// <summary>
        /// Mirror reflection, then rotated by an angle drawn from N(0, sigma²) degrees.
        /// The result is clamped so the noise can never send the shot back into the surface.
        /// </summary>
        public static Vector2 Reflect(Vector2 dir, Vector2 normal, float noiseSigmaDegrees)
        {
            Vector2 reflected = Reflect(dir, normal);
            if (noiseSigmaDegrees <= 0f)
                return reflected;

            reflected = Rotate(reflected, NextGaussian() * noiseSigmaDegrees);

            float minOut = Mathf.Sin(MinExitAngle * Mathf.Deg2Rad);
            float outward = Vector2.Dot(reflected, normal);
            if (outward < minOut)
            {
                Vector2 tangent = reflected - normal * outward;
                if (tangent.sqrMagnitude < 1e-6f)
                    tangent = new Vector2(-normal.y, normal.x);
                reflected = normal * minOut + tangent.normalized * Mathf.Cos(MinExitAngle * Mathf.Deg2Rad);
            }
            return reflected.normalized;
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        /// <summary>Standard normal sample (Box–Muller).</summary>
        public static float NextGaussian()
        {
            float u1 = Mathf.Max(1e-7f, 1f - Random.value);
            float u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
