using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// A door at the foot of a wall that the player can walk through once, heading right (+x), and never come back through.
    /// When their centre passes the middle of the door, it slides them the rest of the way through, whatever they press;
    /// once all of them is past, it shuts behind them.
    /// Every level starts behind one: the safe zone's door, which also lets shots in (heading −x) but never out, so nothing
    /// fired in the safe zone gets out. Level 10's boss arena is entered through another, which no shot passes either way,
    /// and which stays locked (solid to the player too) until every guard outside it is down.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class OneWayDoor : MonoBehaviour
    {
        /// <summary>Every door in the current level.</summary>
        public static readonly List<OneWayDoor> All = new List<OneWayDoor>();
        /// <summary>The current level's safe zone door.</summary>
        public static OneWayDoor SafeZone { get; private set; }

        /// <summary>True until the player is on their way out of the safe zone; enemies hold fire until then.</summary>
        public static bool PlayerIsSafe => SafeZone != null && !SafeZone.PlayerIsLeaving;

        [Tooltip("Speed (m/s) the door slides the player through at once they're past its middle. Walking through faster isn't slowed down.")]
        public float slideSpeed = 2.5f;
        [Tooltip("Shots heading back the way the player came (−x) pass through it, as the safe zone's door lets shots in.")]
        public bool letsShotsBack;

        /// <summary>
        /// Set once the player's centre has passed the middle of the door. For the safe zone door, enemies wake at the same
        /// moment the slide starts, so the player can never be shot at and still turn back; and as the gun only clears the
        /// door after that, nothing can be shot from the doorway at enemies that aren't allowed to shoot back.
        /// </summary>
        public bool PlayerIsLeaving { get; private set; }
        /// <summary>True while the door is sliding the player through: the player can't move left, and moves right at least <see cref="slideSpeed"/>.</summary>
        public bool IsSlidingPlayerThrough => PlayerIsLeaving && !IsShut;
        /// <summary>Set once the player is all the way through; from then on the door blocks them too.</summary>
        public bool IsShut { get; private set; }
        /// <summary>While locked, the door is solid to the player as well.</summary>
        public bool IsLocked { get; private set; }
        /// <summary>The door's far face. Once it has shut, the player can't get any further left than this.</summary>
        public float OuterX => m_Collider.bounds.max.x;

        BoxCollider2D m_Collider;
        Collider2D m_Player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            SafeZone = null;
        }

        /// <summary>Whether a shot heading along <paramref name="direction"/> passes through <paramref name="collider"/> rather than hitting it.</summary>
        public static bool LetsShotThrough(Collider2D collider, Vector2 direction)
        {
            if (direction.x >= 0f)
                return false;
            foreach (var door in All)
                if (door.letsShotsBack && collider == door.m_Collider)
                    return true;
            return false;
        }

        void Awake() => m_Collider = GetComponent<BoxCollider2D>();

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            if (SafeZone == this)
                SafeZone = null;
        }

        /// <summary>Makes this the level's safe zone door, which lets shots in but none out.</summary>
        public void MakeSafeZoneDoor()
        {
            SafeZone = this;
            letsShotsBack = true;
        }

        /// <summary>Lets <paramref name="player"/> walk through (once it's unlocked) until they are all the way past.</summary>
        public void Admit(PlayerController player)
        {
            m_Player = player.GetComponent<Collider2D>();
            Physics2D.IgnoreCollision(m_Player, m_Collider, !IsLocked);
        }

        /// <summary>Keeps the player out until <see cref="Unlock"/> is called.</summary>
        public void Lock()
        {
            IsLocked = true;
            if (m_Player != null)
                Physics2D.IgnoreCollision(m_Player, m_Collider, false);
        }

        public void Unlock()
        {
            if (!IsLocked)
                return;
            IsLocked = false;
            if (m_Player != null && !IsShut)
                Physics2D.IgnoreCollision(m_Player, m_Collider, true);
        }

        // Checked every physics step rather than every frame, so the player can't slip through and back between two frames.
        void FixedUpdate()
        {
            if (IsShut || IsLocked || m_Player == null)
                return;

            Bounds player = m_Player.bounds;
            if (player.center.x > m_Collider.bounds.center.x)
                PlayerIsLeaving = true;
            if (player.min.x >= OuterX)
            {
                IsShut = true;
                Physics2D.IgnoreCollision(m_Player, m_Collider, false);
            }
        }
    }
}
