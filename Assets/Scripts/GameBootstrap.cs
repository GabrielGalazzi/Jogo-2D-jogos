using UnityEngine;
using UnityEngine.InputSystem;

namespace RicochetPoC
{
    /// <summary>
    /// Runs the game. It opens on the main menu, where the player picks a language and presses Play.
    /// Each level is built at runtime: its geometry, safe zone door, player, enemies and exit door, plus camera and HUD.
    /// Enemies are dealt out over random spawn spots near their placements every time a level loads (see <see cref="SpawnSpots"/>).
    /// The player starts in the safe zone. Clearing every enemy opens the exit at the far end of the level. Level 10 instead ends
    /// in the boss's closed arena, whose one-way door shuts the player in with it, and is won when the boss falls.
    /// Level 6's safe zone holds the twin gun, and the player starts every level after it with that gun.
    /// Levels 1 and 6 show a tutorial board while the player is in the safe zone. Dying brings up a menu to retry the
    /// level or go back to the main menu. Esc pauses the level, on a screen that offers the main menu; beating the boss
    /// freezes the arena under a victory screen that does the same.
    /// Drop it on an empty GameObject in any scene and press Play.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        static readonly Color WallColor = new Color(0.36f, 0.38f, 0.44f);
        /// <summary>Behind the levels: a muted off-white.</summary>
        static readonly Color BackgroundColor = new Color(0.85f, 0.84f, 0.81f);
        /// <summary>Behind the main menu, whose white title and buttons are drawn for a dark backdrop.</summary>
        static readonly Color MenuBackgroundColor = new Color(0.08f, 0.09f, 0.12f);
        /// <summary>The level title banner: drawn over the open background, so dark against the off-white.</summary>
        static readonly Color BannerColor = new Color(0.13f, 0.14f, 0.17f);
        static readonly Color DoorLockedColor = new Color(0.45f, 0.16f, 0.18f);
        static readonly Color DoorOpenColor = new Color(0.25f, 0.85f, 0.4f);
        static readonly Color MagazineColor = new Color(0.2f, 0.45f, 0.95f, 0.85f);
        static readonly Color MagazineEmptyColor = new Color(0.9f, 0.18f, 0.2f, 0.85f);

        const float DoorWidth = 1.2f;
        const float DoorHeight = 2.2f;
        const float JumperHopHeight = 1.4f;
        const float TitleDuration = 2f;
        /// <summary>Seconds after the player dies before the death menu starts fading in, and how long it takes to.</summary>
        const float DeathMenuDelay = 0.8f;
        const float DeathMenuFadeTime = 0.25f;
        /// <summary>Seconds after the boss falls (long enough for its death to play out) before the victory screen fades in, and how long it takes to.</summary>
        const float VictoryScreenDelay = 1.5f;
        const float VictoryScreenFadeTime = 0.25f;
        /// <summary>Seconds the tutorial board takes to fade out once the player leaves the safe zone. It's up from the moment the level loads.</summary>
        const float BoardFadeTime = 0.4f;
        const float Pad = 16f;

        [Range(1, Levels.Count)] public int startLevel = 1;
        [Tooltip("Go straight into Start Level when Play mode starts, skipping the main menu. For testing.")]
        public bool skipMainMenu;
        [Tooltip("Standalone window size in pixels. Scaled down, keeping its shape, on monitors too small to fit it.")]
        public Vector2Int windowSize = new Vector2Int(1680, 840);
        [Tooltip("Briefly draw the path each enemy's aim solver planned when it fires: a faint line that warns the player where the shot will bounce.")]
        public bool showEnemyAimTraces = true;
        [Tooltip("Rounds in the player's magazine.")]
        [Min(1)] public int magazineSize = 6;
        [Tooltip("Seconds the player's gun takes to reload once the magazine runs dry.")]
        [Min(0f)] public float reloadTime = 2f;
        [Tooltip("Holding Shift multiplies the player's move speed by this.")]
        [Min(1f)] public float sprintMultiplier = 1.5f;
        [Tooltip("Minimum seconds between shots from the player's twin gun (from level 6), which fires two shots for two rounds.")]
        [Min(0f)] public float twinGunFireInterval = 0.4f;
        [Tooltip("Seconds switching guns (F) takes. Neither gun fires meanwhile.")]
        [Min(0f)] public float gunSwitchTime = 0.5f;
        [Tooltip("Seconds after a gun switch finishes before F can switch again.")]
        [Min(0f)] public float gunSwitchCooldown = 1f;
        [Tooltip("Every time a level loads, deal its enemies out over random spawn spots near where it places them. Each keeps its type.")]
        public bool randomizeEnemySpawns = true;
        [Tooltip("Spawn spots per enemy in each group of nearby enemies, counting the spots the level places them on.")]
        [Min(1f)] public float spawnSpotsPerEnemy = 1.7f;
        [Tooltip("How far (m) along the level a random spawn spot can be from where the level places the enemies sharing it. Doubled where that's too cramped for enough spots.")]
        [Min(0f)] public float spawnRange = 3f;
        [Tooltip("From level 4 onwards, the chance an enemy ducks when one of the player's ricocheted shots is about to hit it. The boss never ducks.")]
        [Range(0f, 1f)] public float enemyDuckChance = 0.1f;

