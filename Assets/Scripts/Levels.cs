using System.Collections.Generic;
using UnityEngine;

namespace RicochetPoC
{
    /// <summary>What a block is, which decides its art. Every kind is solid level geometry that shots ricochet off.</summary>
    public enum BlockKind
    {
        /// <summary>A free-standing slab, e.g. a bunker roof.</summary>
        Slab,
        Floor,
        Ceiling,
        /// <summary>The level's outer walls: the safe zone's back wall and the end wall.</summary>
        Wall,
        /// <summary>A wall standing on a one-way door: the safe zone's and the boss arena's.</summary>
        Partition,
        LowWall,
        CeilingDrop,
        Platform,
        /// <summary>An angled plate to bank shots off.</summary>
        Plate,
    }

    public struct Block
    {
        public Vector2 center;
        public Vector2 size;
        public float angle;
        public BlockKind kind;
    }

    public struct EnemySpawn
    {
        public Vector2 anchor;     // where the feet touch: the surface below (floor mount) or above (ceiling mount)
        public EnemyMount mount;
        public EnemyWeapon weapon;
        public bool hops;
        public bool boss;
    }

    /// <summary>
    /// A closed room at the end of a level, walled off from the rest of it, that the player enters through a one-way door at
    /// the foot of its wall and can't leave: level 10's boss arena.
    /// </summary>
    public class BossArena
    {
        /// <summary>Near face of the wall between the arena and the rest of the level.</summary>
        public float wallX;
        /// <summary>How far along the level the boss's centre can move, from x to y.</summary>
        public Vector2 bossRange;

        /// <summary>The door: on the floor, centred in the thickness of the wall above it, the same size as the safe zone's.</summary>
        public Rect DoorRect => new Rect(wallX + (LevelData.SafeZoneWallThickness - LevelData.SafeZoneDoorWidth) * 0.5f, 0f,
            LevelData.SafeZoneDoorWidth, LevelData.SafeZoneDoorHeight);
    }

    /// <summary>
    /// One level: a room (50 m unless it says otherwise) with its geometry, enemy placements, tutorial board and enemy
    /// accuracy/fire rate. The player starts in a safe zone behind the room's back wall (x &lt; 0) and leaves it through a door
    /// at the foot of that wall.
    /// </summary>
    public class LevelData
    {
        public const float DefaultLength = 50f;
        public const float Height = 12f;
        /// <summary>Inside width of the safe zone.</summary>
        public const float SafeZoneWidth = 5f;
        /// <summary>Thickness of the wall between the safe zone and the room, which the door is set into.</summary>
        public const float SafeZoneWallThickness = 1f;
        public const float SafeZoneDoorWidth = 0.6f;
        public const float SafeZoneDoorHeight = 2.5f;
        /// <summary>Inner face of the safe zone's back wall. The room itself still starts at x = 0.</summary>
        public const float SafeZoneMinX = -SafeZoneWallThickness - SafeZoneWidth;
        /// <summary>The safe zone door: on the floor, centred in the thickness of the wall above it.</summary>
        public static readonly Rect SafeZoneDoorRect = new Rect(-(SafeZoneWallThickness + SafeZoneDoorWidth) * 0.5f, 0f, SafeZoneDoorWidth, SafeZoneDoorHeight);
        /// <summary>Where a gun to pick up floats in the safe zone: at waist height, between the player's spawn and the door.</summary>
        public static readonly Vector2 SafeZonePickupPoint = new Vector2(SafeZoneMinX + SafeZoneWidth * 0.75f, 0.8f);
        /// <summary>Ground obstacles and ceiling drops are half the player's standing height.</summary>
        public const float LowObstacle = 0.8f;
        public const float LowObstacleWidth = 1.2f;
        /// <summary>Underside of a ceiling drop, where ceiling enemies can hang.</summary>
        public const float DropCeiling = Height - LowObstacle;
        const float PlatformThickness = 0.5f;
        const float PropThickness = 0.4f;

