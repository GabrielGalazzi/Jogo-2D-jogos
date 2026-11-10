using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    public enum EnemyWeapon
    {
        [Tooltip("One projectile.")]
        Standard,
        [Tooltip("Two projectiles at once, fanned slightly apart.")]
        Twin,
        [Tooltip("One large, slow projectile, fired at a slower rate.")]
        Heavy,
    }

    public enum EnemyMount
    {
        [Tooltip("Stands on the surface below.")]
        Floor,
        [Tooltip("Hangs upside down from the surface above.")]
        Ceiling,
    }

    /// <summary>
    /// Gunner. Predicts where the player will be, ray traces a ricochet shot off level geometry
    /// to reach that point, and fires projectiles whose bounces carry Gaussian noise so they can miss.
    /// Holds fire until it has been on screen at least once, so nothing shoots at the player from out of view,
    /// and while the player is still in the level's safe zone. The boss (see <see cref="Boss"/>) builds on it.
    /// Only dies to player shots that have ricocheted at least once. With a <see cref="duckChance"/>, it sometimes ducks
    /// as one of those shots is about to hit, which saves it if the shot was coming in flat at head height.
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
        [Tooltip("Physics steps of the player's movement averaged to lead a shot, so a change of direction that lasts a single step doesn't throw the aim.")]
        [Min(1)] public int aimSmoothingSteps = 5;
        public float engageRange = 18f;
        [Tooltip("Seconds between shots, picked uniformly from [x, y].")]
        public Vector2 fireInterval = new Vector2(1.6f, 2.8f);
        [Tooltip("Delay before the first shot once the enemy has appeared on screen, picked uniformly from [x, y].")]
        public Vector2 sightedDelay = new Vector2(0.6f, 1.4f);
        public float barrelLength = 0.75f;

        [Header("Weapon")]
        public EnemyWeapon weapon = EnemyWeapon.Standard;
        [Tooltip("Projectile for the Standard and Twin weapons.")]
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
        [Tooltip("Angle between the two projectiles of a Twin shot.")]
        public float twinSpreadDegrees = 4f;
        public ProjectileSettings heavyShot = new ProjectileSettings
        {
            speed = 6.5f,
            radius = 0.32f,
            maxBounces = 2,
            bounceNoiseDegrees = 5f,
            lifetime = 9f,
            color = new Color(0.65f, 0.35f, 1f),
            ricochetColor = new Color(0.65f, 0.35f, 1f),
        };
        [Tooltip("Heavy weapons wait this many times longer between shots.")]
        public float heavyFireIntervalScale = 1.5f;

        [Header("Movement")]
        public EnemyMount mount = EnemyMount.Floor;
        [Tooltip("Height of the periodic hop; 0 = stands still. Ceiling-mounted enemies drop down and spring back up.")]
        public float hopHeight;
        [Tooltip("Seconds between hops, picked uniformly from [x, y].")]
        public Vector2 hopInterval = new Vector2(1.2f, 2.4f);
        [Tooltip("Same as the player's (Physics2D gravity × 3), so hops read the same as player jumps.")]
        public float hopGravity = 29.43f;

        [Header("Ducking")]
        [Tooltip("Chance to duck when a lethal player shot is about to hit, rolled once per shot. 0 = never ducks.")]
        [Range(0f, 1f)] public float duckChance;
        [Tooltip("Same as the player's: the body shrinks by this fraction, towards the surface it's mounted on.")]
        [Range(0f, 0.9f)] public float duckHeightReduction = 0.4f;
        [Tooltip("How many seconds before impact an incoming shot is noticed.")]
        public float duckWarningTime = 0.3f;
        [Tooltip("Seconds it stays down after ducking.")]
        public float duckDuration = 0.6f;

        [Header("Body")]
        public Vector2 size = new Vector2(0.8f, 1.6f);
        [Tooltip("Player shots must have ricocheted at least this many times to kill.")]
        public int ricochetsRequiredToKill = 1;

        public Team Team => Team.Enemy;
        /// <summary>False until the enemy has been on screen; until then it holds fire.</summary>
        public bool HasBeenSeen { get; private set; }
        public bool IsDucking { get; private set; }

        protected Transform GunPivot => m_GunPivot;
        protected BoxCollider2D Hitbox => m_Collider;
        protected Rigidbody2D KinematicBody => m_Rigidbody;
        protected ProjectileSettings ShotSettings(EnemyWeapon gun) => gun == EnemyWeapon.Heavy ? heavyShot : shot;

        protected static readonly Color BlockedColor = new Color(0.4f, 0.9f, 1f);
        static readonly Color BarrelColor = new Color(0.2f, 0.2f, 0.22f);
        static readonly System.Predicate<Projectile> s_IsGone = p => p == null;

        protected SpriteRenderer m_Body;
        /// <summary>The character art; null without the art library, when the placeholder box (<see cref="m_Body"/>) shows instead.</summary>
        protected CharacterVisual m_Art;
        /// <summary>Until then the gun keeps pointing the way it last fired; after that it rests level, facing the player.</summary>
        protected float m_AimHoldUntil;
        Transform m_GunPivot;
        BoxCollider2D m_Collider;
        Rigidbody2D m_Rigidbody;
        /// <summary>Shots already rolled against <see cref="duckChance"/>.</summary>
        readonly List<Projectile> m_RolledShots = new List<Projectile>();
        float m_DuckUntil;
        Vector2 m_RestPosition;
        Vector2 m_HopDirection;
        float m_FireTimer;
        float m_FlashTimer;
        Color m_FlashColor;
        float m_HopTimer;
        float m_HopAge = -1f; // < 0 while not hopping

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            ShowAimTraces = true;
        }

        public static Color BodyColor(EnemyWeapon weapon)
        {
            switch (weapon)
            {
                case EnemyWeapon.Twin: return new Color(0.95f, 0.5f, 0.15f);
                case EnemyWeapon.Heavy: return new Color(0.55f, 0.25f, 0.85f);
                default: return new Color(0.85f, 0.2f, 0.2f);
            }
        }

        /// <summary>
        /// Puts the base of the body on <paramref name="anchor"/>: standing on it, or hanging from it when ceiling-mounted.
        /// Call before the enemy is first enabled, as that is where it rests between hops.
        /// </summary>
        public void PlaceOn(Vector2 anchor)
        {
            bool ceiling = mount == EnemyMount.Ceiling;
            Vector2 up = ceiling ? Vector2.down : Vector2.up;
            transform.SetPositionAndRotation(anchor + up * size.y * 0.5f, Quaternion.Euler(0f, 0f, ceiling ? 180f : 0f));
        }

        protected virtual void Awake()
        {
            m_Collider = GetComponent<BoxCollider2D>();
            m_Collider.size = size;

            // Kinematic so hops move the collider through the physics engine rather than teleporting a static one.
            if (!TryGetComponent(out m_Rigidbody))
                m_Rigidbody = gameObject.AddComponent<Rigidbody2D>();
            m_Rigidbody.bodyType = RigidbodyType2D.Kinematic;
            m_Rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            m_RestPosition = transform.position;
            m_HopDirection = transform.up;
            m_HopTimer = Random.Range(hopInterval.x, hopInterval.y);

            m_Body = Shapes.Box(transform, "Body", BodyColor(weapon), Vector2.zero, size, 5);
            // The art stands on the feet (the surface it's mounted on) and never scales; the box stays as the hitbox frame, hidden.
            m_Art = CharacterVisual.Create(transform, CharacterArtFor(), -size.y * 0.5f, 0);
            if (m_Art != null)
            {
                m_Body.enabled = false;
                m_Art.footstepPitch = 0.8f;
            }
            m_GunPivot = new GameObject("Gun Pivot").transform;
            m_GunPivot.SetParent(transform, false);
            m_GunPivot.localPosition = new Vector2(0f, size.y * 0.2f);
            BuildBarrel();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        protected virtual void Update()
        {
            if (m_FlashTimer > 0f)
                m_FlashTimer -= Time.deltaTime;
            m_Body.color = m_FlashTimer > 0f ? m_FlashColor : BodyColor(weapon);

            if (duckChance > 0f)
                UpdateDucking();
            UpdateArt();

            if (!HasBeenSeen)
            {
                if (!IsOnScreen())
                    return;
                HasBeenSeen = true;
                m_FireTimer = Random.Range(sightedDelay.x, sightedDelay.y);
            }

            var player = PlayerController.Instance;
            // While it can't fire the fire timer is held too, so the first shot still waits out the sighted delay.
            if (player == null || player.IsDead || !CanFire(player))
                return;
            if (Vector2.Distance(m_GunPivot.position, player.AimPoint) > engageRange)
                return;

            m_FireTimer -= Time.deltaTime;
            if (m_FireTimer > 0f)
                return;
            // With no ricochet path right now, look again shortly.
            m_FireTimer = TryFire(player) ? NextFireInterval() : RetryInterval;
        }

        /// <summary>This enemy's art: one per weapon type (the red, orange and purple teams).</summary>
        protected virtual CharacterArt CharacterArtFor()
        {
            var lib = Art.Lib;
            if (lib == null)
                return null;
            switch (weapon)
            {
                case EnemyWeapon.Twin: return lib.enemyTwin;
                case EnemyWeapon.Heavy: return lib.enemyHeavy;
                default: return lib.enemyStandard;
            }
        }

        /// <summary>The marker sprite for <paramref name="gun"/>, in this enemy's team colour; null for the placeholder barrels.</summary>
        protected virtual Sprite GunSprite(EnemyWeapon gun)
        {
            var lib = Art.Lib;
            if (lib == null)
                return null;
            switch (gun)
            {
                case EnemyWeapon.Twin: return lib.twinEnemy;
                case EnemyWeapon.Heavy: return lib.heavyEnemy;
                default: return lib.standardEnemy;
            }
        }

        /// <summary>The paint burst it goes out in.</summary>
        protected virtual Flipbook EliminationBurst(ArtLibrary lib) => lib.burstPaint;

        /// <summary>Tint of the art when it isn't flashing.</summary>
        protected virtual Color ArtTint => Color.white;

        /// <summary>Whether the art faces its own +x (right, standing on the floor): towards the player.</summary>
        protected virtual bool FacesRight()
        {
            var player = PlayerController.Instance;
            return player == null || transform.InverseTransformPoint(player.transform.position).x >= 0f;
        }

        /// <summary>Feeds the Animator (hopping, ducking), faces the player, and rests the gun level between shots.</summary>
        protected virtual void UpdateArt()
        {
            bool right = FacesRight();
            if (Time.time >= m_AimHoldUntil)
                RestGun(m_GunPivot, right);
            if (m_Art == null)
                return;
            m_Art.Renderer.color = m_FlashTimer > 0f ? m_FlashColor : ArtTint;
            m_Art.Face(right);
            var a = m_Art.Animator;
            bool hopping = m_HopAge >= 0f;
            a.SetBool(CharacterVisual.Grounded, !hopping);
            a.SetFloat(CharacterVisual.VelocityY, hopping ? Mathf.Sqrt(2f * hopGravity * hopHeight) - hopGravity * m_HopAge : 0f);
            a.SetBool(CharacterVisual.Ducking, IsDucking);
        }

        /// <summary>
        /// The gun sits in the hands: after the Animator has picked this frame's sprite, the gun pivot (the grip, where shots
        /// leave from and the aim solver plans from) moves to that frame's hands, hopping and ducking included.
        /// </summary>
        protected virtual void LateUpdate()
        {
            if (m_Art != null && m_Art.TryHandsLocal(out var hands))
                m_GunPivot.localPosition = hands;
        }

        /// <summary>Points <paramref name="pivot"/>'s gun level, to the right or left of the body (in its own frame).</summary>
        protected static void RestGun(Transform pivot, bool right)
        {
            pivot.localRotation = Quaternion.Euler(0f, 0f, right ? 0f : 180f);
            pivot.localScale = new Vector3(1f, right ? 1f : -1f, 1f);
        }

        /// <summary>Whether it may shoot right now. Enemies hold fire while the player is in the safe zone.</summary>
        protected virtual bool CanFire(PlayerController player) => !OneWayDoor.PlayerIsSafe;

        /// <summary>Seconds from one shot to the next.</summary>
        protected virtual float NextFireInterval() =>
            Random.Range(fireInterval.x, fireInterval.y) * (weapon == EnemyWeapon.Heavy ? heavyFireIntervalScale : 1f);

        /// <summary>Seconds before looking for a shot again, when there was no ricochet path to the player.</summary>
        protected virtual float RetryInterval => 0.25f;

        /// <summary>Sets the time until the next shot.</summary>
        protected void ScheduleShot(float seconds) => m_FireTimer = seconds;

        /// <summary>Fires the gun at the player along a ricochet path. Returns false if there was none.</summary>
        protected virtual bool TryFire(PlayerController player)
        {
            if (!TryPlanShot(player, m_GunPivot.position, weapon, out var plan))
                return false;
            Fire(plan, m_GunPivot, weapon);
            OnFired();
            return true;
        }

        protected virtual void FixedUpdate()
        {
            if (hopHeight <= 0f)
                return;

            float offset = 0f;
            if (m_HopAge >= 0f)
            {
                m_HopAge += Time.fixedDeltaTime;
                float launchSpeed = Mathf.Sqrt(2f * hopGravity * hopHeight);
                offset = launchSpeed * m_HopAge - 0.5f * hopGravity * m_HopAge * m_HopAge;
                if (offset <= 0f)
                {
                    offset = 0f;
                    m_HopAge = -1f;
                    m_HopTimer = Random.Range(hopInterval.x, hopInterval.y);
                }
            }
            else if ((m_HopTimer -= Time.fixedDeltaTime) <= 0f)
            {
                m_HopAge = 0f;
            }
            m_Rigidbody.MovePosition(m_RestPosition + m_HopDirection * offset);
        }

        /// <summary>Watches for lethal player shots about to hit, ducks at some of them, and stands back up afterwards.</summary>
        void UpdateDucking()
        {
            if (m_RolledShots.Count > 0)
                m_RolledShots.RemoveAll(s_IsGone);

            foreach (var shot in Projectile.Active)
            {
                if (shot.Team != Team.Player || shot.Bounces < ricochetsRequiredToKill || m_RolledShots.Contains(shot) || !IsAboutToHit(shot))
                    continue;
                // One roll per shot, so the chance doesn't compound frame after frame while it flies in.
                m_RolledShots.Add(shot);
                if (Random.value < duckChance)
                    m_DuckUntil = Time.time + duckDuration;
            }
            SetDucking(Time.time < m_DuckUntil);
        }

        /// <summary>
        /// True if <paramref name="shot"/> will reach the standing body within <see cref="duckWarningTime"/>. Ducking only
        /// makes it miss if it's coming in flat above the ducked body; steep floor and ceiling bounces still connect.
        /// Looks along the shot's current leg only; a bounce on the way changes the leg and gets looked at afresh.
        /// </summary>
        bool IsAboutToHit(Projectile shot)
        {
            if (!SweepBox(shot.Position, shot.Direction, shot.Speed * duckWarningTime, shot.Radius, transform.position, size, out float distance))
                return false;
            return !ShotPhysics.Cast(shot.Position, shot.Direction, distance, shot.Radius, Team.Player, true, out _); // unless a wall gets in the way first
        }

        /// <summary>
        /// Whether a circle of <paramref name="radius"/> moving from <paramref name="origin"/> along <paramref name="dir"/> touches an
        /// axis-aligned box within <paramref name="maxDistance"/>, and how far along it first does. The box is grown by the radius,
        /// which squares off its corners: near enough for deciding whether to duck.
        /// </summary>
        static bool SweepBox(Vector2 origin, Vector2 dir, float maxDistance, float radius, Vector2 center, Vector2 boxSize, out float distance)
        {
            distance = 0f;
            Vector2 half = boxSize * 0.5f + new Vector2(radius, radius);
            float enter = 0f, exit = maxDistance;
            for (int axis = 0; axis < 2; axis++)
            {
                float from = origin[axis] - center[axis], d = dir[axis];
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (Mathf.Abs(from) > half[axis])
                        return false; // moving parallel to these sides, outside them
                    continue;
                }
                float t1 = (-half[axis] - from) / d, t2 = (half[axis] - from) / d;
                enter = Mathf.Max(enter, Mathf.Min(t1, t2));
                exit = Mathf.Min(exit, Mathf.Max(t1, t2));
            }
            distance = enter;
            return enter <= exit;
        }

        /// <summary>Ducks or stands back up, shrinking the body towards the surface it's mounted on, as the player does.</summary>
        protected void SetDucking(bool duck)
        {
            if (duck == IsDucking)
                return;
            IsDucking = duck;

            // Shrinks towards the feet, i.e. the surface it's mounted on: the floor, or the ceiling for hangers.
            float height = duck ? size.y * (1f - duckHeightReduction) : size.y;
            float centerY = (height - size.y) * 0.5f;
            m_Collider.size = new Vector2(size.x, height);
            m_Collider.offset = new Vector2(0f, centerY);
            m_Body.transform.localPosition = new Vector2(0f, centerY);
            m_Body.transform.localScale = new Vector3(size.x, height, 1f);
            m_GunPivot.localPosition = new Vector2(0f, centerY + height * 0.2f);
            OnBodyResized(height, centerY);
        }

        /// <summary>Called when ducking changes the body's height; <paramref name="centerY"/> is its new local centre.</summary>
        protected virtual void OnBodyResized(float height, float centerY) { }

        bool IsOnScreen()
        {
            var cam = Camera.main;
            if (cam == null)
                return true;
            Vector3 viewport = cam.WorldToViewportPoint(transform.position);
            return viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        /// <summary>Plans a ricochet shot from <paramref name="origin"/> with <paramref name="gun"/> at where the player will be.</summary>
        protected bool TryPlanShot(PlayerController player, Vector2 origin, EnemyWeapon gun, out ShotPlan plan)
        {
            plan = default;
            var settings = ShotSettings(gun);

            float lead = prediction == Prediction.NextFrame
                ? Time.fixedDeltaTime
                : Vector2.Distance(origin, player.AimPoint) / settings.speed;

            if (BankShotSolver.FindBankShots(origin, PredictPlayer(player, lead), settings.radius, Team, s_Plans) == 0)
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
                    lead = Mathf.Max(0f, plan.pathLength - barrelLength) / settings.speed;
                    if (BankShotSolver.FindBankShots(origin, PredictPlayer(player, lead), settings.radius, Team, s_Plans) == 0)
                        break;
                    plan = ClosestTo(plan.direction);
                }
            }
            return true;
        }

        /// <summary>Extrapolates the player along their average velocity over the last few physics steps, stopping at the first wall in the way.</summary>
        Vector2 PredictPlayer(PlayerController player, float lead)
        {
            const float bodyRadius = 0.3f;
            Vector2 from = player.AimPoint;
            Vector2 delta = player.AverageVelocity(aimSmoothingSteps) * lead;
            float dist = delta.magnitude;
            if (dist < 1e-4f)
                return from;
            Vector2 predicted = ShotPhysics.Cast(from, delta / dist, dist, bodyRadius, Team.Player, true, out var hit) ? hit.centroid : from + delta;

            // The cast passes the safe zone door inwards, as shots do, but a player it has shut behind can't.
            var door = OneWayDoor.SafeZone;
            if (door != null && door.IsShut)
                predicted.x = Mathf.Max(predicted.x, door.OuterX + bodyRadius);
            return predicted;
        }

        static ShotPlan ClosestTo(Vector2 direction)
        {
            var best = s_Plans[0];
            foreach (var p in s_Plans)
                if (Vector2.Dot(p.direction, direction) > Vector2.Dot(best.direction, direction))
                    best = p;
            return best;
        }

        /// <summary>Turns <paramref name="pivot"/> along <paramref name="plan"/> and fires <paramref name="gun"/> from it.</summary>
        protected void Fire(ShotPlan plan, Transform pivot, EnemyWeapon gun)
        {
            pivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(plan.direction.y, plan.direction.x) * Mathf.Rad2Deg);
            // Mirrored when it points back over the body's shoulder, so the hopper stays on the body's top side.
            pivot.localScale = new Vector3(1f, Vector2.Dot(pivot.right, transform.right) < 0f ? -1f : 1f, 1f);
            m_AimHoldUntil = Time.time + 0.7f;
            var settings = ShotSettings(gun);
            var lib = Art.Lib;
            if (lib != null)
                Sfx.Play(gun == EnemyWeapon.Heavy ? lib.shotHeavy : lib.shotEnemy, gun == EnemyWeapon.Heavy ? 0.6f : 0.5f, 1f, 0.06f);

            if (gun == EnemyWeapon.Twin)
            {
                // Fanned either side of the planned path, so the pair straddles where the player is heading.
                float half = twinSpreadDegrees * 0.5f;
                Projectile.Spawn(Team.Enemy, plan.origin, ShotPhysics.Rotate(plan.direction, half), barrelLength, settings);
                Projectile.Spawn(Team.Enemy, plan.origin, ShotPhysics.Rotate(plan.direction, -half), barrelLength, settings);
            }
            else
            {
                Projectile.Spawn(Team.Enemy, plan.origin, plan.direction, barrelLength, settings);
            }

            if (ShowAimTraces)
            {
                var c = settings.color;
                c.a = 0.6f;
                AimTrace.Show(c, 0.6f, plan.origin, plan.bouncePoint, plan.target);
            }
        }

        /// <summary>Called after every shot.</summary>
        protected virtual void OnFired() { }

        /// <summary>(Re)builds the barrel art for the current weapon.</summary>
        protected void BuildBarrel() => BuildBarrel(m_GunPivot, weapon);

        /// <summary>(Re)builds the barrel art on <paramref name="pivot"/> for <paramref name="gun"/>: two thin barrels, one fat barrel, or a plain one.</summary>
        protected void BuildBarrel(Transform pivot, EnemyWeapon gun)
        {
            for (int i = pivot.childCount - 1; i >= 0; i--)
            {
                var old = pivot.GetChild(i);
                old.SetParent(null, false);
                Destroy(old.gameObject);
            }

            var sprite = GunSprite(gun);
            if (sprite != null)
            {
                // The marker sprite's pivot is its back end, on the barrel's axis.
                var go = new GameObject("Marker");
                go.transform.SetParent(pivot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                SortLayers.Set(sr, SortLayers.Characters, 1);
                return;
            }

            Vector2 center = new Vector2(barrelLength * 0.5f, 0f);
            switch (gun)
            {
                case EnemyWeapon.Twin:
                    Shapes.Box(pivot, "Barrel", BarrelColor, center + Vector2.up * 0.07f, new Vector2(barrelLength, 0.09f), 6);
                    Shapes.Box(pivot, "Barrel", BarrelColor, center - Vector2.up * 0.07f, new Vector2(barrelLength, 0.09f), 6);
                    break;
                case EnemyWeapon.Heavy:
                    Shapes.Box(pivot, "Barrel", BarrelColor, center, new Vector2(barrelLength, 0.3f), 6);
                    break;
                default:
                    Shapes.Box(pivot, "Barrel", BarrelColor, center, new Vector2(barrelLength, 0.16f), 6);
                    break;
            }
        }

        protected void Flash(Color color, float duration)
        {
            m_FlashColor = color;
            m_FlashTimer = duration;
        }

        public void OnShot(Projectile projectile)
        {
            if (projectile.Bounces < ricochetsRequiredToKill)
            {
                Flash(BlockedColor, 0.15f); // direct hit: harmless
                HarmlessHit(projectile.Position);
                return;
            }
            OnRicochetHit(projectile);
        }

        /// <summary>The splash and dull splat of a hit that doesn't eliminate (or, on the player, doesn't hurt).</summary>
        internal static void HarmlessHit(Vector2 at)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            SpriteFx.Play(lib.blockedSplat, at, 12);
            Sfx.Play(lib.splatHarmless, 0.8f);
        }

        /// <summary>A player shot that ricocheted enough times landed. Regular enemies die outright.</summary>
        protected virtual void OnRicochetHit(Projectile projectile)
        {
            PlayElimination(projectile.Position);
            Destroy(gameObject);
        }

        /// <summary>The elimination: a paint burst in the team colour, the hit sound, and the art left behind playing its death.</summary>
        protected void PlayElimination(Vector2 hitPoint)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            SpriteFx.Play(EliminationBurst(lib), Vector2.Lerp(transform.position, hitPoint, 0.5f), 14);
            Sfx.Play(lib.elimination, 1f, 1f, 0.03f);
            if (m_Art != null)
                SpriteFx.Corpse(CharacterArtFor(), transform, m_Art.transform.localPosition, m_Art.Renderer.flipX);
        }
    }
}
