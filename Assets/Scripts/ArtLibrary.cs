using UnityEngine;

namespace RicochetPoC
{
    /// <summary>A sprite animation played in code (effects that aren't on an Animator): its frames and frame rate.</summary>
    [System.Serializable]
    public class Flipbook
    {
        public Sprite[] frames = new Sprite[0];
        [Min(1f)] public float fps = 12f;

        public bool IsValid => frames != null && frames.Length > 0 && frames[0] != null;
        public float Duration => frames.Length / fps;
    }

    /// <summary>A character's art: its Animator controller, a still for when it isn't animated yet, and its death frames.</summary>
    [System.Serializable]
    public class CharacterArt
    {
        public RuntimeAnimatorController controller;
        public Sprite idle;

        public bool IsValid => controller != null && idle != null;
    }

    /// <summary>
    /// Every sprite, animation and sound the game uses. The runtime builders (level geometry, characters, shots, HUD) read it
    /// through <see cref="Art.Lib"/>; anything missing falls back to the code-drawn placeholders, so the game always runs.
    /// Built by Ricochet ▸ Build Art Library (see ArtPipeline.cs) into Assets/Resources/ArtLibrary.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Ricochet/Art Library", fileName = "ArtLibrary")]
    public class ArtLibrary : ScriptableObject
    {
        /// <summary>One pixel density for every sprite: 30 px per metre, so each art pixel is 2×2 screen pixels at 1680×840.</summary>
        public const int PixelsPerUnit = 30;

        [Header("Characters")]
        public CharacterArt player = new CharacterArt();
        public CharacterArt enemyStandard = new CharacterArt();
        public CharacterArt enemyTwin = new CharacterArt();
        public CharacterArt enemyHeavy = new CharacterArt();
        public CharacterArt boss = new CharacterArt();
        [Tooltip("Crack overlays shown on the boss's cracked armour regions.")]
        public Sprite bossCrackHead;
        public Sprite bossCrackTorso;

        [Header("Level")]
        [Tooltip("The room's back wall, floor to ceiling (12 m), repeated along the level behind everything. No collider.")]
        public Sprite backgroundWall;
        public Sprite floor;
        public Sprite ceiling;
        public Sprite wallConcrete;
        public Sprite wallPartition;
        public Sprite labBench;
        public Sprite ceilingDuct;
        [Tooltip("Floating platforms: an aluminium pipe hung from the ceiling. Tiled along its length; 3 px end caps.")]
        public Sprite pipe;
        [Tooltip("Bunker roofs: the same pipe with hazard bands.")]
        public Sprite pipeHazard;
        [Tooltip("The rods the pipes hang from (decor, no collider), repeated up to the ceiling.")]
        public Sprite hangerRod;
        public Sprite pipeClamp;
        [Tooltip("Angled plates: a broken LED light panel hanging from one cable. Used when there's no pre-rotated version.")]
        public Sprite lightPanel;
        [Tooltip("Light panels drawn pre-rotated, one per angle in Plate Angles, so their pixels stay on the screen grid.")]
        public Sprite[] plateRotations = new Sprite[0];
        public int[] plateAngles = new int[0];
        [Tooltip("The cable a light panel hangs from (decor), repeated up to the ceiling.")]
        public Sprite cable;
        [Tooltip("The panel's snapped second cable, hanging from the ceiling (pivot at its top).")]
        public Sprite cableSnapped;
        public Sprite doorSafeZone;
        public Sprite doorArena;
        public Sprite doorExit;
        public Sprite exitSign;
        public Sprite doorLed;

        [Header("Weapons (held in both hands; pivot = rear hand on the grip)")]
        public Sprite pistolPlayer;
        public Sprite twinPlayer;
        [Tooltip("The twin gun on its own, for the pickup.")]
        public Sprite twinPickup;
        [Tooltip("Reload: the front hand fetches a paint pod and pours it into the hopper. Same pivot as the gun.")]
        public Flipbook reloadPistol = new Flipbook();
        public Flipbook reloadTwin = new Flipbook();
        public Sprite standardEnemy;
        public Sprite twinEnemy;
        public Sprite heavyEnemy;
        public Sprite standardBoss;
        public Sprite twinBoss;
        public Sprite heavyBoss;

