using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// Final boss, fought in a closed arena. Cycles Standard → Twin → Heavy every few shots and wears armour on three regions:
    /// the head, the front (the side facing the player) and the back. A ricochet hit on an intact region only cracks its plate;
    /// a second ricochet on the same cracked region deals one damage and the plate re-seals. Cracks on different regions are
    /// tracked independently.
    ///
    /// It notices a lethal shot heading its way with a <see cref="dodgeChance"/>, rolled once per shot, and tries to dodge it:
    /// it ducks, jumps or side-steps, whichever keeps it clearest of every shot it has noticed. It only starts to move
    /// <see cref="reactionTime"/> after noticing a shot, so a shot that ricochets close to it leaves it too little warning.
    /// Once it's up on the ceiling it stays there, so from then on it only ducks or side-steps.
    ///
    /// At two thirds of its health it draws a second gun, which it keeps until it dies. At one third it leaps up to the ceiling
    /// and hangs from it for the rest of the fight. Each time, its armour turns black and nothing hurts it for
    /// <see cref="rageDuration"/> seconds, while it fires both guns <see cref="rageShotsPerSecond"/> times a second.
    /// </summary>
    public class Boss : Enemy
    {
        public enum Region { Head, Front, Back }

        [Header("Boss")]
        [Tooltip("Hit points. 6 splits the fight into three phases of 2 each: the second gun comes out at 4, the ceiling leap at 2.")]
        public int maxHealth = 6;
        [Tooltip("Shots fired before switching to the next weapon.")]
        public int shotsPerWeapon = 3;
        [Tooltip("Top fraction of the body that counts as the head.")]
        [Range(0.1f, 0.6f)] public float headFraction = 0.3f;

        [Header("Arena")]
        [Tooltip("The door into its arena. It holds fire until the player is on their way through.")]
        public OneWayDoor arenaDoor;
        [Tooltip("How far along the level its centre can move, from x to y.")]
        public Vector2 roamRange = new Vector2(44f, 55f);
        [Tooltip("Underside of the ceiling it hangs from in the last phase.")]
        public float ceilingY = LevelData.Height;

        [Header("Dodging")]
        [Tooltip("Chance it notices a lethal shot about to hit it and tries to dodge, rolled once per shot. A shot it misses goes unnoticed for the rest of its flight.")]
        [Range(0f, 1f)] public float dodgeChance = 0.33f;
        [Tooltip("Seconds before impact it notices a lethal shot heading its way.")]
        public float noticeTime = 0.5f;
        [Tooltip("Seconds from noticing a shot to starting to dodge it.")]
        public float reactionTime = 0.12f;
        [Tooltip("Side-step speed (m/s).")]
        public float dodgeSpeed = 7f;
        [Tooltip("Furthest a side-step takes it (m).")]
        public float dodgeDistance = 2.5f;
        [Tooltip("Height of a dodging jump (m). It only jumps from the floor: once it's on the ceiling it stays there.")]
        public float jumpHeight = 2f;
        [Tooltip("Speed (m/s) it walks back to the middle of its range at, so dodges don't leave it pinned at one end.")]
        public float walkSpeed = 2.5f;
        [Tooltip("Seconds after a dodge before it starts walking back to the middle.")]
        public float returnDelay = 1f;

        [Header("Phases")]
        [Tooltip("Seconds its armour stays black, and nothing hurts it, after each phase change.")]
        public float rageDuration = 4f;
        [Tooltip("Volleys from both guns per second while its armour is black.")]
        public float rageShotsPerSecond = 2f;
        [Tooltip("Seconds its leap up to the ceiling takes.")]
        public float ceilingLeapTime = 0.7f;

        public static Boss Instance { get; private set; }
        public int Health { get; private set; }
        public bool IsCracked(Region region) => m_Cracked[(int)region];
        /// <summary>True while its armour is black after a phase change: no shot hurts it or cracks it.</summary>
        public bool IsInvulnerable => Time.time < m_InvulnerableUntil;
        public bool HasSecondGun => m_SecondGun != null;
        /// <summary>True once it has started its leap to the ceiling.</summary>
        public bool IsOnCeiling => mount == EnemyMount.Ceiling || IsLeaping;

        static readonly EnemyWeapon[] WeaponCycle = { EnemyWeapon.Standard, EnemyWeapon.Twin, EnemyWeapon.Heavy };
        static readonly Color PlateColor = new Color(0.62f, 0.66f, 0.74f);
        static readonly Color CrackedColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color BlackArmourColor = new Color(0.05f, 0.05f, 0.07f);
        static readonly Color HurtColor = Color.white;
        const float PlateInset = 0.1f;
        /// <summary>Distance a shot travels between the points where a dodge is checked against it.</summary>
        const float SampleSpacing = 0.08f;
        /// <summary>Legs of each shot's flight it looks ahead along: the one it's on and the ones after its next bounces.</summary>
        const int LegsAhead = 3;
        /// <summary>
        /// Seconds past <see cref="noticeTime"/> that a dodge is checked against the shots, so one that only puts off the hit
        /// (ducking under a shot that's coming down steeply, say) doesn't count as clearing it.
        /// </summary>
        const float DodgeLookAhead = 0.6f;

        /// <summary>One straight stretch of a shot's flight, from <see cref="from"/> to <see cref="to"/> seconds from now.</summary>
        struct Leg
        {
            public Vector2 start;
            public Vector2 direction;
            public float speed;
            public float radius;
            public float from;
            public float to;
        }

        struct Dodge
        {
            public float targetX;
            public bool duck;
            public bool jump;
        }

        readonly bool[] m_Cracked = new bool[3];
        readonly SpriteRenderer[] m_Plates = new SpriteRenderer[3];
        /// <summary>With the art in: crack overlays shown over cracked regions (the plates above stay as hidden region frames).</summary>
        readonly SpriteRenderer[] m_Cracks = new SpriteRenderer[3];
        /// <summary>The legs ahead of every lethal shot in flight that it hasn't overlooked.</summary>
        readonly List<Leg> m_Legs = new List<Leg>();
        /// <summary>Lethal shots looked at so far, with their bounce count then: a new bounce is a new leg to look at.</summary>
        readonly Dictionary<Projectile, int> m_SeenLegs = new Dictionary<Projectile, int>();
        /// <summary>Shots that have threatened it, and whether it noticed them (<see cref="dodgeChance"/>, rolled once per shot).</summary>
        readonly Dictionary<Projectile, bool> m_Noticed = new Dictionary<Projectile, bool>();
        static readonly List<Projectile> s_Gone = new List<Projectile>();
        static readonly List<Dodge> s_Options = new List<Dodge>(5);
        static readonly List<Dodge> s_Clear = new List<Dodge>(5);

        Transform m_SecondGun;
        int m_WeaponIndex;
        int m_ShotsWithWeapon;
        float m_Facing = -1f; // +1 when the player is on its own right (its local +x), -1 on its left
        float m_InvulnerableUntil = float.NegativeInfinity;

        // Movement along the surface it's mounted on: the floor, or the ceiling once it has leapt up.
        float m_SurfaceY;
        float m_X;
        float m_TargetX;
        float m_MoveSpeed;
        float m_JumpAge = -1f;  // < 0 while not jumping
        float m_LeapAge = -1f;  // < 0 while not leaping to the ceiling
        float m_LeapFromY;
        float m_DodgeDuckUntil;
        Dodge m_PendingDodge;
        float m_DodgeAt = float.PositiveInfinity;
        float m_LastDodgeTime = float.NegativeInfinity;

        bool IsLeaping => m_LeapAge >= 0f;
        bool IsJumping => m_JumpAge >= 0f;
        EnemyWeapon SecondWeapon => WeaponCycle[(m_WeaponIndex + 1) % WeaponCycle.Length];
        float LaunchSpeed => Mathf.Sqrt(2f * hopGravity * jumpHeight);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBossStatics() => Instance = null;

        protected override void Awake()
        {
            weapon = WeaponCycle[0];
            base.Awake();
            Instance = this;
            Health = maxHealth;
            // Placed standing on the floor (see PlaceOn).
            m_X = m_TargetX = transform.position.x;
            m_SurfaceY = transform.position.y - size.y * 0.5f;

            // The plates are children of the body, laid out in its unit square, so they squash with it when it ducks.
            // They sit just inside it, so its weapon colour still shows as an outline.
            m_Body.sortingOrder = 4;
            var body = m_Body.transform;
            float insetX = PlateInset / size.x, insetY = PlateInset / size.y;
            m_Plates[(int)Region.Head] = Shapes.Box(body, "Head Plate", PlateColor,
                new Vector2(0f, 0.5f - headFraction * 0.5f), new Vector2(1f - insetX * 2f, headFraction - insetY * 2f), 5);
            var torsoPlate = new Vector2(0.5f - insetX * 1.5f, 1f - headFraction - insetY * 2f);
            m_Plates[(int)Region.Front] = Shapes.Box(body, "Front Plate", PlateColor, Vector2.zero, torsoPlate, 5);
            m_Plates[(int)Region.Back] = Shapes.Box(body, "Back Plate", PlateColor, Vector2.zero, torsoPlate, 5);

            var lib = Art.Lib;
            if (m_Art != null && lib != null && lib.bossCrackHead != null && lib.bossCrackTorso != null)
            {
                for (int i = 0; i < m_Plates.Length; i++)
                {
                    m_Plates[i].enabled = false;
                    var go = new GameObject(m_Plates[i].name + " Crack");
                    go.transform.SetParent(transform, false);
                    m_Cracks[i] = go.AddComponent<SpriteRenderer>();
                    m_Cracks[i].sprite = i == (int)Region.Head ? lib.bossCrackHead : lib.bossCrackTorso;
                    SortLayers.Set(m_Cracks[i], SortLayers.Characters, 2);
                    m_Cracks[i].enabled = false;
                }
                m_Art.footstepPitch = 0.65f;
                m_Art.footstepVolume = 0.5f;
            }
        }

        protected override CharacterArt CharacterArtFor() => Art.Lib != null ? Art.Lib.boss : null;

        protected override Sprite GunSprite(EnemyWeapon gun)
        {
            var lib = Art.Lib;
            if (lib == null)
                return null;
            switch (gun)
            {
                case EnemyWeapon.Twin: return lib.twinBoss;
                case EnemyWeapon.Heavy: return lib.heavyBoss;
                default: return lib.standardBoss;
            }
        }

        protected override Flipbook EliminationBurst(ArtLibrary lib) => lib.burstPaint;

        /// <summary>Both guns in his hands: the second one, from 2/3 health, held just below the first.</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (m_SecondGun != null && m_Art != null && m_Art.TryHandsLocal(out var hands))
                m_SecondGun.localPosition = hands + Vector2.down * SecondGunDrop;
        }

        /// <summary>How far below the first gun the second sits in his hands (m).</summary>
        const float SecondGunDrop = 0.17f;

        /// <summary>Its armour turns black while nothing can hurt it.</summary>
        protected override Color ArtTint => IsInvulnerable ? new Color(0.2f, 0.2f, 0.24f) : Color.white;

        protected override bool FacesRight() => m_Facing >= 0f;

        /// <summary>Walking back to the middle, dashing aside, jumping, ducking or leaping up to the ceiling.</summary>
        protected override void UpdateArt()
        {
            base.UpdateArt();
            if (m_SecondGun != null && Time.time >= m_AimHoldUntil)
                RestGun(m_SecondGun, FacesRight());
            if (m_Art == null)
                return;
            // From where he's heading rather than how far he moved this frame: frames without a physics step move him 0, and
            // flickering to Idle restarted the walk clip on its footstep frame over and over.
            float speed = Mathf.Abs(m_TargetX - m_X) > 0.02f ? m_MoveSpeed : 0f;
            var a = m_Art.Animator;
            a.SetFloat(CharacterVisual.Speed, speed);
            a.SetBool(CharacterVisual.Sprint, m_MoveSpeed > walkSpeed + 0.01f);
            a.SetBool(CharacterVisual.Grounded, !IsJumping && !IsLeaping);
            a.SetFloat(CharacterVisual.VelocityY, IsLeaping ? 1f : IsJumping ? LaunchSpeed - hopGravity * m_JumpAge : 0f);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        protected override void Update()
        {
            base.Update();

            var player = PlayerController.Instance;
            if (player != null)
                m_Facing = transform.InverseTransformPoint(player.transform.position).x >= 0f ? 1f : -1f;

            UpdateDodging();
            UpdatePlates();
        }

        void UpdatePlates()
        {
            float torsoY = -headFraction * 0.5f;
            float halfOffset = 0.25f - PlateInset * 0.25f / size.x;
            m_Plates[(int)Region.Front].transform.localPosition = new Vector2(halfOffset * m_Facing, torsoY);
            m_Plates[(int)Region.Back].transform.localPosition = new Vector2(-halfOffset * m_Facing, torsoY);

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            for (int i = 0; i < m_Plates.Length; i++)
                m_Plates[i].color = IsInvulnerable ? BlackArmourColor : m_Cracked[i] ? Color.Lerp(CrackedColor, PlateColor, pulse * 0.5f) : PlateColor;

            // Crack overlays follow the (hidden) plates: same place, squashed with them while it ducks.
            float squash = IsDucking ? 1f - duckHeightReduction : 1f;
            for (int i = 0; i < m_Cracks.Length; i++)
            {
                var crack = m_Cracks[i];
                if (crack == null)
                    continue;
                crack.enabled = m_Cracked[i] && !IsInvulnerable;
                if (!crack.enabled)
                    continue;
                crack.transform.SetPositionAndRotation(m_Plates[i].transform.position, transform.rotation);
                crack.transform.localScale = new Vector3(1f, squash, 1f);
                crack.flipX = i == (int)Region.Back;
                crack.color = new Color(1f, 1f, 1f, 0.65f + 0.35f * pulse);
            }
        }

        /// <summary>Which region a point on the body is in, in the body's own frame, so it holds when it's ducking or upside down.</summary>
        Region RegionOf(Vector2 point)
        {
            Vector2 local = m_Body.transform.InverseTransformPoint(point);
            if (local.y >= 0.5f - headFraction)
                return Region.Head;
            return local.x * m_Facing >= 0f ? Region.Front : Region.Back;
        }

        protected override void OnRicochetHit(Projectile projectile)
        {
            if (IsInvulnerable)
            {
                Flash(BlockedColor, 0.15f);
                HarmlessHit(projectile.Position);
                return;
            }

            int region = (int)RegionOf(projectile.Position);
            if (!m_Cracked[region])
            {
                m_Cracked[region] = true; // first hit only cracks the plate
                Flash(BlockedColor, 0.15f);
                HarmlessHit(projectile.Position);
                return;
            }

            m_Cracked[region] = false; // second hit on the same region gets through; the plate re-seals
            Health--;
            Flash(HurtColor, 0.3f);
            var lib = Art.Lib;
            if (Health <= 0)
            {
                // A bigger send-off than a guard's: the burst goes off three times around the body.
                PlayElimination(projectile.Position);
                if (lib != null)
                {
                    SpriteFx.Play(lib.burstPaint, (Vector2)transform.position + Vector2.up * 0.7f, 14, true);
                    SpriteFx.Play(lib.burstPaint, (Vector2)transform.position - Vector2.up * 0.6f, 14);
                }
                Destroy(gameObject);
                return;
            }
            if (lib != null)
            {
                SpriteFx.Play(lib.burstPaint, projectile.Position, 14);
                Sfx.Play(lib.elimination, 0.8f, 0.85f, 0f);
            }

            // Compared as whole numbers: at 2/3 and 1/3 of its health, rounded down to whole hit points.
            bool phaseChange = false;
            if (!HasSecondGun && Health * 3 <= maxHealth * 2)
            {
                DrawSecondGun();
                phaseChange = true;
            }
            if (!IsOnCeiling && Health * 3 <= maxHealth)
            {
                StartLeap();
                phaseChange = true;
            }
            if (phaseChange)
            {
                m_InvulnerableUntil = Time.time + rageDuration;
                ScheduleShot(0.15f);
            }
        }

        // ---- Guns ----

        void DrawSecondGun()
        {
            m_SecondGun = new GameObject("Second Gun Pivot").transform;
            m_SecondGun.SetParent(transform, false);
            float height = IsDucking ? size.y * (1f - duckHeightReduction) : size.y;
            PlaceSecondGun(height, (height - size.y) * 0.5f);
            BuildBarrel(m_SecondGun, SecondWeapon);
        }

        /// <summary>The second gun is held at the hip, below the first.</summary>
        void PlaceSecondGun(float height, float centerY) => m_SecondGun.localPosition = new Vector2(0f, centerY - height * 0.15f);

        protected override void OnBodyResized(float height, float centerY)
        {
            if (m_SecondGun != null)
                PlaceSecondGun(height, centerY);
        }

        /// <summary>Holds fire until the player is on their way into the arena, and while it leaps to the ceiling.</summary>
        protected override bool CanFire(PlayerController player) =>
            base.CanFire(player) && (arenaDoor == null || arenaDoor.PlayerIsLeaving) && !IsLeaping;

        protected override float NextFireInterval() => IsInvulnerable ? 1f / rageShotsPerSecond : base.NextFireInterval();

        protected override float RetryInterval => IsInvulnerable ? 0.1f : base.RetryInterval;

        /// <summary>Each gun looks for its own ricochet path to the player; the volley counts if either finds one.</summary>
        protected override bool TryFire(PlayerController player)
        {
            bool fired = false;
            if (TryPlanShot(player, GunPivot.position, weapon, out var plan))
            {
                Fire(plan, GunPivot, weapon);
                fired = true;
            }
            if (HasSecondGun && TryPlanShot(player, m_SecondGun.position, SecondWeapon, out plan))
            {
                Fire(plan, m_SecondGun, SecondWeapon);
                fired = true;
            }
            if (fired)
                OnFired();
            return fired;
        }

        /// <summary>Moves on to the next weapon every few volleys. The second gun always holds the one after the first's.</summary>
        protected override void OnFired()
        {
            if (++m_ShotsWithWeapon < shotsPerWeapon)
                return;
            m_ShotsWithWeapon = 0;
            m_WeaponIndex = (m_WeaponIndex + 1) % WeaponCycle.Length;
            weapon = WeaponCycle[m_WeaponIndex];
            BuildBarrel();
            if (HasSecondGun)
                BuildBarrel(m_SecondGun, SecondWeapon);
        }

        // ---- Movement ----

        protected override void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (IsLeaping)
            {
                Leap(dt);
                return;
            }

            m_X = Mathf.MoveTowards(m_X, m_TargetX, m_MoveSpeed * dt);
            float lift = 0f;
            if (IsJumping)
            {
                m_JumpAge += dt;
                lift = JumpLift(m_JumpAge);
                if (lift <= 0f)
                {
                    lift = 0f;
                    m_JumpAge = -1f;
                }
            }
            KinematicBody.MovePosition(new Vector2(m_X, CenterY(lift)));
        }

        /// <summary>How far its feet are off the surface it's mounted on, <paramref name="age"/> seconds into a jump.</summary>
        float JumpLift(float age) => LaunchSpeed * age - 0.5f * hopGravity * age * age;

        /// <summary>Where its centre is with its feet <paramref name="lift"/> off the surface it's mounted on.</summary>
        float CenterY(float lift) => mount == EnemyMount.Ceiling ? m_SurfaceY - lift - size.y * 0.5f : m_SurfaceY + lift + size.y * 0.5f;

        void StartLeap()
        {
            m_LeapAge = 0f;
            m_LeapFromY = transform.position.y;
            m_JumpAge = -1f;
            m_DodgeDuckUntil = 0f;
            m_DodgeAt = float.PositiveInfinity;
            m_TargetX = m_X;
            SetDucking(false);
        }

        /// <summary>Rises from the floor to the ceiling, turning over on the way, and hangs from it from then on.</summary>
        void Leap(float dt)
        {
            m_LeapAge += dt;
            float t = Mathf.Clamp01(m_LeapAge / ceilingLeapTime);
            float eased = t * t * (3f - 2f * t);
            KinematicBody.MovePosition(new Vector2(m_X, Mathf.Lerp(m_LeapFromY, ceilingY - size.y * 0.5f, eased)));
            KinematicBody.MoveRotation(180f * eased);
            if (t < 1f)
                return;
            mount = EnemyMount.Ceiling;
            m_SurfaceY = ceilingY;
            m_LeapAge = -1f;
        }

        // ---- Dodging ----

        /// <summary>
        /// Starts any dodge that's due, then looks at every lethal shot in flight, following it through its next bounces for
        /// <see cref="noticeTime"/>. Whenever one it hasn't looked at yet (or one that has bounced since) would hit it if it
        /// carried on as it is, and it notices that shot, it picks a dodge, to start after its reaction time.
        /// </summary>
        void UpdateDodging()
        {
            if (IsLeaping || IsInvulnerable)
            {
                m_DodgeAt = float.PositiveInfinity;
                SetDucking(false);
                return;
            }

            if (Time.time >= m_DodgeAt)
            {
                Begin(m_PendingDodge);
                m_DodgeAt = float.PositiveInfinity;
            }
            else if (m_DodgeAt == float.PositiveInfinity && Time.time >= m_LastDodgeTime + returnDelay)
            {
                // Settled after a dodge: head back to the middle of its range, leaving room to dodge either way.
                m_TargetX = (roamRange.x + roamRange.y) * 0.5f;
                m_MoveSpeed = walkSpeed;
            }
            SetDucking(Time.time < m_DodgeDuckUntil && !IsJumping);

            s_Gone.Clear();
            foreach (var shot in m_SeenLegs.Keys)
                if (shot == null)
                    s_Gone.Add(shot);
            foreach (var shot in m_Noticed.Keys)
                if (shot == null)
                    s_Gone.Add(shot);
            foreach (var shot in s_Gone)
            {
                m_SeenLegs.Remove(shot);
                m_Noticed.Remove(shot);
            }

            m_Legs.Clear();
            bool newThreat = false;
            foreach (var shot in Projectile.Active)
            {
                if (shot.Team != Team.Player || shot.Bounces < ricochetsRequiredToKill)
                    continue;
                if (m_Noticed.TryGetValue(shot, out bool noticed) && !noticed)
                    continue; // overlooked for the rest of its flight
                int first = m_Legs.Count;
                AddLegs(shot);
                if (m_SeenLegs.TryGetValue(shot, out int bounces) && bounces == shot.Bounces)
                    continue;
                if (Clearance(CarryOn(), float.PositiveInfinity, first, m_Legs.Count, noticeTime) > 0f)
                    continue; // doesn't hit it within the time it notices shots in, for now
                if (!m_Noticed.ContainsKey(shot))
                {
                    // The first time a shot is about to hit, roll whether it notices it at all.
                    noticed = Random.value < dodgeChance;
                    m_Noticed[shot] = noticed;
                    if (!noticed)
                    {
                        m_Legs.RemoveRange(first, m_Legs.Count - first);
                        continue;
                    }
                }
                m_SeenLegs[shot] = shot.Bounces;
                newThreat = true;
            }
            if (newThreat)
                Decide();
        }

        /// <summary>
        /// Follows <paramref name="shot"/> for <see cref="noticeTime"/> plus <see cref="DodgeLookAhead"/>, bouncing it off walls
        /// as a shot does (as a perfect mirror, which a player shot is), and adds each leg of the way to <see cref="m_Legs"/>.
        /// </summary>
        void AddLegs(Projectile shot)
        {
            Vector2 position = shot.Position, direction = shot.Direction;
            float time = 0f, horizon = noticeTime + DodgeLookAhead;
            for (int i = 0; i < LegsAhead && time < horizon; i++)
            {
                float reach = shot.Speed * (horizon - time);
                bool bounces = ShotPhysics.Cast(position, direction, reach, shot.Radius, Team.Player, true, out var wall);
                float duration = (bounces ? wall.distance : reach) / shot.Speed;
                m_Legs.Add(new Leg { start = position, direction = direction, speed = shot.Speed, radius = shot.Radius, from = time, to = time + duration });
                if (!bounces)
                    return;
                time += duration;
                position = wall.centroid + wall.normal * ShotPhysics.Skin;
                direction = ShotPhysics.Reflect(direction, wall.normal);
            }
        }

        /// <summary>Carrying on as it is: no new move.</summary>
        Dodge CarryOn() => new Dodge { targetX = m_TargetX };

        /// <summary>
        /// Weighs ducking, a side-step either way and (on the floor) a jump against every shot it has noticed. Picks at random
        /// among those that clear them all; if none does, the one that comes closest. Due to start after <see cref="reactionTime"/>.
        /// </summary>
        void Decide()
        {
            float at = reactionTime;
            float startX = Mathf.MoveTowards(m_X, m_TargetX, m_MoveSpeed * at);
            s_Options.Clear();
            s_Options.Add(new Dodge { targetX = Mathf.Clamp(startX - dodgeDistance, roamRange.x, roamRange.y) });
            s_Options.Add(new Dodge { targetX = Mathf.Clamp(startX + dodgeDistance, roamRange.x, roamRange.y) });
            if (!IsJumping)
            {
                s_Options.Add(new Dodge { targetX = startX, duck = true });
                // Up on the ceiling it stays there, so jumping (dropping off it) is only for the floor.
                if (mount == EnemyMount.Floor)
                    s_Options.Add(new Dodge { targetX = startX, jump = true });
            }

            s_Clear.Clear();
            Dodge closest = s_Options[0];
            float closestClearance = float.NegativeInfinity;
            foreach (var option in s_Options)
            {
                float clearance = Clearance(option, at, 0, m_Legs.Count, float.PositiveInfinity);
                if (clearance > 0f)
                    s_Clear.Add(option);
                if (clearance > closestClearance)
                {
                    closestClearance = clearance;
                    closest = option;
                }
            }
            m_PendingDodge = s_Clear.Count > 0 ? s_Clear[Random.Range(0, s_Clear.Count)] : closest;
            m_DodgeAt = Time.time + at;
        }

        void Begin(Dodge dodge)
        {
            m_LastDodgeTime = Time.time;
            m_TargetX = dodge.targetX;
            m_MoveSpeed = dodgeSpeed;
            if (dodge.duck)
                m_DodgeDuckUntil = Time.time + duckDuration;
            if (dodge.jump && !IsJumping)
            {
                m_JumpAge = 0f;
                m_DodgeDuckUntil = 0f;
            }
        }

        /// <summary>
        /// How close the legs from <paramref name="firstLeg"/> up to <paramref name="endLeg"/> come to the body within
        /// <paramref name="within"/> seconds (minus the shot's radius; negative means a hit), if it carries on as it is until
        /// <paramref name="at"/> seconds from now and then makes <paramref name="dodge"/>.
        /// </summary>
        float Clearance(Dodge dodge, float at, int firstLeg, int endLeg, float within)
        {
            float closest = float.PositiveInfinity;
            for (int i = firstLeg; i < endLeg; i++)
            {
                var leg = m_Legs[i];
                float step = SampleSpacing / leg.speed, end = Mathf.Min(leg.to, within);
                for (float t = leg.from; t <= end; t += step)
                {
                    Vector2 shot = leg.start + leg.direction * (leg.speed * (t - leg.from));
                    Rect body = BodyAt(t, dodge, at);
                    float dx = Mathf.Max(Mathf.Abs(shot.x - body.center.x) - body.width * 0.5f, 0f);
                    float dy = Mathf.Max(Mathf.Abs(shot.y - body.center.y) - body.height * 0.5f, 0f);
                    closest = Mathf.Min(closest, Mathf.Sqrt(dx * dx + dy * dy) - leg.radius);
                }
            }
            return closest;
        }

        /// <summary>
        /// Its body <paramref name="ahead"/> seconds from now, if it carries on as it is until <paramref name="at"/> seconds
        /// from now and then makes <paramref name="dodge"/>. A jump under way runs its course; ducking only happens on the surface.
        /// </summary>
        Rect BodyAt(float ahead, Dodge dodge, float at)
        {
            bool started = ahead > at;
            float x = Mathf.MoveTowards(m_X, m_TargetX, m_MoveSpeed * Mathf.Min(ahead, at));
            if (started)
                x = Mathf.MoveTowards(x, dodge.targetX, dodgeSpeed * (ahead - at));

            float lift = IsJumping ? JumpLift(m_JumpAge + ahead) : dodge.jump && started ? JumpLift(ahead - at) : 0f;
            bool jumping = IsJumping || (dodge.jump && started);
            lift = Mathf.Max(0f, lift);
            bool ducked = !jumping && ((dodge.duck && started && ahead < at + duckDuration) || Time.time + ahead < m_DodgeDuckUntil);

            float height = ducked ? size.y * (1f - duckHeightReduction) : size.y;
            float bottom = mount == EnemyMount.Ceiling ? m_SurfaceY - lift - height : m_SurfaceY + lift;
            return new Rect(x - size.x * 0.5f, bottom, size.x, height);
        }
    }
}
