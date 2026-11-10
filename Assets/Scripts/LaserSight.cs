using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// Line from the gun along the path the next shot will take, up to its first ricochet plus a short fading tail.
    /// Traced with the same circle casts as a real projectile, so it stops wherever the shot would (including on enemies).
    /// </summary>
    public class LaserSight : MonoBehaviour
    {
        public Color color = new Color(1f, 0.2f, 0.2f, 0.6f);
        public float width = 0.04f;
        [Tooltip("How far the line carries on after the first ricochet.")]
        public float tailLength = 1.5f;
        public float range = 60f;

        const int TailPieces = 4;
        /// <summary>Caps how far a grazing line is stretched from the shot's centre to the surface, in shot radii.</summary>
        const float MaxSurfaceReach = 4f;
        const int SortingOrder = 4;

        SpriteRenderer m_Beam;
        readonly SpriteRenderer[] m_Tail = new SpriteRenderer[TailPieces];
        /// <summary>The bounce indicator: where the shot ricochets and turns lethal. Only with the art library.</summary>
        SpriteRenderer m_BounceIcon;
        /// <summary>The dashed 1 px beam sprite, repeated along the line; null draws a plain stretched box.</summary>
        Sprite m_Dash;

        static readonly Color BounceIconColor = new Color(0.16f, 0.83f, 1f, 0.9f);

        void Awake()
        {
            var lib = Art.Lib;
            m_Dash = lib != null ? lib.laser : null;
            m_Beam = Piece("Beam");
            for (int i = 0; i < TailPieces; i++)
                m_Tail[i] = Piece("Tail");
            if (lib != null && lib.bounceIcon != null)
            {
                var go = new GameObject("Bounce Indicator");
                go.transform.SetParent(transform, false);
                m_BounceIcon = go.AddComponent<SpriteRenderer>();
                m_BounceIcon.sprite = lib.bounceIcon;
                m_BounceIcon.color = BounceIconColor;
                SortLayers.Set(m_BounceIcon, SortLayers.UI, 1);
            }
            Hide();
        }

        SpriteRenderer Piece(string name)
        {
            if (m_Dash == null)
                return Shapes.Box(transform, name, color, Vector2.zero, Vector2.one, SortingOrder);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = m_Dash;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            SortLayers.Set(sr, SortLayers.UI, 0);
            return sr;
        }

        /// <summary>
        /// Traces a shot fired the way <see cref="Projectile.Spawn"/> would fire it: from <paramref name="origin"/> (the gun pivot),
        /// drawn from the barrel tip.
        /// </summary>
        public void Trace(Vector2 origin, Vector2 dir, float barrelLength, ProjectileSettings shot)
        {
            if (!ShotPhysics.Cast(origin, dir, range, shot.radius, Team.Player, false, out var hit))
            {
                Place(m_Beam, origin + dir * barrelLength, origin + dir * range, color);
                HideTail();
                return;
            }

            // The cast follows the shot's centre, which turns a radius short of the surface.
            // Carry on to where the line itself meets the surface, so it visibly touches the wall and mirrors off it.
            float reach = Mathf.Min(shot.radius / -Vector2.Dot(dir, hit.normal), shot.radius * MaxSurfaceReach);
            Vector2 impact = hit.centroid + dir * reach;
            Place(m_Beam, origin + dir * Mathf.Min(barrelLength, hit.distance), impact, color);

            if (hit.target != null || shot.maxBounces <= 0)
            {
                HideTail();
                return;
            }

            if (m_BounceIcon != null)
            {
                // Off the surface along its normal, so it marks the bounce without covering the wall.
                m_BounceIcon.enabled = true;
                m_BounceIcon.transform.SetPositionAndRotation(impact + hit.normal * 0.32f, Quaternion.identity);
            }
            Vector2 bounced = ShotPhysics.Reflect(dir, hit.normal);
            // Cut short if the ricochet runs straight into something else (e.g. aiming into a corner).
            float tail = tailLength;
            if (ShotPhysics.Cast(hit.centroid + hit.normal * ShotPhysics.Skin, bounced, tailLength, shot.radius, Team.Player, false, out var next))
                tail = next.distance;

            // Fade out in steps along the tail.
            float piece = tail / TailPieces;
            for (int i = 0; i < TailPieces; i++)
            {
                var c = color;
                c.a *= 1f - (i + 0.5f) / TailPieces;
                Place(m_Tail[i], impact + bounced * (piece * i), impact + bounced * (piece * (i + 1)), c);
            }
        }

        public void Hide()
        {
            m_Beam.enabled = false;
            HideTail();
        }

        void HideTail()
        {
            foreach (var sr in m_Tail)
                sr.enabled = false;
            if (m_BounceIcon != null)
                m_BounceIcon.enabled = false;
        }

        void Place(SpriteRenderer sr, Vector2 a, Vector2 b, Color c)
        {
            Vector2 d = b - a;
            sr.enabled = d.sqrMagnitude > 1e-6f;
            if (!sr.enabled)
                return;
            sr.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
            if (m_Dash != null)
                sr.size = new Vector2(d.magnitude, m_Dash.bounds.size.y); // dashes repeat along it at the art's pixel size
            else
                sr.transform.localScale = new Vector3(d.magnitude, width, 1f);
            sr.color = c;
        }
    }
}