        [Header("Shots and effects")]
        public Sprite paintballFresh;
        public Sprite paintballLive;
        public Sprite paintballLiveGlow;
        public Sprite paintballEnemy;
        public Sprite paintballHeavy;
        [Tooltip("Enemy shots once they've bounced and turned lethal: red-hot, with a red glow.")]
        public Sprite paintballEnemyLive;
        public Sprite paintballEnemyLiveGlow;
        public Sprite paintballHeavyLive;
        public Sprite paintballHeavyLiveGlow;
        public Sprite[] splatBlue = new Sprite[0];
        public Sprite[] splatOrange = new Sprite[0];
        public Sprite[] splatMagenta = new Sprite[0];
        public Sprite[] splatPurple = new Sprite[0];
        public Flipbook armFlash = new Flipbook();
        [Tooltip("The arming flash in red, where an enemy shot bounces and turns lethal.")]
        public Flipbook armFlashRed = new Flipbook();
        public Flipbook bounceSpark = new Flipbook();
        public Flipbook blockedSplat = new Flipbook();
        [Tooltip("An enemy's shot splashing on the player, in that shot's paint.")]
        public Flipbook hitMagenta = new Flipbook();
        public Flipbook hitPurple = new Flipbook();
        [Tooltip("The green paint burst of a shot that eliminates (or wounds the boss): every splash on an enemy is green.")]
        public Flipbook burstPaint = new Flipbook();
        public Sprite pickupGlow;

        [Header("UI")]
        public Texture2D ammoFull;
        public Texture2D ammoEmpty;
        public Texture2D heartFull;
        public Texture2D heartEmpty;
        public Sprite bounceIcon;
        public Sprite laser;
        public Texture2D crosshair;
        public Texture2D buttonNormal;
        public Texture2D buttonHover;
        public Texture2D buttonPressed;
        public Texture2D buttonGhost;

        [Header("Tutorial boards")]
        [Tooltip("The animated picture on each card of the level 1 and level 6 tutorial boards.")]
        public TutorialArt tutorial = new TutorialArt();

        [Header("Sound effects")]
        public AudioClip shotPlayer;
        public AudioClip shotEnemy;
        public AudioClip shotHeavy;
        [Tooltip("A player shot's first bounce: it is armed (lethal) from here on.")]
        public AudioClip ricochetArmed;
        public AudioClip ricochet;
        [Tooltip("A shot that hits without eliminating: a direct hit, or a hit on cracked-but-standing armour.")]
        public AudioClip splatHarmless;
        public AudioClip elimination;
        public AudioClip playerHit;
        public AudioClip[] footstepsWalk = new AudioClip[0];
        public AudioClip[] footstepsRun = new AudioClip[0];
        public AudioClip jump;
        public AudioClip land;
        public AudioClip crouch;
        public AudioClip uiClick;
        public AudioClip roundStart;
        public AudioClip roundClear;
        public AudioClip roundLost;
        public AudioClip gunSwitch;
        public AudioClip reload;

        [Header("Music")]
        public AudioClip musicMenu;
        public AudioClip musicGameplay;

        [Header("Hands")]
        [Tooltip("Character animation frames and, for each, where its hands are (metres from the feet pivot, facing right): the gun's grip goes there.")]
        public Sprite[] handSprites = new Sprite[0];
        public Vector2[] handOffsets = new Vector2[0];

        System.Collections.Generic.Dictionary<Sprite, Vector2> m_Hands;

        /// <summary>Where the hands are on <paramref name="frame"/>, in metres from its pivot (the feet), facing right.</summary>
        public bool TryHands(Sprite frame, out Vector2 offset)
        {
            offset = default;
            if (frame == null)
                return false;
            if (m_Hands == null)
            {
                m_Hands = new System.Collections.Generic.Dictionary<Sprite, Vector2>();
                for (int i = 0; i < handSprites.Length && i < handOffsets.Length; i++)
                    if (handSprites[i] != null)
                        m_Hands[handSprites[i]] = handOffsets[i];
            }
            return m_Hands.TryGetValue(frame, out offset);
        }

        void OnEnable() => m_Hands = null;

        /// <summary>The pre-rotated light panel for <paramref name="angle"/> degrees, or null if there isn't one.</summary>
        public Sprite PlateFor(float angle)
        {
            int rounded = Mathf.RoundToInt(angle);
            for (int i = 0; i < plateAngles.Length && i < plateRotations.Length; i++)
                if (plateAngles[i] == rounded)
                    return plateRotations[i];
            return null;
        }
    }

    /// <summary>Sorting layers, back to front. "Background" is reserved, empty for now, for the backdrop that comes later.</summary>
    public static class SortLayers
    {
        public const string Background = "Background";
        public const string Objects = "Objects";
        public const string Characters = "Characters";
        public const string Effects = "ProjectilesFX";
        public const string UI = "UI";

        /// <summary>Puts <paramref name="renderer"/> on <paramref name="layer"/> if the project has it (it does once the art pipeline has run).</summary>
        public static void Set(Renderer renderer, string layer, int order)
        {
            if (SortingLayer.NameToID(layer) != 0 || layer == "Default")
                renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
        }
    }

    /// <summary>Access to the art library. Null when it hasn't been built: callers fall back to placeholders.</summary>
    public static class Art
    {
        static ArtLibrary s_Lib;
        static bool s_Looked;

        public static ArtLibrary Lib
        {
            get
            {
                if (!s_Looked)
                {
                    s_Lib = Resources.Load<ArtLibrary>("ArtLibrary");
                    s_Looked = true;
                }
                return s_Lib;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Lib = null;
            s_Looked = false;
        }
    }
}
