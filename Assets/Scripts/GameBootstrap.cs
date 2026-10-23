using UnityEngine;
using UnityEngine.InputSystem;

namespace RicochetPoC
{
    /// <summary>
    /// Builds the whole proof of concept at runtime: level geometry, player, enemies, camera and HUD.
    /// Drop it on an empty GameObject in any scene and press Play.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        struct Block
        {
            public Vector2 center;
            public Vector2 size;
            public float angle;
        }

        static Block Solid(float xMin, float yMin, float xMax, float yMax) => new Block
        {
            center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f),
            size = new Vector2(xMax - xMin, yMax - yMin),
        };

        static Block Tilted(float centerX, float centerY, float width, float height, float angle) => new Block
        {
            center = new Vector2(centerX, centerY),
            size = new Vector2(width, height),
            angle = angle,
        };

        // Room interior spans x 0..80, y 0..12.
        const float LevelWidth = 80f;
        const float LevelHeight = 12f;

        static readonly Block[] Level =
        {
            // Shell: floor, ceiling, back walls.
            Solid(-1f, -1f, LevelWidth + 1f, 0f),
            Solid(-1f, LevelHeight, LevelWidth + 1f, LevelHeight + 1f),
            Solid(-1f, 0f, 0f, LevelHeight),
            Solid(LevelWidth, 0f, LevelWidth + 1f, LevelHeight),

            // Section A
            Solid(9f, 0f, 10.5f, 2.4f),
            Solid(15f, 3.4f, 21f, 3.9f),
            Tilted(5.5f, 8.5f, 3f, 0.4f, 30f),

            // Section B
            Solid(26f, 5f, 27f, LevelHeight),
            Solid(30f, 6f, 36f, 6.5f),
            Solid(40f, 0f, 41f, 2.2f),
            Tilted(37f, 9.5f, 2.5f, 0.4f, -35f),
            Solid(47f, 1.3f, 51f, 3.2f), // duck under it

            // Section C
            Solid(54f, 3.4f, 59f, 3.9f),
            Solid(61f, 0f, 62f, 2.6f),
            Solid(66f, 6.5f, 72f, 7f),
            Tilted(60f, 9.5f, 3f, 0.4f, 20f),
            Solid(74f, 0f, 75f, 2.2f),
        };

        // Where each enemy's feet stand.
        static readonly Vector2[] EnemyFeet =
        {
            new Vector2(13f, 0f),
            new Vector2(19f, 3.9f),
            new Vector2(33.5f, 6.5f),
            new Vector2(44f, 0f),
            new Vector2(56.5f, 3.9f),
            new Vector2(64.5f, 0f),
            new Vector2(70f, 7f),
            new Vector2(77.5f, 0f),
        };

        static readonly Color WallColor = new Color(0.36f, 0.38f, 0.44f);
        static readonly Color BackgroundColor = new Color(0.08f, 0.09f, 0.12f);

        public Vector2 playerSpawnFeet = new Vector2(2.5f, 0f);
        [Tooltip("Briefly draw the path each enemy's aim solver planned when it fires (toggle in game with T).")]
        public bool showEnemyAimTraces = true;

        Transform m_World;
        PlayerController m_Player;
        CameraFollow m_CameraFollow;
        GUIStyle m_Style;
        GUIStyle m_BannerStyle;

        void Start()
        {
            Enemy.ShowAimTraces = showEnemyAimTraces;
            SetupCamera();
            BuildWorld();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.rKey.wasPressedThisFrame)
                Restart();
            if (keyboard.tKey.wasPressedThisFrame)
                Enemy.ShowAimTraces = !Enemy.ShowAimTraces;
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
            cam.orthographicSize = LevelHeight * 0.5f + 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            cam.transform.SetPositionAndRotation(new Vector3(0f, LevelHeight * 0.5f, -10f), Quaternion.identity);

            if (!cam.TryGetComponent(out m_CameraFollow))
                m_CameraFollow = cam.gameObject.AddComponent<CameraFollow>();
            m_CameraFollow.levelMinX = -1f;
            m_CameraFollow.levelMaxX = LevelWidth + 1f;
            m_CameraFollow.height = LevelHeight * 0.5f;
        }

        void BuildWorld()
        {
            m_World = new GameObject("World").transform;

            foreach (var block in Level)
            {
                var sr = Shapes.Box(m_World, "Block", WallColor, block.center, block.size);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, block.angle);
                sr.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            }

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(m_World, false);
            m_Player = playerGo.AddComponent<PlayerController>();
            playerGo.transform.position = playerSpawnFeet + Vector2.up * m_Player.standingSize.y * 0.5f;

            foreach (var feet in EnemyFeet)
            {
                var enemyGo = new GameObject("Enemy");
                enemyGo.transform.SetParent(m_World, false);
                var enemy = enemyGo.AddComponent<Enemy>();
                enemyGo.transform.position = feet + Vector2.up * enemy.size.y * 0.5f;
            }

            Physics2D.SyncTransforms();
            m_CameraFollow.target = playerGo.transform;
            m_CameraFollow.SnapToTarget();
        }

        void Restart()
        {
            // Deactivate first so the player/enemies unregister immediately rather than at end of frame.
            m_World.gameObject.SetActive(false);
            Destroy(m_World.gameObject);
            Projectile.DestroyAll();
            BuildWorld();
        }

        void OnGUI()
        {
            if (m_Style == null)
            {
                m_Style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 40) };
                m_Style.normal.textColor = Color.white;
                m_BannerStyle = new GUIStyle(m_Style) { fontSize = Screen.height / 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            if (m_Player == null)
                return;

            float line = m_Style.fontSize * 1.5f;
            GUI.Label(new Rect(16, 12, Screen.width, line),
                $"HP {Mathf.Max(0, m_Player.Health)}/{m_Player.maxHealth}     Enemies left {Enemy.All.Count}/{EnemyFeet.Length}", m_Style);
            GUI.Label(new Rect(16, 12 + line, Screen.width, line),
                "A/D move · W jump · S duck · mouse aim · LMB shoot · T aim traces · R restart", m_Style);
            GUI.Label(new Rect(16, 12 + line * 2f, Screen.width, line),
                "Enemies only die to shots that ricocheted at least once.", m_Style);

            string banner = m_Player.IsDead ? "YOU DIED — press R" : Enemy.All.Count == 0 ? "AREA CLEAR — press R" : null;
            if (banner != null)
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), banner, m_BannerStyle);
        }
    }
}
