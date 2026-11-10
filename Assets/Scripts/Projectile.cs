using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    [System.Serializable]
    public class ProjectileSettings
    {
        public float speed = 20f;
        public float radius = 0.12f;
        [Tooltip("Wall bounces allowed; the next wall contact after the last bounce destroys the shot.")]
        public int maxBounces = 4;
        [Tooltip("Std-dev (degrees) of the Gaussian noise added to every reflection. 0 = perfect mirror bounce.")]
        public float bounceNoiseDegrees = 0f;
        public float lifetime = 5f;
        public Color color = Color.yellow;
        [Tooltip("Colour after the first ricochet.")]
        public Color ricochetColor = new Color(1f, 0.45f, 0.1f);
    }

    /// <summary>
    /// Kinematic projectile: moved by circle casts rather than the physics solver, so reflections are exact.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        static readonly List<Projectile> s_Active = new List<Projectile>();

        /// <summary>Every shot in flight.</summary>
        public static IReadOnlyList<Projectile> Active => s_Active;

        public Team Team { get; private set; }
        public int Bounces { get; private set; }
        /// <summary>Centre of the shot; during <see cref="IShootable.OnShot"/>, where it touched the target.</summary>
        public Vector2 Position => m_Position;
        public Vector2 Direction => m_Direction;
        public float Speed => m_Settings.speed;
        public float Radius => m_Settings.radius;

        ProjectileSettings m_Settings;
        Vector2 m_Position;
        Vector2 m_Direction;
        float m_Age;
        SpriteRenderer m_Sprite;
        /// <summary>Set when the shot uses the art library's sprites rather than a tinted circle.</summary>
        bool m_HasArt;
        /// <summary>A player shot's glow halo, shown once it has bounced and become lethal.</summary>
        SpriteRenderer m_Glow;
        float m_NextTrail;

        const float TrailInterval = 0.025f;
        const float TrailFade = 0.12f;

        bool IsHeavy => m_Settings.radius > 0.25f;
        /// <summary>A shot that has ricocheted: lethal, and drawn glowing (cyan for the player's, red for an enemy's).</summary>
        bool IsLive => Bounces > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Active.Clear();

        /// <summary>
        /// Fires a projectile from <paramref name="origin"/> (e.g. the gun pivot) and immediately advances it by
        /// <paramref name="preAdvance"/> (the barrel length), so a barrel poking into a wall still bounces correctly.
        /// </summary>
        public static Projectile Spawn(Team team, Vector2 origin, Vector2 direction, float preAdvance, ProjectileSettings settings)
        {
            var go = new GameObject(team + " Shot");
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * settings.radius * 2f;

            var p = go.AddComponent<Projectile>();
            p.Team = team;
            p.m_Settings = settings;
            p.m_Position = origin;
            p.m_Direction = direction.normalized;
            p.m_Sprite = go.AddComponent<SpriteRenderer>();
            p.m_Sprite.sprite = Shapes.Circle;
            p.m_Sprite.color = settings.color;
            p.m_Sprite.sortingOrder = 20;
            p.ApplyArt();

            p.Advance(preAdvance);
            return p;
        }

        /// <summary>
        /// Swaps the tinted circle for the paintball sprite at its own pixel size: a matte orange ball for a fresh player shot,
        /// magenta for an enemy's, a big purple one for a heavy. The live (bounced) look is switched on by <see cref="BecomeLive"/>.
        /// </summary>
        void ApplyArt()
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            var sprite = Team == Team.Player ? lib.paintballFresh : IsHeavy ? lib.paintballHeavy : lib.paintballEnemy;
            if (sprite == null)
                return;
            m_HasArt = true;
            transform.localScale = Vector3.one;
            m_Sprite.sprite = sprite;
            m_Sprite.color = Color.white;
            SortLayers.Set(m_Sprite, SortLayers.Effects, 5);
        }

        /// <summary>
        /// The first bounce makes a shot lethal, and it shows: a player shot turns bright cyan, an enemy's red-hot, each with a
        /// glow halo of its colour, a trail and a flash where it bounced, and the armed ping (lower for an enemy's).
        /// </summary>
        void BecomeLive(Vector2 contact)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            bool mine = Team == Team.Player;
            Sfx.Play(lib.ricochetArmed, mine ? 0.75f : 0.4f, mine ? 1f : 0.72f, 0.03f);
            SpriteFx.Play(mine ? lib.armFlash : lib.armFlashRed, contact, 11);
            var live = mine ? lib.paintballLive : IsHeavy ? lib.paintballHeavyLive : lib.paintballEnemyLive;
            var halo = mine ? lib.paintballLiveGlow : IsHeavy ? lib.paintballHeavyLiveGlow : lib.paintballEnemyLiveGlow;
            if (!m_HasArt || live == null)
                return;
            m_Sprite.sprite = live;
            if (halo != null)
            {
                var glow = new GameObject("Glow");
                glow.transform.SetParent(transform, false);
                m_Glow = glow.AddComponent<SpriteRenderer>();
                m_Glow.sprite = halo;
                SortLayers.Set(m_Glow, SortLayers.Effects, 4);
            }
        }

        /// <summary>Spark and ricochet sound for a bounce that doesn't change what the shot is.</summary>
        void Ricochet(Vector2 contact)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            SpriteFx.Play(lib.bounceSpark, contact, 10);
            bool mine = Team == Team.Player;
            Sfx.Play(lib.ricochet, mine ? 0.5f : 0.22f, mine ? 1.1f : (IsHeavy ? 0.7f : 0.9f), 0.05f);
        }

        /// <summary>The shot ends against a wall: a paint splat stays on it, in this shot's colour.</summary>
        void SplatOnWall(Vector2 contact, Vector2 normal)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            var splats = Team == Team.Player ? (IsLive ? lib.splatBlue : lib.splatOrange) : IsHeavy ? lib.splatPurple : lib.splatMagenta;
            SpriteFx.Splat(splats, contact, normal);
            Sfx.Play(lib.splatHarmless, Team == Team.Player ? 0.4f : 0.2f, 1.15f, 0.08f);
        }

        /// <summary>An enemy shot hitting the player: a splash of its paint.</summary>
        void SplashOnPlayer(Vector2 at)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            SpriteFx.Play(IsHeavy ? lib.hitPurple : lib.hitMagenta, at, 12);
        }

        void LateUpdate()
        {
            if (!m_HasArt || !IsLive)
                return;
            if (m_Glow != null)
                m_Glow.color = new Color(1f, 1f, 1f, 0.38f + 0.12f * Mathf.Sin(m_Age * 40f));
            if (m_Age >= m_NextTrail)
            {
                m_NextTrail = m_Age + TrailInterval;
                SpriteFx.Ghost(m_Sprite.sprite, m_Position, TrailFade, 0.45f, 3);
            }
        }

        public static void DestroyAll()
        {
            // Backwards: Destroy disables each shot straight away, and OnDisable removes it from s_Active.
            for (int i = s_Active.Count - 1; i >= 0; i--)
                if (s_Active[i] != null)
                    Destroy(s_Active[i].gameObject);
            s_Active.Clear();
        }

        void OnEnable() => s_Active.Add(this);
        void OnDisable() => s_Active.Remove(this);

        void Update()
        {
            m_Age += Time.deltaTime;
            if (m_Age >= m_Settings.lifetime)
            {
                Destroy(gameObject);
                return;
            }
            Advance(m_Settings.speed * Time.deltaTime);
        }

        void Advance(float distance)
        {
            if (!enabled)
                return;

            // Several iterations so a fast shot can bounce more than once per frame (e.g. into a corner).
            for (int i = 0; i < 8 && distance > 0f; i++)
            {
                if (!ShotPhysics.Cast(m_Position, m_Direction, distance, m_Settings.radius, Team, false, out var hit))
                {
                    m_Position += m_Direction * distance;
                    break;
                }

                m_Position = hit.centroid;
                distance -= hit.distance;

                if (hit.target != null)
                {
                    if (hit.target.Team == Team.Player && IsLive)
                        SplashOnPlayer(m_Position);
                    hit.target.OnShot(this);
                    Consume();
                    return;
                }

                // Where the ball's edge touched the surface.
                Vector2 contact = m_Position - hit.normal * m_Settings.radius;
                if (Bounces >= m_Settings.maxBounces)
                {
                    SplatOnWall(contact, hit.normal);
                    Consume();
                    return;
                }

                Bounces++;
                m_Direction = ShotPhysics.Reflect(m_Direction, hit.normal, m_Settings.bounceNoiseDegrees);
                m_Position += hit.normal * ShotPhysics.Skin;
                if (!m_HasArt)
                    m_Sprite.color = m_Settings.ricochetColor;
                if (Bounces == 1)
                    BecomeLive(contact);
                else
                    Ricochet(contact);
            }
            transform.position = m_Position;
        }

        void Consume()
        {
            transform.position = m_Position;
            enabled = false;
            Destroy(gameObject);
        }
    }
}
