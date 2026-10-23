using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// Stationary gunner. Predicts where the player will be, ray traces a ricochet shot off level geometry
    /// to reach that point, and fires a projectile whose bounces carry Gaussian noise so it can miss.
    /// Only dies to player shots that have ricocheted at least once.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Enemy : MonoBehaviour, IShootable
    {
        public enum Prediction
        {
            [Tooltip("Lead the player by one physics step (position + velocity * fixedDeltaTime).")]
            NextFrame,
            [Tooltip("Lead the player by the projectile's travel time along the planned path.")]
            FlightTime,
        }

        public static readonly List<Enemy> All = new List<Enemy>();
        public static bool ShowAimTraces = true;

        static readonly List<ShotPlan> s_Plans = new List<ShotPlan>();

        [Header("Targeting")]
        public Prediction prediction = Prediction.FlightTime;
        public float engageRange = 18f;
        [Tooltip("Seconds between shots, picked uniformly from [x, y].")]
        public Vector2 fireInterval = new Vector2(1.6f, 2.8f);
        public float barrelLength = 0.75f;
        public ProjectileSettings shot = new ProjectileSettings
        {
            speed = 11f,
            radius = 0.14f,
            maxBounces = 2,
            bounceNoiseDegrees = 7f,
            lifetime = 5f,
            color = new Color(1f, 0.25f, 0.85f),
            ricochetColor = new Color(1f, 0.25f, 0.85f),
        };

        [Header("Body")]
        public Vector2 size = new Vector2(0.8f, 1.6f);
        [Tooltip("Player shots must have ricocheted at least this many times to kill.")]
        public int ricochetsRequiredToKill = 1;

        public Team Team => Team.Enemy;

        static readonly Color BodyColor = new Color(0.85f, 0.2f, 0.2f);
        static readonly Color BlockedColor = new Color(0.4f, 0.9f, 1f);

        Transform m_GunPivot;
        SpriteRenderer m_Body;
        float m_FireTimer;
        float m_BlockedFlash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            ShowAimTraces = true;
        }

        void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.size = size;

            m_Body = Shapes.Box(transform, "Body", BodyColor, Vector2.zero, size, 5);
            m_GunPivot = new GameObject("Gun Pivot").transform;
            m_GunPivot.SetParent(transform, false);
            m_GunPivot.localPosition = new Vector2(0f, size.y * 0.2f);
            Shapes.Box(m_GunPivot, "Barrel", new Color(0.2f, 0.2f, 0.22f), new Vector2(barrelLength * 0.5f, 0f), new Vector2(barrelLength, 0.16f), 6);

            m_FireTimer = Random.Range(0.5f, fireInterval.y);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (m_BlockedFlash > 0f)
            {
                m_BlockedFlash -= Time.deltaTime;
                m_Body.color = m_BlockedFlash > 0f ? BlockedColor : BodyColor;
            }

            var player = PlayerController.Instance;
            if (player == null || player.IsDead)
                return;
            if (Vector2.Distance(m_GunPivot.position, player.AimPoint) > engageRange)
                return;

            m_FireTimer -= Time.deltaTime;
            if (m_FireTimer > 0f)
                return;

            if (TryPlanShot(player, out var plan))
            {
                Fire(plan);
                m_FireTimer = Random.Range(fireInterval.x, fireInterval.y);
            }
            else
            {
                m_FireTimer = 0.25f; // no ricochet path right now; look again shortly
            }
        }

        bool TryPlanShot(PlayerController player, out ShotPlan plan)
        {
            plan = default;
            Vector2 origin = m_GunPivot.position;

            float lead = prediction == Prediction.NextFrame
                ? Time.fixedDeltaTime
                : Vector2.Distance(origin, player.AimPoint) / shot.speed;

            if (BankShotSolver.FindBankShots(origin, PredictPlayer(player, lead), shot.radius, Team, s_Plans) == 0)
                return false;

            // Prefer short paths (less time for the player to react), with a little variety.
            s_Plans.Sort((a, b) => a.pathLength.CompareTo(b.pathLength));
            plan = s_Plans[Random.Range(0, Mathf.Min(2, s_Plans.Count))];

            if (prediction == Prediction.FlightTime)
            {
                // Lead time depends on the chosen path, so re-solve a couple of times; keep the solution
                // closest to the previous one so the iteration converges on the same ricochet surface.
                for (int i = 0; i < 2; i++)
                {
                    lead = Mathf.Max(0f, plan.pathLength - barrelLength) / shot.speed;
                    if (BankShotSolver.FindBankShots(origin, PredictPlayer(player, lead), shot.radius, Team, s_Plans) == 0)
                        break;
                    plan = ClosestTo(plan.direction);
                }
            }
            return true;
        }

        /// <summary>Extrapolates the player along their current velocity, stopping at the first wall in the way.</summary>
        static Vector2 PredictPlayer(PlayerController player, float lead)
        {
            Vector2 from = player.AimPoint;
            Vector2 delta = player.Velocity * lead;
            float dist = delta.magnitude;
            if (dist < 1e-4f)
                return from;
            if (ShotPhysics.Cast(from, delta / dist, dist, 0.3f, Team.Player, true, out var hit))
                return hit.centroid;
            return from + delta;
        }

        static ShotPlan ClosestTo(Vector2 direction)
        {
            var best = s_Plans[0];
            foreach (var p in s_Plans)
                if (Vector2.Dot(p.direction, direction) > Vector2.Dot(best.direction, direction))
                    best = p;
            return best;
        }

        void Fire(ShotPlan plan)
        {
            m_GunPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(plan.direction.y, plan.direction.x) * Mathf.Rad2Deg);
            Projectile.Spawn(Team.Enemy, plan.origin, plan.direction, barrelLength, shot);

            if (ShowAimTraces)
            {
                var c = shot.color;
                c.a = 0.6f;
                AimTrace.Show(c, 0.6f, plan.origin, plan.bouncePoint, plan.target);
            }
        }

        public void OnShot(Projectile projectile)
        {
            if (projectile.Bounces < ricochetsRequiredToKill)
            {
                m_BlockedFlash = 0.15f; // direct hit: harmless
                return;
            }
            Destroy(gameObject);
        }
    }
}