        public readonly int number;
        public readonly LocalizedText title;
        /// <summary>Distance from the safe zone wall (x = 0) to the end wall.</summary>
        public readonly float length;
        public readonly Vector2 fireInterval;
        public readonly float aimNoiseDegrees;
        public readonly List<Block> blocks = new List<Block>();
        public readonly List<EnemySpawn> enemies = new List<EnemySpawn>();
        /// <summary>Board shown while the player is in the safe zone, teaching the controls or a new weapon; null for none.</summary>
        public TutorialBoard tutorial;
        /// <summary>The boss's closed arena at the end of the level; null for none.</summary>
        public BossArena arena;
        public Vector2 playerSpawnFeet = new Vector2(SafeZoneMinX + SafeZoneWidth * 0.5f, 0f);

        /// <summary>Furthest along the level an enemy's random spawn spot can be: short of the end wall, or of the boss arena's wall.</summary>
        public float SpawnMaxX => arena != null ? arena.wallX - 2f : length - 2f;

        /// <param name="title">The level's name in English, and <paramref name="titlePortuguese"/> in Portuguese.</param>
        /// <param name="fireInterval">Seconds between enemy shots, picked uniformly from [x, y].</param>
        /// <param name="aimNoiseDegrees">Std-dev of the noise on enemy ricochets; lower is more accurate.</param>
        /// <param name="length">Distance from the safe zone wall to the end wall.</param>
        public LevelData(int number, string title, string titlePortuguese, Vector2 fireInterval, float aimNoiseDegrees, float length = DefaultLength)
        {
            this.number = number;
            this.title = new LocalizedText(title, titlePortuguese);
            this.fireInterval = fireInterval;
            this.aimNoiseDegrees = aimNoiseDegrees;
            this.length = length;

            // Shell: floor and ceiling over the safe zone and the room, the safe zone's back wall, the end wall,
            // and the wall between the safe zone and the room, standing on the door.
            Solid(SafeZoneMinX - 1f, -1f, length + 1f, 0f, BlockKind.Floor);
            Solid(SafeZoneMinX - 1f, Height, length + 1f, Height + 1f, BlockKind.Ceiling);
            Solid(SafeZoneMinX - 1f, 0f, SafeZoneMinX, Height, BlockKind.Wall);
            Solid(length, 0f, length + 1f, Height, BlockKind.Wall);
            Solid(-SafeZoneWallThickness, SafeZoneDoorHeight, 0f, Height, BlockKind.Partition);
        }

        /// <summary>
        /// Walls off the rest of the level from <paramref name="wallX"/> on as the boss's arena, with a one-way door at the foot
        /// of the wall. The boss's centre stays between <paramref name="bossMinX"/> and <paramref name="bossMaxX"/>, where the floor
        /// and the ceiling above it need to be flat and clear.
        /// </summary>
        public void BossArena(float wallX, float bossMinX, float bossMaxX)
        {
            arena = new BossArena { wallX = wallX, bossRange = new Vector2(bossMinX, bossMaxX) };
            Solid(wallX, SafeZoneDoorHeight, wallX + SafeZoneWallThickness, Height, BlockKind.Partition);
        }

        public void Solid(float xMin, float yMin, float xMax, float yMax, BlockKind kind = BlockKind.Slab) => blocks.Add(new Block
        {
            center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f),
            size = new Vector2(xMax - xMin, yMax - yMin),
            kind = kind,
        });

        /// <summary>Half-height block on the floor: hop over it, or duck behind it.</summary>
        public void LowWall(float xMin) => Solid(xMin, 0f, xMin + LowObstacleWidth, LowObstacle, BlockKind.LowWall);

        /// <summary>The same block hanging from the ceiling.</summary>
        public void CeilingDrop(float xMin) => Solid(xMin, DropCeiling, xMin + LowObstacleWidth, Height, BlockKind.CeilingDrop);

        public void Platform(float xMin, float xMax, float top) => Solid(xMin, top - PlatformThickness, xMax, top, BlockKind.Platform);

