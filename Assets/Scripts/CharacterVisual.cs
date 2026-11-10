using UnityEngine;

namespace RicochetPoC
{
    /// <summary>
    /// A character's sprite and Animator, on a child at the character's feet (the sprites' pivot), never scaled, so the art
    /// keeps its pixel density whatever the hitbox does. Gameplay code drives it through the Animator parameters below;
    /// the walk and run clips call <see cref="Footstep"/> from animation events on the frames where a foot lands.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public class CharacterVisual : MonoBehaviour
    {
        // Animator parameters, shared by every character controller (see ArtPipeline.cs).
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int Sprint = Animator.StringToHash("Sprint");
        public static readonly int Grounded = Animator.StringToHash("Grounded");
        public static readonly int VelocityY = Animator.StringToHash("VelocityY");
        public static readonly int Ducking = Animator.StringToHash("Ducking");
        public static readonly int Landing = Animator.StringToHash("Landing");
        public static readonly int Dead = Animator.StringToHash("Dead");

        [Tooltip("Pitch of this character's footsteps: lower reads heavier.")]
        public float footstepPitch = 1f;
        public float footstepVolume = 0.35f;

        public SpriteRenderer Renderer { get; private set; }
        public Animator Animator { get; private set; }

        /// <summary>
        /// Adds the visual under <paramref name="parent"/>, with its feet (the sprite pivot) at local height <paramref name="feetY"/>.
        /// Returns null when <paramref name="art"/> isn't usable, so the caller keeps its placeholder.
        /// </summary>
        public static CharacterVisual Create(Transform parent, CharacterArt art, float feetY, int order)
        {
            if (art == null || !art.IsValid)
                return null;
            var go = new GameObject("Art");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector2(0f, feetY);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.idle;
            SortLayers.Set(sr, SortLayers.Characters, order);
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = art.controller;
            var visual = go.AddComponent<CharacterVisual>();
            visual.Renderer = sr;
            visual.Animator = animator;
            animator.SetBool(Grounded, true);
            return visual;
        }

        void Awake()
        {
            Renderer = GetComponent<SpriteRenderer>();
            Animator = GetComponent<Animator>();
        }

        /// <summary>Faces right when <paramref name="right"/>; the art is drawn facing right and mirrored for left.</summary>
        public void Face(bool right) => Renderer.flipX = !right;

        /// <summary>
        /// Where the hands are on the frame showing right now, in the character root's local space (mirrored with the art),
        /// so the gun's grip can sit in them. False when the frame has no hand position (death frames, or no art library).
        /// </summary>
        public bool TryHandsLocal(out Vector2 local)
        {
            local = default;
            var lib = Art.Lib;
            if (lib == null || !lib.TryHands(Renderer.sprite, out var offset))
                return false;
            if (Renderer.flipX)
                offset.x = -offset.x;
            local = (Vector2)transform.localPosition + offset;
            return true;
        }

        /// <summary>Animation event on the walk and run clips: 0 for a walking step, 1 for a running one.</summary>
        public void Footstep(int running)
        {
            var lib = Art.Lib;
            if (lib == null)
                return;
            Sfx.PlayAny(running != 0 && lib.footstepsRun.Length > 0 ? lib.footstepsRun : lib.footstepsWalk,
                footstepVolume * (running != 0 ? 1.15f : 1f), footstepPitch, 0.06f);
        }
    }
}
