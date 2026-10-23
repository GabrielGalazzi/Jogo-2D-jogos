using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RicochetPoC
{
    /// <summary>
    /// A/D move, W jump, S duck (height and hitbox shrink), mouse aims the gun, left click fires.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class PlayerController : MonoBehaviour, IShootable
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        public float moveSpeed = 7f;
        public float duckMoveMultiplier = 0.5f;
        public float jumpSpeed = 15f;
        public float gravityScale = 3f;

        [Header("Body")]
        public Vector2 standingSize = new Vector2(0.8f, 1.6f);
        [Range(0f, 0.9f)] public float duckHeightReduction = 0.4f;

        [Header("Gun")]
        public float barrelLength = 0.85f;
        public float fireCooldown = 0.15f;
        public ProjectileSettings shot = new ProjectileSettings();

        [Header("Health")]
        public int maxHealth = 3;
        public float invulnerableTime = 0.75f;

        public Team Team => Team.Player;
        public int Health { get; private set; }
        public bool IsDead => Health <= 0;
        public bool IsDucking { get; private set; }
        public Vector2 Velocity => m_Body.linearVelocity;
        /// <summary>Centre of the current hitbox — what enemies aim at.</summary>
        public Vector2 AimPoint => m_Collider.bounds.center;

        static readonly Color BodyColor = new Color(0.25f, 0.6f, 1f);
        const float JumpBufferTime = 0.12f;

        static ContactFilter2D s_SolidFilter = CreateSolidFilter();
        readonly List<Collider2D> m_Overlaps = new List<Collider2D>(8);

        Rigidbody2D m_Body;
        BoxCollider2D m_Collider;
        Transform m_Visual;
        SpriteRenderer m_BodySprite;
        Transform m_GunPivot;
        Camera m_Camera;

        float m_MoveInput;
        float m_JumpBufferedUntil = float.NegativeInfinity;
        float m_NextFireTime;
        float m_InvulnerableUntil;
        Vector2 m_AimDirection = Vector2.right;

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

            m_Body = GetComponent<Rigidbody2D>();
            m_Body.gravityScale = gravityScale;
            m_Body.freezeRotation = true;
            m_Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            m_Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            m_Collider = GetComponent<BoxCollider2D>();
            m_Collider.sharedMaterial = new PhysicsMaterial2D("Player (no friction)") { friction = 0f, bounciness = 0f };

            m_BodySprite = Shapes.Box(transform, "Body", BodyColor, Vector2.zero, standingSize, 5);
            m_Visual = m_BodySprite.transform;
            m_GunPivot = new GameObject("Gun Pivot").transform;
            m_GunPivot.SetParent(transform, false);
            Shapes.Box(m_GunPivot, "Barrel", new Color(0.15f, 0.15f, 0.18f), new Vector2(barrelLength * 0.5f, 0f), new Vector2(barrelLength, 0.16f), 6);

            ApplyBodyShape();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (IsDead)
                return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            m_MoveInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            if (keyboard.wKey.wasPressedThisFrame)
                m_JumpBufferedUntil = Time.time + JumpBufferTime;

            SetDucking(keyboard.sKey.isPressed);
            Aim(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame && Time.time >= m_NextFireTime)
            {
                m_NextFireTime = Time.time + fireCooldown;
                Projectile.Spawn(Team.Player, m_GunPivot.position, m_AimDirection, barrelLength, shot);
            }

            // Blink while invulnerable.
            m_BodySprite.enabled = Time.time >= m_InvulnerableUntil || Mathf.Repeat(Time.time, 0.12f) < 0.06f;
        }

        void FixedUpdate()
        {
            if (IsDead)
                return;

            float speed = moveSpeed * (IsDucking ? duckMoveMultiplier : 1f);
            Vector2 v = m_Body.linearVelocity;
            v.x = m_MoveInput * speed;

            if (Time.time <= m_JumpBufferedUntil && IsGrounded())
            {
                v.y = jumpSpeed;
                m_JumpBufferedUntil = float.NegativeInfinity;
            }
            m_Body.linearVelocity = v;
        }

        void Aim(Vector2 screenPosition)
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
            if (m_Camera == null)
                return;

            Vector2 mouseWorld = m_Camera.ScreenToWorldPoint(screenPosition);
            Vector2 toMouse = mouseWorld - (Vector2)m_GunPivot.position;
            if (toMouse.sqrMagnitude < 1e-4f)
                return;

            m_AimDirection = toMouse.normalized;
            m_GunPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg);
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
            foreach (var c in m_Overlaps)
                if (c != m_Collider)
                    return true;
            return false;
        }

        public void OnShot(Projectile projectile)
        {
            if (IsDead || Time.time < m_InvulnerableUntil)
                return;

            Health--;
            m_InvulnerableUntil = Time.time + invulnerableTime;
            if (IsDead)
            {
                m_BodySprite.enabled = true;
                m_BodySprite.color = new Color(0.25f, 0.25f, 0.3f);
                m_Body.linearVelocity = new Vector2(0f, m_Body.linearVelocity.y);
            }
        }
    }
}