        /// <summary>Angled plate to bank shots off. Positive angles rise to the right.</summary>
        public void Prop(float centerX, float centerY, float angle, float length = 2.5f)
        {
            Debug.Assert(Mathf.Abs(angle) >= 30f && Mathf.Abs(angle) <= 60f, $"Level {number}: prop angle {angle}° is outside 30°–60°");
            blocks.Add(new Block { center = new Vector2(centerX, centerY), size = new Vector2(length, PropThickness), angle = angle, kind = BlockKind.Plate });
        }

        public void Grunt(float x, float feetY = 0f, EnemyWeapon weapon = EnemyWeapon.Standard) =>
            AddEnemy(new Vector2(x, feetY), EnemyMount.Floor, weapon, false);

        public void Jumper(float x, float feetY = 0f, EnemyWeapon weapon = EnemyWeapon.Standard) =>
            AddEnemy(new Vector2(x, feetY), EnemyMount.Floor, weapon, true);

        /// <summary>Hangs upside down from the ceiling (or a ceiling drop). Hopping hangers drop down and spring back.</summary>
        public void Hanger(float x, float ceilingY = Height, EnemyWeapon weapon = EnemyWeapon.Standard, bool hops = false) =>
            AddEnemy(new Vector2(x, ceilingY), EnemyMount.Ceiling, weapon, hops);

        public void AddBoss(float x) => enemies.Add(new EnemySpawn { anchor = new Vector2(x, 0f), mount = EnemyMount.Floor, boss = true });

