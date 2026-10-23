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

        public Team Team { get; private set; }
        public int Bounces { get; private set; }

        ProjectileSettings m_Settings;
        Vector2 m_Position;
        Vector2 m_Direction;
        float m_Age;
        SpriteRenderer m_Sprite;

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

            p.Advance(preAdvance);
            return p;
        }

        public static void DestroyAll()
        {
            foreach (var p in s_Active)
                if (p != null)
                    Destroy(p.gameObject);
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
                    hit.target.OnShot(this);
                    Consume();
                    return;
                }

                if (Bounces >= m_Settings.maxBounces)
                {
                    Consume();
                    return;
                }

                Bounces++;
                m_Direction = ShotPhysics.Reflect(m_Direction, hit.normal, m_Settings.bounceNoiseDegrees);
                m_Position += hit.normal * ShotPhysics.Skin;
                m_Sprite.color = m_Settings.ricochetColor;
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