        [Header("Boss")]
        [Tooltip("Seconds the boss's armour stays black, and nothing hurts it, after it draws its second gun at 2/3 health and after it leaps to the ceiling at 1/3.")]
        [Min(0f)] public float bossInvulnerableTime = 4f;
        [Tooltip("Volleys per second from both of the boss's guns while its armour is black.")]
        [Min(0.1f)] public float bossRageShotsPerSecond = 2f;
        [Tooltip("Chance the boss notices a lethal shot about to hit it and tries to dodge, rolled once per shot.")]
        [Range(0f, 1f)] public float bossDodgeChance = 0.33f;
        [Tooltip("Seconds from the boss noticing a shot heading its way to starting to dodge it. Lower makes it harder to hit.")]
        [Min(0f)] public float bossReactionTime = 0.12f;

        /// <summary>The level being played; null on the main menu.</summary>
        public LevelData Level { get; private set; }
        /// <summary>
        /// True while the level is frozen under the pause screen or the victory screen: time stands still and the player's
        /// controls are ignored, so clicking a button doesn't fire a shot.
        /// </summary>
        public static bool IsPaused { get; private set; }

        Transform m_World;
        PlayerController m_Player;
        Camera m_Camera;
        CameraFollow m_CameraFollow;
        SpriteRenderer m_Door;
        OneWayDoor m_SafeZoneDoor;
        SpriteRenderer m_SafeZoneDoorSprite;
        OneWayDoor m_ArenaDoor;
        SpriteRenderer m_ArenaDoorSprite;
        Boss m_Boss;
        bool m_OnMainMenu;
        float m_TitleUntil;
        float m_DiedAt = -1f;
        /// <summary>When the boss fell (game time), and when the victory screen started to show (unscaled, as it freezes the game).</summary>
        float m_WonAt = -1f;
        float m_VictoryShownAt = -1f;
        float m_BoardAlpha;
        /// <summary>Set once the level-clear (or victory) sting has played, so it plays once per level.</summary>
        bool m_ClearAnnounced;
        GUIStyle m_Style;
        GUIStyle m_SmallStyle;
        GUIStyle m_BannerStyle;
        GUIStyle m_PanelStyle;
        int m_StyleScreenWidth;

        bool IsPlaying => !m_OnMainMenu && Level != null && m_Player != null;
        bool IsCleared => Enemy.All.Count == 0;
        bool IsFinalLevel => Level.number == Levels.Count;
        /// <summary>The boss has fallen (its guards already have: its arena only opens once they're down).</summary>
        bool HasWon => IsFinalLevel && m_Boss == null;
        /// <summary>Enemies still standing, not counting the boss.</summary>
        int GuardsLeft => Enemy.All.Count - (m_Boss != null ? 1 : 0);
        float ExitDoorX => Level.length - 0.2f - DoorWidth;
        /// <summary>0 until the death menu starts to appear, 1 once it's fully shown.</summary>
        float DeathMenuShown => m_DiedAt < 0f ? 0f : Mathf.Clamp01((Time.time - m_DiedAt - DeathMenuDelay) / DeathMenuFadeTime);
        /// <summary>0 until the victory screen starts to appear, 1 once it's fully shown.</summary>
        float VictoryScreenShown => m_VictoryShownAt < 0f ? 0f : Mathf.Clamp01((Time.unscaledTime - m_VictoryShownAt) / VictoryScreenFadeTime);
        /// <summary>Esc pauses any time the player is alive and the victory screen isn't up.</summary>
        bool CanPause => IsPlaying && !m_Player.IsDead && m_VictoryShownAt < 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsPaused = false;

        void Awake()
        {
            Lang.Load();
            ApplyWindowSize();
        }

        void OnDestroy()
        {
            // Never leave time stopped behind, e.g. when Play mode ends on the pause screen.
            if (IsPaused)
                SetPaused(false);
        }

