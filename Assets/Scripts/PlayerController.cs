using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RicochetPoC
{
    public enum PlayerGun
    {
        [Tooltip("One shot per round.")]
        Pistol,
        [Tooltip("The Twin enemies' gun: two shots fanned apart, one round each.")]
        Twin,
    }

    /// <summary>
    /// A/D move, Shift sprint, W jump, S duck (height and hitbox shrink), mouse aims the gun, left click fires.
    /// Emptying the magazine starts an automatic reload; a laser sight previews each shot's first ricochet.
    /// Once the player has the twin gun, F switches between it and the pistol. Both guns feed from the same magazine.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour, IShootable
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        public float moveSpeed = 7f;
        [Tooltip("Holding Shift multiplies the move speed by this. Ducking cancels it.")]
        [Min(1f)] public float sprintMultiplier = 1.5f;
        public float duckMoveMultiplier = 0.5f;
        public float jumpSpeed = 15f;
        public float gravityScale = 3f;

        [Header("Body")]
        public Vector2 standingSize = new Vector2(0.8f, 1.6f);
        [Range(0f, 0.9f)] public float duckHeightReduction = 0.4f;

        [Header("Gun")]
        public float barrelLength = 0.85f;
        public float fireCooldown = 0.15f;
        [Min(1)] public int magazineSize = 6;
        [Tooltip("Seconds to refill the magazine once it runs dry.")]
        [Min(0f)] public float reloadTime = 2f;
        public ProjectileSettings shot = new ProjectileSettings();

        [Header("Twin Gun")]
        [Tooltip("Lets F switch to the twin gun. It's picked up in level 6's safe zone, and every later level starts with it.")]
        public bool hasTwinGun;
        [Tooltip("Angle between the twin gun's two shots. Same as the Twin enemies' gun.")]
        public float twinSpreadDegrees = 4f;
        [Tooltip("Minimum seconds between twin gun shots.")]
        [Min(0f)] public float twinFireCooldown = 0.4f;
        [Tooltip("Seconds a switch between guns takes. Neither gun fires meanwhile.")]
        [Min(0f)] public float gunSwitchTime = 0.5f;
        [Tooltip("Seconds after a switch finishes before F can switch again.")]
        [Min(0f)] public float gunSwitchCooldown = 1f;

        [Header("Health")]
        public int maxHealth = 3;
        public float invulnerableTime = 0.75f;

        public Team Team => Team.Player;
        public int Health { get; private set; }
        public bool IsDead => Health <= 0;
        public bool IsDucking { get; private set; }
        /// <summary>Rounds left in the magazine. The gun reloads automatically when this hits 0.</summary>
        public int Ammo { get; private set; }
        public bool IsReloading => Ammo <= 0;
        /// <summary>0 when a reload starts, 1 once the magazine is full again.</summary>
        public float ReloadProgress => !IsReloading ? 1f : reloadTime > 0f ? Mathf.Clamp01((Time.time - m_ReloadStartTime) / reloadTime) : 0f;
        /// <summary>Centre of the current hitbox — what enemies aim at.</summary>
        public Vector2 AimPoint => m_Collider.bounds.center;
        public Bounds HitBox => m_Collider.bounds;
        /// <summary>The gun in hand. Mid-switch, the one being put away.</summary>
        public PlayerGun Gun { get; private set; }
        /// <summary>True while one gun is put away and the other brought out. Neither fires meanwhile.</summary>
        public bool IsSwitchingGun { get; private set; }
        /// <summary>The gun being switched to while <see cref="IsSwitchingGun"/>; otherwise the gun in hand.</summary>
        public PlayerGun NextGun => IsSwitchingGun ? Other(Gun) : Gun;
        /// <summary>0 when a switch starts, 1 once the new gun is ready.</summary>
        public float SwitchProgress => !IsSwitchingGun ? 1f : gunSwitchTime > 0f ? Mathf.Clamp01((Time.time - m_SwitchStartTime) / gunSwitchTime) : 1f;
        /// <summary>Whether pressing F would switch guns right now.</summary>
        public bool CanSwitchGun => hasTwinGun && !IsDead && !IsSwitchingGun && Time.time >= m_NextSwitchTime;

        static readonly Color BodyColor = new Color(0.25f, 0.6f, 1f);
        static readonly Color BarrelColor = new Color(0.15f, 0.15f, 0.18f);
        const float JumpBufferTime = 0.12f;
        /// <summary>Physics steps of velocity kept for <see cref="AverageVelocity"/>.</summary>
        const int VelocityHistoryLength = 30;

        static ContactFilter2D s_SolidFilter = CreateSolidFilter();
        readonly List<Collider2D> m_Overlaps = new List<Collider2D>(8);
        readonly Vector2[] m_VelocityHistory = new Vector2[VelocityHistoryLength];
        int m_VelocityHistoryCount;
        int m_VelocityHistoryNext;

        Rigidbody2D m_Body;
        BoxCollider2D m_Collider;
        Transform m_Visual;
        SpriteRenderer m_BodySprite;
        /// <summary>The character art and its Animator; null without the art library, when the placeholder box shows instead.</summary>
        CharacterVisual m_Art;
        /// <summary>What blinks while invulnerable: the art, or the placeholder box.</summary>
        SpriteRenderer BodyRenderer => m_Art != null ? m_Art.Renderer : m_BodySprite;
        bool m_WasGrounded = true;
        float m_AirborneSince;
        float m_LandingUntil;
        /// <summary>The marker sprite in the hands, and the sprite it shows when not reloading.</summary>
        SpriteRenderer m_Marker;
        Sprite m_MarkerSprite;

        /// <summary>Most the muzzle tips up during a reload, in degrees.</summary>
        const float ReloadTilt = 28f;
        Transform m_GunPivot;
        /// <summary>Holds the barrel art; squashed into the pivot and back out again during a gun switch.</summary>
        Transform m_GunArt;
        PlayerGun? m_GunArtShown;
        LaserSight m_Laser;
        /// <summary>The twin gun's second line, for its bottom barrel.</summary>
        LaserSight m_SecondLaser;
        Camera m_Camera;

        float m_MoveInput;
        bool m_SprintInput;
        float m_JumpBufferedUntil = float.NegativeInfinity;
        float m_NextFireTime;
        float m_ReloadStartTime;
        float m_SwitchStartTime;
        float m_NextSwitchTime;
        float m_InvulnerableUntil;
        Vector2 m_AimDirection = Vector2.right;
        /// <summary>Which way the body (and the gun with it) faces: the side of the body's centre the mouse is on.</summary>
        bool m_FacingRight = true;
        /// <summary>How far past the body's centre (in metres) the mouse has to go to turn the character around.</summary>
        const float FacingDeadZone = 0.05f;
        /// <summary>The mouse has to be at least this far (in metres) from the gun to aim it; closer, the last aim holds.</summary>
        const float MinAimDistance = 0.15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        static ContactFilter2D CreateSolidFilter()
        {
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            return filter;
        }

        void Awake()
        {
            Instance = this;
            Health = maxHealth;
            Ammo = magazineSize;

            m_Body = GetComponent<Rigidbody2D>();
            m_Body.gravityScale = gravityScale;
            m_Body.freezeRotation = true;
            m_Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            m_Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            m_Collider = GetComponent<BoxCollider2D>();
            m_Collider.sharedMaterial = new PhysicsMaterial2D("Player (no friction)") { friction = 0f, bounciness = 0f };

            m_BodySprite = Shapes.Box(transform, "Body", BodyColor, Vector2.zero, standingSize, 5);
            m_Visual = m_BodySprite.transform;
            // The art stands on the feet and never scales; the box above stays as the hitbox frame, hidden.
            m_Art = CharacterVisual.Create(transform, Art.Lib != null ? Art.Lib.player : null, -standingSize.y * 0.5f, 0);
            if (m_Art != null)
                m_BodySprite.enabled = false;
            m_GunPivot = new GameObject("Gun Pivot").transform;
            m_GunPivot.SetParent(transform, false);
            m_GunArt = new GameObject("Gun").transform;
            m_GunArt.SetParent(m_GunPivot, false);
            ShowGunArt(Gun, 1f);
            m_Laser = CreateLaser("Laser Sight");
            m_SecondLaser = CreateLaser("Laser Sight (bottom barrel)");

            ApplyBodyShape();
        }

        LaserSight CreateLaser(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<LaserSight>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            // Frozen under the pause or victory screen: clicking its button mustn't fire a shot.
            if (IsDead || GameBootstrap.IsPaused)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            m_MoveInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            m_SprintInput = keyboard.shiftKey.isPressed;
            if (keyboard.wKey.wasPressedThisFrame)
                m_JumpBufferedUntil = Time.time + JumpBufferTime;

            SetDucking(keyboard.sKey.isPressed);
            Aim(mouse.position.ReadValue());

            if (keyboard.fKey.wasPressedThisFrame && CanSwitchGun)
            {
                IsSwitchingGun = true;
                m_SwitchStartTime = Time.time;
                Sfx.Play(Art.Lib != null ? Art.Lib.gunSwitch : null, 0.6f);
            }
            UpdateGunSwitch();

            if (IsReloading && Time.time >= m_ReloadStartTime + reloadTime)
            {
                Ammo = magazineSize;
                Sfx.Play(Art.Lib != null ? Art.Lib.reload : null, 0.6f);
            }

            if (mouse.leftButton.wasPressedThisFrame && !IsReloading && !IsSwitchingGun && Time.time >= m_NextFireTime)
                Fire();
            UpdateLaser();
            UpdateArt();
            UpdateReloadArt();

            // Blink while invulnerable.
            BodyRenderer.enabled = Time.time >= m_InvulnerableUntil || Mathf.Repeat(Time.time, 0.12f) < 0.06f;
        }

        /// <summary>
        /// The gun sits in the hands: after the Animator has picked this frame's sprite, the gun pivot (the grip, and the
        /// point shots and the laser start from) moves to where that frame's hands are, crouching and jumping included.
        /// </summary>
        void LateUpdate()
        {
            if (m_Art != null && !IsDead && m_Art.TryHandsLocal(out var hands))
                m_GunPivot.localPosition = hands;
        }

        /// <summary>
        /// While the magazine refills, the gun tips up and plays the reload: the front hand fetches a paint pod and pours it
        /// into the hopper, timed to the reload bar.
        /// </summary>
        void UpdateReloadArt()
        {
            var lib = Art.Lib;
            if (m_Art == null || lib == null)
                return;
            if (m_Marker == null)
            {
                m_Marker = m_GunArt.GetComponentInChildren<SpriteRenderer>();
                m_MarkerSprite = m_Marker != null ? m_Marker.sprite : null;
                if (m_Marker == null)
                    return;
            }
            var book = Gun == PlayerGun.Twin ? lib.reloadTwin : lib.reloadPistol;
            if (IsReloading && !IsSwitchingGun && book.IsValid)
            {
                float t = ReloadProgress;
                m_Marker.sprite = book.frames[Mathf.Min(book.frames.Length - 1, Mathf.FloorToInt(t * book.frames.Length))];
                m_GunArt.localRotation = Quaternion.Euler(0f, 0f, ReloadTilt * Mathf.Sin(t * Mathf.PI));
            }
            else
            {
                m_Marker.sprite = m_MarkerSprite;
                m_GunArt.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Feeds the Animator what the body is doing, faces the aim, and plays the landing thud.</summary>
        void UpdateArt()
        {
            bool grounded = IsGrounded();
            if (grounded && !m_WasGrounded && Time.time - m_AirborneSince > 0.12f)
            {
                m_LandingUntil = Time.time + 0.12f;
                Sfx.Play(Art.Lib != null ? Art.Lib.land : null, 0.55f);
            }
            if (!grounded && m_WasGrounded)
                m_AirborneSince = Time.time;
            m_WasGrounded = grounded;

            if (m_Art == null)
                return;
            var a = m_Art.Animator;
            Vector2 v = m_Body.linearVelocity;
            a.SetFloat(CharacterVisual.Speed, Mathf.Abs(v.x));
            a.SetFloat(CharacterVisual.VelocityY, v.y);
            a.SetBool(CharacterVisual.Sprint, m_SprintInput && !IsDucking);
            a.SetBool(CharacterVisual.Grounded, grounded);
            a.SetBool(CharacterVisual.Ducking, IsDucking);
            a.SetBool(CharacterVisual.Landing, Time.time < m_LandingUntil);
            a.SetBool(CharacterVisual.Dead, IsDead);
            m_Art.Face(m_FacingRight);
        }

        void Fire()
        {
            Vector2 origin = m_GunPivot.position;
            if (Gun == PlayerGun.Twin)
            {
                m_NextFireTime = Time.time + twinFireCooldown;
                TwinDirections(out var top, out var bottom);
                // One round per barrel. With a single round left, only the top barrel fires.
                Projectile.Spawn(Team.Player, origin, top, barrelLength, shot);
                Sfx.Play(Art.Lib != null ? Art.Lib.shotPlayer : null, 0.8f, 0.94f);
                if (--Ammo > 0)
                {
                    Projectile.Spawn(Team.Player, origin, bottom, barrelLength, shot);
                    Ammo--;
                }
            }
            else
            {
                m_NextFireTime = Time.time + fireCooldown;
                Projectile.Spawn(Team.Player, origin, m_AimDirection, barrelLength, shot);
                Sfx.Play(Art.Lib != null ? Art.Lib.shotPlayer : null, 0.8f);
                Ammo--;
            }
            if (Ammo <= 0)
                m_ReloadStartTime = Time.time;
        }

        /// <summary>
        /// The twin gun's two shots, fanned either side of the aim from the pivot, as the Twin enemies fire theirs.
        /// The top one leaves from the barrel that's higher on screen, whichever way the gun faces.
        /// </summary>
        void TwinDirections(out Vector2 top, out Vector2 bottom)
        {
            // Turning anticlockwise raises a shot aimed right, but lowers one aimed left.
            float half = twinSpreadDegrees * 0.5f * (m_FacingRight ? 1f : -1f);
            top = ShotPhysics.Rotate(m_AimDirection, half);
            bottom = ShotPhysics.Rotate(m_AimDirection, -half);
        }

        static PlayerGun Other(PlayerGun gun) => gun == PlayerGun.Twin ? PlayerGun.Pistol : PlayerGun.Twin;

        /// <summary>
        /// Plays out a gun switch: the old barrels shrink back into the pivot over the first half, the new ones grow out of it
        /// over the second. Once it's done, F stays locked for <see cref="gunSwitchCooldown"/>.
        /// </summary>
        void UpdateGunSwitch()
        {
            if (!IsSwitchingGun)
                return;
            float t = SwitchProgress;
            if (t >= 1f)
            {
                IsSwitchingGun = false;
                Gun = Other(Gun);
                m_NextSwitchTime = Time.time + gunSwitchCooldown;
                ShowGunArt(Gun, 1f);
                return;
            }
            ShowGunArt(t < 0.5f ? Gun : Other(Gun), Mathf.Abs(1f - 2f * t));
        }

        /// <summary>Shows <paramref name="gun"/>'s barrels, drawn out to <paramref name="extension"/> of their length.</summary>
        void ShowGunArt(PlayerGun gun, float extension)
        {
            if (m_GunArtShown != gun)
            {
                for (int i = m_GunArt.childCount - 1; i >= 0; i--)
                {
                    // Detached first, so the new marker is the only one found before the old one is gone at frame end.
                    var old = m_GunArt.GetChild(i);
                    old.SetParent(null, false);
                    Destroy(old.gameObject);
                }
                BuildBarrels(m_GunArt, gun, barrelLength, m_Art != null ? 1 : 6);
                m_GunArtShown = gun;
                m_Marker = null; // found again (with its new resting sprite) on the next reload check
            }
            m_GunArt.localScale = new Vector3(extension, 1f, 1f);
        }

        /// <summary>Barrel art for <paramref name="gun"/>, running from <paramref name="parent"/>'s origin along its +x.</summary>
        public static void BuildBarrels(Transform parent, PlayerGun gun, float length, int sortingOrder)
        {
            var lib = Art.Lib;
            var sprite = lib == null ? null : gun == PlayerGun.Twin ? lib.twinPlayer : lib.pistolPlayer;
            if (sprite != null)
            {
                // The marker sprite's pivot is its back end, on the barrel's axis.
                var go = new GameObject("Marker");
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                SortLayers.Set(sr, SortLayers.Characters, sortingOrder);
                return;
            }
            var center = new Vector2(length * 0.5f, 0f);
            if (gun == PlayerGun.Twin)
            {
                // Laid out like the Twin enemies' barrels.
                Shapes.Box(parent, "Barrel", BarrelColor, center + Vector2.up * 0.07f, new Vector2(length, 0.09f), sortingOrder);
                Shapes.Box(parent, "Barrel", BarrelColor, center - Vector2.up * 0.07f, new Vector2(length, 0.09f), sortingOrder);
            }
            else
            {
                Shapes.Box(parent, "Barrel", BarrelColor, center, new Vector2(length, 0.16f), sortingOrder);
            }
        }

        /// <summary>
        /// One laser line for each shot the next pull of the trigger fires: two for the twin gun, or just the top one
        /// when a single round is left. None mid-switch, while neither gun is ready.
        /// </summary>
        void UpdateLaser()
        {
            if (IsSwitchingGun)
            {
                m_Laser.Hide();
                m_SecondLaser.Hide();
                return;
            }

            Vector2 origin = m_GunPivot.position;
            if (Gun != PlayerGun.Twin)
            {
                m_Laser.Trace(origin, m_AimDirection, barrelLength, shot);
                m_SecondLaser.Hide();
                return;
            }

            TwinDirections(out var top, out var bottom);
            m_Laser.Trace(origin, top, barrelLength, shot);
            // An empty magazine refills completely, so a single shot only follows when exactly one round is left.
            if (Ammo == 1)
                m_SecondLaser.Hide();
            else
                m_SecondLaser.Trace(origin, bottom, barrelLength, shot);
        }

        void FixedUpdate()
        {
            RecordVelocity();
            if (IsDead)
                return;

            float speed = moveSpeed * (IsDucking ? duckMoveMultiplier : m_SprintInput ? sprintMultiplier : 1f);
            Vector2 v = m_Body.linearVelocity;
            v.x = m_MoveInput * speed;
            // Past the middle of a one-way door (the safe zone's, or the boss arena's), it slides the player the rest of the way through.
            foreach (var door in OneWayDoor.All)
                if (door.IsSlidingPlayerThrough)
                    v.x = Mathf.Max(v.x, door.slideSpeed);

            if (Time.time <= m_JumpBufferedUntil && IsGrounded())
            {
                v.y = jumpSpeed;
                m_JumpBufferedUntil = float.NegativeInfinity;
                Sfx.Play(Art.Lib != null ? Art.Lib.jump : null, 0.5f);
            }
            m_Body.linearVelocity = v;
        }

        /// <summary>Logs the velocity the last physics step left the body with.</summary>
        void RecordVelocity()
        {
            m_VelocityHistory[m_VelocityHistoryNext] = m_Body.linearVelocity;
            m_VelocityHistoryNext = (m_VelocityHistoryNext + 1) % VelocityHistoryLength;
            m_VelocityHistoryCount = Mathf.Min(m_VelocityHistoryCount + 1, VelocityHistoryLength);
        }

        /// <summary>
        /// Mean velocity over the last <paramref name="steps"/> physics steps (at most 30, fewer just after spawning).
        /// Enemies lead their shots with it, so a change of direction that only lasts a step or two doesn't throw their aim.
        /// </summary>
        public Vector2 AverageVelocity(int steps)
        {
            steps = Mathf.Min(steps, m_VelocityHistoryCount);
            if (steps <= 0)
                return m_Body.linearVelocity;
            Vector2 sum = Vector2.zero;
            for (int i = 1; i <= steps; i++)
                sum += m_VelocityHistory[(m_VelocityHistoryNext - i + VelocityHistoryLength) % VelocityHistoryLength];
            return sum / steps;
        }

        void Aim(Vector2 screenPosition)
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
            if (m_Camera == null)
                return;

            Vector2 mouseWorld = m_Camera.ScreenToWorldPoint(screenPosition);
            // Faces the side of the body the mouse is on, measured from the body's centre. Not from the gun: it sits in the
            // hands, which swap sides when the art flips, so measuring from it flipped the facing back and forth every frame.
            float fromCenter = mouseWorld.x - transform.position.x;
            if (fromCenter > FacingDeadZone)
                m_FacingRight = true;
            else if (fromCenter < -FacingDeadZone)
                m_FacingRight = false;

            Vector2 toMouse = mouseWorld - (Vector2)m_GunPivot.position;
            // With the mouse between the body's centre and the hands, the gun would point back through the body; it points
            // straight up or down instead.
            if (m_FacingRight ? toMouse.x < 0f : toMouse.x > 0f)
                toMouse.x = 0f;
            if (toMouse.sqrMagnitude >= MinAimDistance * MinAimDistance)
                m_AimDirection = toMouse.normalized;
            else if (m_FacingRight ? m_AimDirection.x < 0f : m_AimDirection.x > 0f)
                m_AimDirection.x = -m_AimDirection.x; // the mouse is on the hands: keep the last aim, on the side faced

            m_GunPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(m_AimDirection.y, m_AimDirection.x) * Mathf.Rad2Deg);
            // Mirrored when facing left, so the marker's hopper stays on top.
            m_GunPivot.localScale = new Vector3(1f, m_FacingRight ? 1f : -1f, 1f);
        }

        void SetDucking(bool wantsDuck)
        {
            if (wantsDuck == IsDucking)
                return;
            // Can't stand up into a ceiling.
            if (!wantsDuck && Overlaps((Vector2)transform.position, new Vector2(standingSize.x * 0.95f, standingSize.y - 0.05f)))
                return;

            IsDucking = wantsDuck;
            ApplyBodyShape();
            if (IsDucking)
                Sfx.Play(Art.Lib != null ? Art.Lib.crouch : null, 0.45f);
        }

        /// <summary>Shrinks/restores hitbox and visuals, keeping the feet planted.</summary>
        void ApplyBodyShape()
        {
            float height = IsDucking ? standingSize.y * (1f - duckHeightReduction) : standingSize.y;
            float centerY = (height - standingSize.y) * 0.5f;

            m_Collider.size = new Vector2(standingSize.x, height);
            m_Collider.offset = new Vector2(0f, centerY);
            m_Visual.localPosition = new Vector2(0f, centerY);
            m_Visual.localScale = new Vector3(standingSize.x, height, 1f);
            m_GunPivot.localPosition = new Vector2(0f, centerY + height * 0.2f);
        }

        bool IsGrounded()
        {
            if (m_Body.linearVelocity.y > 0.1f)
                return false;
            Bounds b = m_Collider.bounds;
            return Overlaps(new Vector2(b.center.x, b.min.y - 0.04f), new Vector2(b.size.x * 0.9f, 0.06f));
        }

        bool Overlaps(Vector2 center, Vector2 size)
        {
            Physics2D.OverlapBox(center, size, 0f, s_SolidFilter, m_Overlaps);
            // Colliders we pass through (a one-way door, until it shuts) don't count.
            foreach (var c in m_Overlaps)
                if (c != m_Collider && !Physics2D.GetIgnoreCollision(m_Collider, c))
                    return true;
            return false;
        }

        public void OnShot(Projectile projectile)
        {
            if (IsDead)
                return;
            // The same rule as the player's shots against enemies: a shot that hasn't ricocheted yet is harmless.
            if (projectile.Bounces < 1)
            {
                Enemy.HarmlessHit(projectile.Position);
                return;
            }
            if (Time.time < m_InvulnerableUntil)
                return;

            Health--;
            m_InvulnerableUntil = Time.time + invulnerableTime;
            Sfx.Play(Art.Lib != null ? Art.Lib.playerHit : null, 0.9f);
            if (IsDead)
            {
                m_Laser.Hide();
                m_SecondLaser.Hide();
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
                if (m_Art != null)
                {
                    // The death animation plays instead of the grey box, and the gun drops out of sight.
                    m_Art.Renderer.enabled = true;
                    m_Art.Animator.SetBool(CharacterVisual.Dead, true);
                    m_GunPivot.gameObject.SetActive(false);
                }
                else
                {
                    m_BodySprite.enabled = true;
                    m_BodySprite.color = new Color(0.25f, 0.25f, 0.3f);
                }
            }
        }
    }
}