        void AddEnemy(Vector2 anchor, EnemyMount mount, EnemyWeapon weapon, bool hops) =>
            enemies.Add(new EnemySpawn { anchor = anchor, mount = mount, weapon = weapon, hops = hops });
    }

    /// <summary>
    /// The ten levels, each 50 m long (level 10 longer, for its boss arena) and harder than the last:
    /// 1 tutorial · 2 low walls and ceiling drops · 3 more enemies · 4 ceiling hangers and jumpers, and enemies start ducking ·
    /// 5 angled props · 6–7 smarter enemy placement, and the player's twin gun · 8 twin and heavy weapons · 9 more of them · 10 boss, in a closed arena.
    /// </summary>
    public static class Levels
    {
        public const int Count = 10;
        /// <summary>Enemies (but not the boss) can duck from the level that introduces jumpers onwards.</summary>
        public const int FirstDuckingLevel = 4;
        /// <summary>The level whose safe zone holds the twin gun. Every later level starts the player with it.</summary>
        public const int TwinGunLevel = 6;

        const EnemyWeapon Twin = EnemyWeapon.Twin;
        const EnemyWeapon Heavy = EnemyWeapon.Heavy;
        const float Drop = LevelData.DropCeiling;

        /// <summary>Builds a fresh copy of level <paramref name="number"/> (1-based).</summary>
        public static LevelData Create(int number)
        {
            switch (Mathf.Clamp(number, 1, Count))
            {
                case 1: return BootCamp();
                case 2: return LowCover();
                case 3: return Reinforcements();
                case 4: return UpsideDown();
                case 5: return Deflection();
                case 6: return Crossfire();
                case 7: return KillBox();
                case 8: return HeavyArms();
                case 9: return Arsenal();
                default: return TheWarden();
            }
        }

        // Tutorial: flat room, a few ground enemies spaced well apart, and the controls board in the safe zone.
        static LevelData BootCamp()
        {
            var l = new LevelData(1, "Boot Camp", "Campo de Treino", new Vector2(2.6f, 3.8f), 9f);
            l.tutorial = TutorialBoard.Controls();
            l.Grunt(11f);
            l.Grunt(21f);
            l.Grunt(31f);
            l.Grunt(41f);
            return l;
        }

        // Half-height walls on the floor and matching blocks on the ceiling.
        static LevelData LowCover()
        {
            var l = new LevelData(2, "Low Cover", "Cobertura Baixa", new Vector2(2.3f, 3.4f), 8f);
            l.LowWall(7f);
            l.CeilingDrop(9.5f);
            l.Grunt(11.5f);
            l.LowWall(14f);
            l.CeilingDrop(17f);
            l.LowWall(19.5f);
            l.Grunt(21.6f);
            l.CeilingDrop(24f);
            l.LowWall(26f);
            l.Grunt(26.6f, LevelData.LowObstacle); // standing on the wall
            l.CeilingDrop(29.5f);
            l.LowWall(32f);
            l.Grunt(34f);
            l.CeilingDrop(36f);
            l.LowWall(38.5f);
            l.CeilingDrop(41.5f);
            l.LowWall(43f);
            l.Grunt(44.8f);
            return l;
        }

        // Same pieces, nearly twice the enemies, some on ledges and in pairs.
        static LevelData Reinforcements()
        {
            var l = new LevelData(3, "Reinforcements", "Reforços", new Vector2(2.1f, 3.1f), 7.5f);
            l.LowWall(6.5f);
            l.CeilingDrop(8.5f);
            l.Grunt(9.6f);
            l.Grunt(11.4f);
            l.LowWall(12.6f);
            l.Platform(14.5f, 17.5f, 3.4f);
            l.Grunt(16f, 3.4f);
            l.CeilingDrop(15.4f);
            l.LowWall(19f);
            l.Grunt(21.4f);
            l.CeilingDrop(22.5f);
            l.Grunt(24f);
            l.LowWall(25.4f);
            l.Platform(27.5f, 31f, 3.6f);
            l.Grunt(29.2f, 3.6f);
            l.CeilingDrop(28.6f);
            l.LowWall(32f);
            l.Grunt(34.6f);
            l.CeilingDrop(35.6f);
            l.Platform(36.8f, 40.2f, 3.4f);
            l.LowWall(37.9f); // under the ledge
            l.Grunt(38.5f, 3.4f);
            l.LowWall(42.5f);
            l.CeilingDrop(44f);
            l.Grunt(45.2f);
            return l;
        }

        // Enemies hanging from the ceiling, and enemies that hop.
        static LevelData UpsideDown()
        {
            var l = new LevelData(4, "Upside Down", "De Ponta-Cabeça", new Vector2(1.9f, 2.9f), 7f);
            l.LowWall(7f);
            l.Grunt(10.5f);
            l.Hanger(13f);
            l.LowWall(15f);
            l.CeilingDrop(18.4f);
            l.Hanger(19f, Drop);
            l.Jumper(21f);
            l.LowWall(23f);
            l.CeilingDrop(26f);
            l.Hanger(26.6f, Drop, hops: true);
            l.Grunt(28.6f);
            l.LowWall(30.2f);
            l.Platform(33f, 36.5f, 3.4f);
            l.CeilingDrop(33.6f);
            l.Jumper(34.75f, 3.4f);
            l.LowWall(37.5f);
            l.Hanger(40f, hops: true);
            l.CeilingDrop(42.5f);
            l.LowWall(43.5f);
            l.Jumper(45.6f);
            return l;
        }

        // Angled plates (30°–60°) and roofed bunkers that block ceiling shots.
        static LevelData Deflection()
        {
            var l = new LevelData(5, "Deflection", "Desvio", new Vector2(1.8f, 2.8f), 6.5f);
            l.Prop(6f, 5f, 45f);
            l.LowWall(8f);
            l.Grunt(11f);
            l.Prop(13.2f, 6.5f, -35f);
            l.Solid(15f, 2.6f, 18.4f, 3.1f); // bunker roof
            l.Grunt(16.7f);
            l.Prop(20.5f, 4.5f, 60f);
            l.Hanger(22.5f);
            l.CeilingDrop(25f);
            l.Jumper(26.5f);
            l.Platform(28f, 31f, 3.4f);
            l.Grunt(29.5f, 3.4f);
            l.LowWall(31.8f);
            l.Prop(32.8f, 6f, 35f);
            l.Solid(34.2f, 2.6f, 37.6f, 3.1f); // bunker roof
            l.Grunt(35.9f);
            l.CeilingDrop(38.4f);
            l.Hanger(39f, Drop);
            l.Prop(41.5f, 6.5f, -55f);
            l.LowWall(42.6f);
            l.Jumper(44.6f);
            l.Prop(46.2f, 4.4f, 30f);
            l.Grunt(46.6f);
            return l;
        }

        // Same toolset; enemies are placed to cover each other. The safe zone holds the twin gun (see TwinGunLevel) and its board.
        static LevelData Crossfire()
        {
            var l = new LevelData(6, "Crossfire", "Fogo Cruzado", new Vector2(1.7f, 2.6f), 6f);
            l.tutorial = TutorialBoard.TwinGun();
            l.LowWall(6.5f);
            // Stack: floor, ledge and ceiling all open up at once.
            l.Prop(8.5f, 7f, -40f);
            l.Grunt(10f);
            l.Hanger(11f);
            l.Platform(11.4f, 14.2f, 3.4f);
            l.Jumper(12.8f, 3.4f);
            l.LowWall(15.5f);
            // Bunker with a guard overhead: the roof shields the gunner, the hanger watches the approach.
            l.Prop(16.8f, 6f, 40f);
            l.Solid(18f, 2.6f, 21.2f, 3.1f);
            l.Grunt(19.6f);
            l.Hanger(19.6f, hops: true);
            l.LowWall(23.2f);
            // Overwatch: the hanger drops at you while you hop the walls.
            l.CeilingDrop(25.6f);
            l.Hanger(26.2f, Drop, hops: true);
            l.LowWall(27.6f);
            l.Grunt(30.4f);
            l.LowWall(31.6f);
            // Plate gallery: the plates give the gunners angles on you, and you angles on them.
            l.Prop(34f, 5.2f, -45f);
            l.Grunt(36f);
            l.Prop(38f, 5.2f, 45f);
            l.Platform(39.2f, 42.2f, 3.4f);
            l.Jumper(40.7f, 3.4f);
            // Rear guard: up on a high ledge, it keeps shooting after you've passed beneath it.
            l.Platform(43f, 45.6f, 6.6f);
            l.Grunt(44.3f, 6.6f);
            l.Grunt(46.8f);
            return l;
        }

        // Ambushes, mirrored bunkers and a plate funnel under a ledge gunner.
        static LevelData KillBox()
        {
            var l = new LevelData(7, "Kill Box", "Zona de Abate", new Vector2(1.6f, 2.5f), 5.5f);
            // Ambush right out of the gate, with one wall for cover.
            l.LowWall(5.8f);
            l.Grunt(8.6f);
            l.Hanger(9.4f, hops: true);
            // Bunkers facing each other; a jumper on the first roof.
            l.Solid(13f, 2.6f, 16.2f, 3.1f);
            l.Grunt(14.6f);
            l.Jumper(14.6f, 3.1f);
            l.LowWall(17.6f);
            l.Prop(18.2f, 6.5f, -50f);
            l.Solid(20.2f, 2.6f, 23.4f, 3.1f);
            l.Grunt(21.8f);
            // Corridor of ceiling blocks, some of them occupied.
            l.CeilingDrop(25f);
            l.Hanger(25.6f, Drop);
            l.LowWall(26.6f);
            l.CeilingDrop(28.4f);
            l.Grunt(30f);
            l.CeilingDrop(31.8f);
            l.Hanger(32.4f, Drop, hops: true);
            l.LowWall(33.2f);
            // Plate funnel: two plates steer shots into a lane watched by a ledge gunner.
            l.Prop(36f, 4.6f, 30f);
            l.Platform(37f, 39f, 7.6f);
            l.Grunt(38f, 7.6f);
            l.Prop(40f, 4.6f, -30f);
            // Last stand: floor, ledge and ceiling together.
            l.Platform(43.2f, 45.8f, 3.4f);
            l.Grunt(44.5f, 3.4f);
            l.Hanger(45.5f, hops: true);
            l.Grunt(46.6f);
            return l;
        }

        // Upgraded weapons arrive: twin shots (orange) and heavy shots (purple).
        static LevelData HeavyArms()
        {
            var l = new LevelData(8, "Heavy Arms", "Armamento Pesado", new Vector2(1.6f, 2.5f), 5.5f);
            l.LowWall(6.5f);
            l.Grunt(10f, weapon: Twin);
            l.Prop(12.5f, 6f, -40f);
            l.LowWall(13.6f);
            l.Grunt(16.4f);
            l.Hanger(17.4f);
            l.CeilingDrop(19.6f);
            l.Hanger(20.2f, Drop);
            l.Grunt(21.8f, weapon: Heavy);
            l.LowWall(23.4f);
            l.Solid(26f, 2.6f, 29.2f, 3.1f); // bunker
            l.Grunt(27.6f, weapon: Twin);
            l.Jumper(27.6f, 3.1f);
            l.Prop(31f, 5.5f, 50f);
            l.Hanger(32.6f);
            l.LowWall(33.6f);
            l.Platform(36f, 39f, 3.4f);
            l.Jumper(37.5f, 3.4f);
            l.Grunt(40.4f);
            l.CeilingDrop(41.4f);
            l.Hanger(42f, Drop, hops: true);
            l.LowWall(43.4f);
            l.Grunt(46.2f, weapon: Heavy);
            return l;
        }

        // Upgraded weapons everywhere, in every position.
        static LevelData Arsenal()
        {
            var l = new LevelData(9, "Arsenal", "Arsenal", new Vector2(1.5f, 2.3f), 5f);
            l.LowWall(6f);
            l.Grunt(9.6f, weapon: Heavy);
            l.Hanger(10.6f, weapon: Twin);
            l.Prop(12.6f, 6.2f, -45f);
            l.LowWall(13.4f);
            l.Solid(16f, 2.6f, 19.2f, 3.1f); // bunker
            l.Grunt(17.6f, weapon: Twin);
            l.Jumper(17.6f, 3.1f, Heavy);
            l.CeilingDrop(20.8f);
            l.Hanger(21.4f, Drop, Twin, hops: true);
            l.LowWall(22.4f);
            l.Grunt(25.2f);
            l.Prop(27.4f, 5f, 35f);
            l.Platform(28.8f, 31.8f, 3.4f);
            l.Grunt(30.3f, 3.4f, Heavy);
            l.Hanger(30.3f, hops: true);
            l.LowWall(33f);
            l.Jumper(35.8f, weapon: Twin);
            l.Prop(38f, 6.4f, -55f);
            l.Platform(39.4f, 42f, 6.4f);
            l.Grunt(40.7f, 6.4f, Twin);
            l.Grunt(41.2f, weapon: Heavy); // under the high ledge
            l.LowWall(43.4f);
            l.Jumper(46f);
            l.Hanger(46.6f, weapon: Heavy);
            return l;
        }

        // A short guarded approach, then the boss's closed arena, which makes the level longer than the others.
        static LevelData TheWarden()
        {
            const float arenaWall = 28f;
            var l = new LevelData(10, "The Warden", "O Guardião", new Vector2(1.4f, 2.2f), 4.5f, 59f);
            l.LowWall(6.5f);
            l.Grunt(10.4f, weapon: Twin);
            l.CeilingDrop(13f);
            l.Hanger(13.6f, Drop, Heavy);
            l.Prop(16.5f, 5.5f, 45f);
            l.LowWall(18f);
            l.Jumper(21.6f, weapon: Twin);
            l.LowWall(24.6f);
            // Arena, 30 m inside: cover on the near side, then open floor and ceiling where the boss moves.
            l.BossArena(arenaWall, 44f, 55f);
            l.Prop(33.5f, 7.5f, -40f);
            l.LowWall(35.5f);
            l.CeilingDrop(38.4f);
            l.LowWall(40.6f);
            l.AddBoss(49.5f);
            l.Prop(57f, 1.35f, 60f); // turns shots dropping behind the boss into its back
            return l;
        }
    }
}