        void Start()
        {
            Enemy.ShowAimTraces = showEnemyAimTraces;
            SetupCamera();
            if (skipMainMenu)
                LoadLevel(startLevel);
            else
                ShowMainMenu();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool confirm = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            if (m_OnMainMenu)
            {
                if (confirm)
                    LoadLevel(startLevel);
                return;
            }
            if (!IsPlaying)
                return;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && CanPause)
                SetPaused(!IsPaused);
            if (IsPaused)
            {
                // Frozen: only Esc (above) and the screen's button do anything. Enter takes the victory screen's button too.
                if (confirm && VictoryScreenShown >= 1f)
                    ShowMainMenu();
                return;
            }

            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame || (confirm && DeathMenuShown >= 1f))
                    LoadLevel(Level.number);
            }

            if (m_Player.IsDead && m_DiedAt < 0f)
            {
                m_DiedAt = Time.time;
                Sfx.Play(Art.Lib?.roundLost, 0.9f, 1f, 0f);
            }
            if (!m_ClearAnnounced && (HasWon || (IsCleared && !IsFinalLevel)) && !m_Player.IsDead)
            {
                m_ClearAnnounced = true;
                Sfx.Play(Art.Lib?.roundClear, 0.9f, 1f, 0f);
            }
            if (HasWon && !m_Player.IsDead)
            {
                if (m_WonAt < 0f)
                    m_WonAt = Time.time;
                else if (Time.time >= m_WonAt + VictoryScreenDelay)
                {
                    m_VictoryShownAt = Time.unscaledTime;
                    SetPaused(true);
                    return;
                }
            }

            // The board is up from the start, and goes once the player is on their way out of the safe zone.
            bool showBoard = Level.tutorial != null && !m_SafeZoneDoor.PlayerIsLeaving && !m_Player.IsDead;
            m_BoardAlpha = Mathf.MoveTowards(m_BoardAlpha, showBoard ? 1f : 0f, Time.deltaTime / BoardFadeTime);

            m_Door.color = IsCleared ? DoorOpenColor : DoorLockedColor;
            m_SafeZoneDoorSprite.color = m_SafeZoneDoor.IsShut ? DoorLockedColor : DoorOpenColor;
            if (m_ArenaDoor != null)
            {
                // Opens once every guard outside is down, and shuts again behind the player.
                if (m_ArenaDoor.IsLocked && GuardsLeft == 0)
                    m_ArenaDoor.Unlock();
                m_ArenaDoorSprite.color = m_ArenaDoor.IsLocked || m_ArenaDoor.IsShut ? DoorLockedColor : DoorOpenColor;
                // Shut in the arena, the camera stays inside it too, which keeps nearly all of it on screen.
                if (m_ArenaDoor.IsShut)
                    m_CameraFollow.levelMinX = Level.arena.wallX;
            }
            if (IsCleared && !IsFinalLevel && !m_Player.IsDead && m_Player.transform.position.x >= ExitDoorX)
                LoadLevel(Level.number + 1);
        }

        void ApplyWindowSize()
        {
            // In a browser the page owns the canvas size, so leave it alone.
            if (Application.isEditor || Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
                return;
            // Leave room for the window frame, title bar and taskbar.
            float scale = Mathf.Min(1f, Mathf.Min((Display.main.systemWidth - 40f) / windowSize.x, (Display.main.systemHeight - 120f) / windowSize.y));
            Screen.SetResolution(Mathf.RoundToInt(windowSize.x * scale), Mathf.RoundToInt(windowSize.y * scale), FullScreenMode.Windowed);
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = LevelData.Height * 0.5f + 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MenuBackgroundColor;
            m_Camera = cam;
            cam.transform.SetPositionAndRotation(new Vector3(0f, LevelData.Height * 0.5f, -10f), Quaternion.identity);

            if (!cam.TryGetComponent(out m_CameraFollow))
                m_CameraFollow = cam.gameObject.AddComponent<CameraFollow>();
            m_CameraFollow.height = LevelData.Height * 0.5f;
        }

        /// <summary>Tears down the current level and shows the main menu.</summary>
        public void ShowMainMenu()
        {
            SetPaused(false);
            TearDownLevel();
            Level = null;
            m_Player = null;
            m_CameraFollow.target = null;
            m_OnMainMenu = true;
            m_Camera.backgroundColor = MenuBackgroundColor;
            Sfx.Music(Art.Lib?.musicMenu);
            UseCrosshair(false);
        }

        /// <summary>
        /// Freezes the level (physics, animation, shots and the player's controls) or lets it run again. The arrow cursor
        /// stands in for the crosshair while it's frozen, for clicking the screen's button.
        /// </summary>
        void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            UseCrosshair(!paused);
        }

        /// <summary>The crosshair cursor (centred on the mouse position) while playing, or the system arrow.</summary>
        static void UseCrosshair(bool crosshair)
        {
            var lib = Art.Lib;
            if (crosshair && lib != null && lib.crosshair != null)
                Cursor.SetCursor(lib.crosshair, new Vector2(lib.crosshair.width, lib.crosshair.height) * 0.5f, CursorMode.Auto);
            else
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>Tears down the current level (if any) and builds level <paramref name="number"/> (clamped to 1–10).</summary>
        public void LoadLevel(int number)
        {
            SetPaused(false); // also brings the crosshair cursor back
            TearDownLevel();
            m_OnMainMenu = false;
            m_Camera.backgroundColor = BackgroundColor;
            Level = Levels.Create(number);
            BuildWorld();
            m_TitleUntil = Time.time + TitleDuration;
            m_DiedAt = -1f;
            m_WonAt = -1f;
            m_VictoryShownAt = -1f;
            m_BoardAlpha = Level.tutorial != null ? 1f : 0f; // shown at once, without fading in
            m_ClearAnnounced = false;

            var lib = Art.Lib;
            Sfx.Music(lib?.musicGameplay);
            Sfx.Play(lib?.roundStart, 0.9f, 1f, 0f);
        }

        void TearDownLevel()
        {
            if (m_World != null)
            {
                // Deactivate first so the player/enemies unregister immediately rather than at end of frame.
                m_World.gameObject.SetActive(false);
                Destroy(m_World.gameObject);
                m_World = null;
            }
            Projectile.DestroyAll();
        }

        void BuildWorld()
        {
            m_World = new GameObject($"Level {Level.number}").transform;
            m_Boss = null;

            SpriteFx.Root = m_World;
            BuildBackWall();
            foreach (var block in Level.blocks)
                BuildBlock(block);

            // Drawn behind everything, no collider: walking into it once it's open moves on to the next level.
            var exitCenter = new Vector2(ExitDoorX + DoorWidth * 0.5f, DoorHeight * 0.5f);
            var lib = Art.Lib;
            if (lib != null && lib.doorExit != null && lib.exitSign != null)
            {
                // The door itself stays as drawn; its EXIT sign shows red while locked and green once it opens.
                var door = ArtSprite(m_World, "Exit Door", lib.doorExit, exitCenter, SortLayers.Objects, -1);
                m_Door = ArtSprite(door.transform, "Exit Sign", lib.exitSign,
                    exitCenter + Vector2.up * (DoorHeight * 0.5f + lib.exitSign.bounds.extents.y + 0.1f), SortLayers.Objects, -1);
                door.gameObject.SetActive(!IsFinalLevel);
            }
            else
            {
                m_Door = Shapes.Box(m_World, "Exit Door", DoorLockedColor, exitCenter, new Vector2(DoorWidth, DoorHeight), -1);
                m_Door.gameObject.SetActive(!IsFinalLevel);
            }

            // Solid like a wall, except to shots heading into the safe zone and to the player on their way out.
            m_SafeZoneDoor = CreateOneWayDoor("Safe Zone Door", LevelData.SafeZoneDoorRect, out m_SafeZoneDoorSprite);
            m_SafeZoneDoor.MakeSafeZoneDoor();
            // The boss arena's: solid to every shot, and to the player until the guards are down and again once they're in.
            m_ArenaDoor = Level.arena != null ? CreateOneWayDoor("Arena Door", Level.arena.DoorRect, out m_ArenaDoorSprite) : null;
            if (m_ArenaDoor != null)
                m_ArenaDoor.Lock();

            m_CameraFollow.levelMinX = LevelData.SafeZoneMinX - 1f;
            m_CameraFollow.levelMaxX = Level.length + 1f;

            var playerGo = new GameObject("Player");
            playerGo.SetActive(false); // configure before Awake runs
            playerGo.transform.SetParent(m_World, false);
            m_Player = playerGo.AddComponent<PlayerController>();
            m_Player.magazineSize = magazineSize;
            m_Player.reloadTime = reloadTime;
            m_Player.sprintMultiplier = sprintMultiplier;
            m_Player.hasTwinGun = Level.number > Levels.TwinGunLevel;
            m_Player.twinFireCooldown = twinGunFireInterval;
            m_Player.gunSwitchTime = gunSwitchTime;
            m_Player.gunSwitchCooldown = gunSwitchCooldown;
            playerGo.transform.position = Level.playerSpawnFeet + Vector2.up * m_Player.standingSize.y * 0.5f;
            playerGo.SetActive(true);
            m_SafeZoneDoor.Admit(m_Player);
            if (m_ArenaDoor != null)
                m_ArenaDoor.Admit(m_Player);

            if (Level.number == Levels.TwinGunLevel)
            {
                var pickup = new GameObject("Twin Gun Pickup").transform;
                pickup.SetParent(m_World, false);
                pickup.position = LevelData.SafeZonePickupPoint;
                pickup.gameObject.AddComponent<GunPickup>();
            }

            var enemies = new Enemy[Level.enemies.Count];
            var anchors = new Vector2[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i] = CreateEnemy(Level.enemies[i]);
                anchors[i] = Level.enemies[i].anchor;
            }
            if (randomizeEnemySpawns)
            {
                Physics2D.SyncTransforms(); // spawn spots are found by casting against the blocks built above
                SpawnSpots.Scatter(enemies, anchors, spawnSpotsPerEnemy, spawnRange, Level.SpawnMaxX);
            }
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].PlaceOn(anchors[i]);
                enemies[i].gameObject.SetActive(true);
            }

            Physics2D.SyncTransforms();
            m_CameraFollow.target = playerGo.transform;
            m_CameraFollow.SnapToTarget();
        }

        /// <summary>
        /// The room's back wall, floor to ceiling, repeated along the whole level (safe zone and boss arena included) behind
        /// everything else. It's decoration only, with no collider. Without the art, the camera's plain background shows.
        /// </summary>
        void BuildBackWall()
        {
            var sprite = Art.Lib != null ? Art.Lib.backgroundWall : null;
            if (sprite == null)
                return;
            // The same span as the floor, from the outer face of one end wall to the other.
            float minX = LevelData.SafeZoneMinX - 1f, maxX = Level.length + 1f;
            var wall = ArtSprite(m_World, "Back Wall", sprite, new Vector2((minX + maxX) * 0.5f, LevelData.Height * 0.5f),
                SortLayers.Background, 0);
            wall.drawMode = SpriteDrawMode.Tiled;
            wall.tileMode = SpriteTileMode.Continuous;
            wall.size = new Vector2(maxX - minX, LevelData.Height);
        }

        /// <summary>
        /// A one-way door filling <paramref name="rect"/>, green until the player is through, then red. With the art in, the
        /// door is drawn as it is and <paramref name="sprite"/> is its status LED strip, which carries the colour.
        /// </summary>
        OneWayDoor CreateOneWayDoor(string name, Rect rect, out SpriteRenderer sprite)
        {
            var lib = Art.Lib;
            var art = lib == null ? null : name.StartsWith("Arena") ? lib.doorArena : lib.doorSafeZone;
            if (art == null || lib.doorLed == null)
            {
                sprite = Shapes.Box(m_World, name, DoorOpenColor, rect.center, rect.size, 1);
                sprite.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
                return sprite.gameObject.AddComponent<OneWayDoor>();
            }

            var door = ArtSprite(m_World, name, art, rect.center, SortLayers.Objects, 1);
            door.gameObject.AddComponent<BoxCollider2D>().size = rect.size;
            // The LED channel runs down the door's right-hand stile, one pixel in from its edge.
            float px = 1f / ArtLibrary.PixelsPerUnit;
            sprite = ArtSprite(door.transform, "Status Light", lib.doorLed,
                rect.center + new Vector2(rect.width * 0.5f - px * 2.5f, 0f), SortLayers.Objects, 2);
            return door.gameObject.AddComponent<OneWayDoor>();
        }

        /// <summary>An unscaled sprite at <paramref name="position"/>, so it shows at the art's own pixel density.</summary>
        static SpriteRenderer ArtSprite(Transform parent, string name, Sprite sprite, Vector2 position, string layer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            SortLayers.Set(sr, layer, order);
            return sr;
        }

        /// <summary>
        /// A block of level geometry: its collider at exactly the size and angle the level gives, and the art for its kind.
        /// Long pieces repeat their sprite along their length at the same pixel density (Tiled); floating platforms and bunker
        /// roofs are aluminium pipes hung from the ceiling (their end caps kept by 9-slice); angled plates are broken light
        /// panels hanging from one cable, drawn pre-rotated to their angle so the pixels stay on the screen grid.
        /// The rods and cables are decoration only: they have no collider, and shots pass through them.
        /// </summary>
        void BuildBlock(Block block)
        {
            var lib = Art.Lib;
            Sprite sprite = null;
            if (lib != null)
            {
                switch (block.kind)
                {
                    case BlockKind.Floor: sprite = lib.floor; break;
                    case BlockKind.Ceiling: sprite = lib.ceiling; break;
                    case BlockKind.Wall: sprite = lib.wallConcrete; break;
                    case BlockKind.Partition: sprite = lib.wallPartition; break;
                    case BlockKind.LowWall: sprite = lib.labBench; break;
                    case BlockKind.CeilingDrop: sprite = lib.ceilingDuct; break;
                    case BlockKind.Platform: sprite = lib.pipe; break;
                    case BlockKind.Slab: sprite = lib.pipeHazard; break;
                    case BlockKind.Plate: sprite = lib.PlateFor(block.angle) ?? lib.lightPanel; break;
                }
            }
            if (sprite != null)
            {
                if (block.kind == BlockKind.Platform || block.kind == BlockKind.Slab)
                    HangPipe(block, lib);
                else if (block.kind == BlockKind.Plate)
                    HangPanel(block, lib);
            }

            if (sprite == null)
            {
                var box = Shapes.Box(m_World, "Block", WallColor, block.center, block.size);
                box.transform.rotation = Quaternion.Euler(0f, 0f, block.angle);
                box.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
                return;
            }

            var go = new GameObject(block.kind.ToString());
            go.transform.SetParent(m_World, false);
            go.transform.SetPositionAndRotation(block.center, Quaternion.Euler(0f, 0f, block.angle));
            go.AddComponent<BoxCollider2D>().size = block.size;

            // The visual is a child, so a pre-rotated plate can sit level while the collider keeps its angle.
            var visual = ArtSprite(go.transform, "Art", sprite, block.center, SortLayers.Objects, 0);
            bool preRotated = block.kind == BlockKind.Plate && sprite != lib.lightPanel;
            if (preRotated)
            {
                visual.transform.rotation = Quaternion.identity;
                return;
            }
            Vector2 native = sprite.bounds.size;
            bool exact = Mathf.Abs(native.x - block.size.x) < 0.02f && Mathf.Abs(native.y - block.size.y) < 0.02f;
            if (!exact)
            {
                // Tiled keeps a sprite's 9-slice border (a ledge's end caps) and repeats its middle rather than stretching it.
                visual.drawMode = SpriteDrawMode.Tiled;
                visual.tileMode = SpriteTileMode.Continuous;
                visual.size = block.size;
            }
        }

        /// <summary>How far in from each end of a pipe its two hanger rods are (m).</summary>
        const float HangerInset = 0.35f;

        /// <summary>Two rods from the pipe's top up to the ceiling, each with a clamp where it grips the pipe.</summary>
        void HangPipe(Block block, ArtLibrary lib)
        {
            if (lib.hangerRod == null)
                return;
            float top = block.center.y + block.size.y * 0.5f;
            float half = block.size.x * 0.5f - HangerInset;
            foreach (float x in new[] { block.center.x - half, block.center.x + half })
            {
                VerticalLine("Hanger Rod", lib.hangerRod, x, top, LevelData.Height, -3);
                if (lib.pipeClamp != null)
                    ArtSprite(m_World, "Pipe Clamp", lib.pipeClamp, new Vector2(x, top - 2f / ArtLibrary.PixelsPerUnit), SortLayers.Objects, 1);
            }
        }

        /// <summary>
        /// A broken light panel: still hanging from the cable at its upper end, while its other cable has snapped and dangles
        /// from the ceiling above the lower end.
        /// </summary>
        void HangPanel(Block block, ArtLibrary lib)
        {
            if (lib.cable == null)
                return;
            float a = block.angle * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (block.size.x * 0.5f - 0.15f);
            Vector2 upper = block.center + (along.y >= 0f ? along : -along);
            Vector2 lower = block.center - (along.y >= 0f ? along : -along);
            VerticalLine("Panel Cable", lib.cable, upper.x, upper.y, LevelData.Height, -3);
            if (lib.cableSnapped != null)
                ArtSprite(m_World, "Snapped Cable", lib.cableSnapped, new Vector2(lower.x, LevelData.Height), SortLayers.Objects, -3);
        }

        /// <summary>A rod or cable sprite repeated from <paramref name="bottom"/> up to <paramref name="top"/> at x.</summary>
        void VerticalLine(string name, Sprite sprite, float x, float bottom, float top, int order)
        {
            if (top <= bottom)
                return;
            var sr = ArtSprite(m_World, name, sprite, new Vector2(x, (bottom + top) * 0.5f), SortLayers.Objects, order);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(sprite.bounds.size.x, top - bottom);
        }

        /// <summary>Builds and configures an enemy, still inactive so it can be placed before it wakes.</summary>
        Enemy CreateEnemy(EnemySpawn spawn)
        {
            var go = new GameObject(spawn.boss ? "Boss" : "Enemy");
            go.SetActive(false); // configure before Awake runs
            go.transform.SetParent(m_World, false);

            var enemy = spawn.boss ? go.AddComponent<Boss>() : go.AddComponent<Enemy>();
            enemy.mount = spawn.mount;
            enemy.weapon = spawn.weapon;
            enemy.fireInterval = Level.fireInterval;
            enemy.shot.bounceNoiseDegrees = Level.aimNoiseDegrees;
            enemy.heavyShot.bounceNoiseDegrees = Level.aimNoiseDegrees * 0.75f;
            if (spawn.hops)
                enemy.hopHeight = JumperHopHeight;
            if (!spawn.boss && Level.number >= Levels.FirstDuckingLevel)
                enemy.duckChance = enemyDuckChance;
            if (spawn.boss)
            {
                enemy.size = new Vector2(1.4f, 2.8f);
                enemy.barrelLength = 1f;
                enemy.engageRange = float.PositiveInfinity; // shut in its arena with the player, it fires wherever they are
                m_Boss = (Boss)enemy;
                m_Boss.arenaDoor = m_ArenaDoor;
                if (Level.arena != null)
                    m_Boss.roamRange = Level.arena.bossRange;
                m_Boss.rageDuration = bossInvulnerableTime;
                m_Boss.rageShotsPerSecond = bossRageShotsPerSecond;
                m_Boss.dodgeChance = bossDodgeChance;
                m_Boss.reactionTime = bossReactionTime;
            }
            return enemy;
        }

        string LevelName => Lang.Pick($"Level {Level.number}/{Levels.Count} · {Level.title}", $"Fase {Level.number}/{Levels.Count} · {Level.title}");

        void OnGUI()
        {
            if (m_OnMainMenu)
            {
                if (Menus.Main(Lang.Pick("RICOCHET", "RICOCHETE")))
                    LoadLevel(startLevel);
                return;
            }
            if (!IsPlaying)
                return;

            if (m_Style == null || m_StyleScreenWidth != Screen.width)
            {
                m_StyleScreenWidth = Screen.width;
                int size = Mathf.Max(14, Mathf.Min(Screen.width / 30, Screen.height / 40));
                m_Style = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
                m_Style.normal.textColor = Color.white;
                m_SmallStyle = new GUIStyle(m_Style) { fontSize = Mathf.Max(12, size * 3 / 4) };
                m_BannerStyle = new GUIStyle(m_Style) { fontSize = Mathf.Min(Screen.width / 10, Screen.height / 12), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                // Hover too: the banner's rect is the whole screen, so the mouse is always over it.
                m_BannerStyle.normal.textColor = m_BannerStyle.hover.textColor = BannerColor;
                m_PanelStyle = new GUIStyle(m_SmallStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            }

            // Top right, kept clear of the wall between the safe zone and the room.
            Rect? board = null;
            if (m_BoardAlpha > 0f)
            {
                var previous = GUI.color;
                GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * m_BoardAlpha);
                var cam = Camera.main;
                float wallX = cam != null ? cam.WorldToScreenPoint(Vector3.zero).x : 0f;
                board = Level.tutorial.Draw(Pad, wallX + Pad, Art.Lib != null ? Art.Lib.tutorial : null);
                GUI.color = previous;
            }

            // Bottom left: the magazine, and the gun panel once there's a second gun. Bottom right: health, as hearts.
            // The boss's bar sits between them, in the same row.
            Rect panels = Magazine(m_Player);
            if (m_Player.hasTwinGun)
                panels = GunPanel(m_Player, panels);
            Rect hearts = Hearts(m_Player);
            if (Boss.Instance != null && Boss.Instance.HasBeenSeen)
                BossBar(Boss.Instance, panels, hearts);

            if (m_Player.IsDead)
            {
                if (DeathMenuShown <= 0f)
                    return;
                switch (Menus.Death(DeathMenuShown, LevelName))
                {
                    case Menus.DeathChoice.Retry:
                        LoadLevel(Level.number);
                        break;
                    case Menus.DeathChoice.MainMenu:
                        ShowMainMenu();
                        break;
                }
                return;
            }

            if (m_VictoryShownAt >= 0f)
            {
                if (Menus.Victory(VictoryScreenShown))
                    ShowMainMenu();
                return;
            }
            if (IsPaused)
            {
                if (Menus.Pause(LevelName))
                    ShowMainMenu();
                return;
            }

            if (Time.time < m_TitleUntil)
            {
                string title = $"{Lang.Pick("LEVEL", "FASE")} {Level.number}\n{Level.title}";
                var area = new Rect(0f, 0f, Screen.width, Screen.height);
                // The tutorial board is up from the start too: where the title would overlap it, the title is centred in the
                // space to its left instead.
                if (board.HasValue)
                {
                    Vector2 size = m_BannerStyle.CalcSize(new GUIContent(title));
                    var centred = new Rect((Screen.width - size.x) * 0.5f, (Screen.height - size.y) * 0.5f, size.x, size.y);
                    if (centred.Overlaps(board.Value))
                        area.width = board.Value.xMin;
                }
                GUI.Label(area, title, m_BannerStyle);
            }
        }

        static string RegionName(Boss.Region region)
        {
            switch (region)
            {
                case Boss.Region.Head: return Lang.Pick("Head", "Cabeça");
                case Boss.Region.Front: return Lang.Pick("Front", "Frente");
                default: return Lang.Pick("Back", "Costas");
            }
        }

        /// <summary>
        /// The boss's health bar, in the bottom row with the player's panels and the same height as them: centred in the
        /// space between the left-hand panels (<paramref name="left"/>) and the hearts (<paramref name="right"/>), with
        /// its name and armour state written across it.
        /// </summary>
        void BossBar(Boss boss, Rect left, Rect right)
        {
            string cracked = "";
            foreach (Boss.Region region in System.Enum.GetValues(typeof(Boss.Region)))
                if (boss.IsCracked(region))
                    cracked += (cracked.Length > 0 ? ", " : "") + RegionName(region);
            string text = boss.IsInvulnerable ? Lang.Pick("THE WARDEN     invulnerable", "O GUARDIÃO     invulnerável")
                : cracked.Length > 0 ? Lang.Pick($"THE WARDEN     cracked: {cracked}", $"O GUARDIÃO     rachaduras: {cracked}")
                : Lang.Pick("THE WARDEN     cracked: none", "O GUARDIÃO     rachaduras: nenhuma");

            float space = right.xMin - left.xMax - Pad * 2f;
            if (space <= 0f)
                return;
            float width = Mathf.Min(Screen.width * 0.4f - Pad, space);
            var bar = new Rect((left.xMax + right.xMin - width) * 0.5f, left.y, width, left.height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = boss.IsInvulnerable ? new Color(0.35f, 0.36f, 0.4f) : new Color(0.9f, 0.2f, 0.25f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * boss.Health / boss.maxHealth, bar.height), Texture2D.whiteTexture);

            // Shadowed so it reads over the red fill as well as the dark track.
            var content = new GUIContent(text);
            GUI.color = Color.black;
            GUI.Label(new Rect(bar.x + 2f, bar.y + 2f, bar.width, bar.height), content, m_PanelStyle);
            GUI.color = previous;
            GUI.Label(bar, content, m_PanelStyle);
        }

        /// <summary>
        /// Bottom-left magazine: one round per pip, emptied right to left as shots are fired. While reloading, the panel
        /// turns red and refills with blue from the left, filling each pip back in as the edge passes over it.
        /// Returns the panel's screen rect.
        /// </summary>
        /// <summary>
        /// Bottom-right health panel, the same height as the magazine: one heart per hit point, emptied right to left as the
        /// player is hit. It flashes red for a moment on each hit. Returns the panel's screen rect.
        /// </summary>
        Rect Hearts(PlayerController player)
        {
            int hearts = player.maxHealth;
            float pip = Mathf.Max(12f, Screen.height / 30f);
            float gap = pip * 0.35f, inset = pip * 0.4f;
            float width = hearts * pip + (hearts - 1) * gap + inset * 2f;
            var panel = new Rect(Screen.width - Pad - width, Screen.height - 8f - pip - inset * 2f, width, pip + inset * 2f);

            if (player.Health != m_HeartsShown)
            {
                if (player.Health < m_HeartsShown && m_HeartsShown != int.MaxValue)
                    m_HeartsFlashUntil = Time.time + 0.35f;
                m_HeartsShown = player.Health;
            }
            var previous = GUI.color;
            GUI.color = Time.time < m_HeartsFlashUntil ? HeartsFlashColor : HeartsPanelColor;
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var lib = Art.Lib;
            Texture full = lib != null && lib.heartFull != null ? lib.heartFull : Shapes.Circle.texture;
            Texture empty = lib != null && lib.heartEmpty != null ? lib.heartEmpty : Shapes.Ring.texture;
            for (int i = 0; i < hearts; i++)
            {
                var r = new Rect(panel.x + inset + i * (pip + gap), panel.y + inset, pip, pip);
                GUI.DrawTexture(r, i < player.Health ? full : empty);
            }
            GUI.color = previous;
            return panel;
        }

        static readonly Color HeartsPanelColor = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color HeartsFlashColor = new Color(0.9f, 0.18f, 0.2f, 0.85f);
        int m_HeartsShown = int.MaxValue;
        float m_HeartsFlashUntil;

        Rect Magazine(PlayerController player)
        {
            int rounds = player.magazineSize;
            float pip = Mathf.Max(12f, Screen.height / 30f);
            float gap = pip * 0.35f, inset = pip * 0.4f;
            // Sits low enough to stay within the floor strip rather than over the playfield.
            var panel = new Rect(Pad, Screen.height - 8f - pip - inset * 2f, rounds * pip + (rounds - 1) * gap + inset * 2f, pip + inset * 2f);
            float fillX = panel.x + panel.width * player.ReloadProgress;

            var previous = GUI.color;
            if (player.IsReloading)
            {
                GUI.color = MagazineEmptyColor;
                GUI.DrawTexture(panel, Texture2D.whiteTexture);
            }
            GUI.color = MagazineColor;
            GUI.DrawTexture(new Rect(panel.x, panel.y, fillX - panel.x, panel.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
            // Paintball icons when the art is in (a loaded ball, an empty socket), otherwise filled and hollow circles.
            var lib = Art.Lib;
            Texture full = lib != null && lib.ammoFull != null ? lib.ammoFull : Shapes.Circle.texture;
            Texture empty = lib != null && lib.ammoEmpty != null ? lib.ammoEmpty : Shapes.Ring.texture;
            for (int i = 0; i < rounds; i++)
            {
                var r = new Rect(panel.x + inset + i * (pip + gap), panel.y + inset, pip, pip);
                if (!player.IsReloading)
                {
                    GUI.DrawTexture(r, i < player.Ammo ? full : empty);
                    continue;
                }
                GUI.DrawTexture(r, empty);
                float filled = Mathf.Clamp01((fillX - r.x) / r.width);
                if (filled > 0f)
                    GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, r.width * filled, r.height), full, new Rect(0f, 0f, filled, 1f));
            }
            GUI.color = previous;
            return panel;
        }

        static string GunName(PlayerGun gun) => gun == PlayerGun.Twin ? Lang.Pick("Twin gun", "Arma dupla") : Lang.Pick("Pistol", "Pistola");

        /// <summary>
        /// Panel just right of the magazine, naming the gun in hand. A switch empties it and fills it back up from the left
        /// as the new gun comes out; after that it stays dimmed until F can switch again. Returns the panel's screen rect.
        /// </summary>
        Rect GunPanel(PlayerController player, Rect magazine)
        {
            float textWidth = Mathf.Max(m_PanelStyle.CalcSize(new GUIContent(GunName(PlayerGun.Pistol))).x, m_PanelStyle.CalcSize(new GUIContent(GunName(PlayerGun.Twin))).x);
            var panel = new Rect(magazine.xMax + Pad * 0.5f, magazine.y, textWidth + magazine.height, magazine.height);

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            var fill = MagazineColor;
            if (!player.IsSwitchingGun && !player.CanSwitchGun)
                fill.a *= 0.45f;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width * player.SwitchProgress, panel.height), Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(panel, GunName(player.NextGun), m_PanelStyle);
            return panel;
        }

        /// <summary>
        /// Word-wrapped label with a drop shadow from <paramref name="x"/> to <paramref name="right"/> (by default the
        /// window's right margin); returns the y below it.
        /// </summary>
        static float Label(float y, string text, GUIStyle style, float x = Pad, float right = -1f)
        {
            var content = new GUIContent(text);
            float width = (right < 0f ? Screen.width - Pad : right) - x;
            float height = style.CalcHeight(content, width);
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(x + 2f, y + 2f, width, height), content, style);
            GUI.color = previous;
            GUI.Label(new Rect(x, y, width, height), content, style);
            return y + height;
        }
    }
}
