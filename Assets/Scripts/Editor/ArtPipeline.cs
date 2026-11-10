using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RicochetPoC.EditorTools
{
    /// <summary>
    /// Import settings for everything under Assets/Art and Assets/Audio, and the builder that turns those files into
    /// the game's Animator controllers and its <see cref="ArtLibrary"/> (Ricochet ▸ Build Art Library).
    ///
    /// Assets/Art/art_slices.json describes each image: its frame size (a strip of frames is sliced left to right),
    /// pivot (0–1 from the bottom-left), frame rate, 9-slice border and mesh type. Every sprite is Point-filtered,
    /// uncompressed, at <see cref="ArtLibrary.PixelsPerUnit"/>.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Art/";
        const string SlicesPath = "Assets/Art/art_slices.json";

        [Serializable]
        internal class SliceEntry
        {
            public int[] frame;
            public float[] pivot;
            public float fps;
            public int[] border;
            public string mesh;
            /// <summary>Character strips: per frame, where the hands are (x, y pairs in px from the feet pivot, facing right).</summary>
            public float[] hands;
            /// <summary>
            /// Sheets that store each distinct frame once (the tutorial animations): the order to play them in, by index,
            /// repeats included. Without it the frames play once each, in sheet order.
            /// </summary>
            public int[] sequence;
        }

        internal static int[] SequenceOf(string assetPath)
        {
            string key = KeyOf(assetPath);
            return key != null && Slices.TryGetValue(key, out var e) && e.sequence != null && e.sequence.Length > 0 ? e.sequence : null;
        }

        internal static float[] HandsOf(string assetPath)
        {
            string key = KeyOf(assetPath);
            return key != null && Slices.TryGetValue(key, out var e) ? e.hands : null;
        }

        static Dictionary<string, SliceEntry> s_Slices;

        internal static Dictionary<string, SliceEntry> Slices
        {
            get
            {
                if (s_Slices == null)
                    s_Slices = LoadSlices();
                return s_Slices;
            }
        }

        internal static void ReloadSlices() => s_Slices = null;

        /// <summary>The JSON is a flat object of path → entry, which JsonUtility can't read directly; parse it entry by entry.</summary>
        static Dictionary<string, SliceEntry> LoadSlices()
        {
            var result = new Dictionary<string, SliceEntry>();
            if (!File.Exists(SlicesPath))
                return result;
            string json = File.ReadAllText(SlicesPath);
            int i = json.IndexOf('{') + 1;
            while (true)
            {
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0)
                    break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);
                int objStart = json.IndexOf('{', keyEnd);
                int depth = 0, objEnd = objStart;
                for (; objEnd < json.Length; objEnd++)
                {
                    if (json[objEnd] == '{') depth++;
                    else if (json[objEnd] == '}' && --depth == 0) break;
                }
                result[key] = JsonUtility.FromJson<SliceEntry>(json.Substring(objStart, objEnd - objStart + 1));
                i = objEnd + 1;
            }
            return result;
        }

        internal static float FpsOf(string assetPath)
        {
            string key = KeyOf(assetPath);
            return key != null && Slices.TryGetValue(key, out var e) && e.fps > 0f ? e.fps : 10f;
        }

        static string KeyOf(string assetPath) =>
            assetPath.StartsWith(ArtRoot) ? Path.ChangeExtension(assetPath.Substring(ArtRoot.Length), null).Replace('\\', '/') : null;

        void OnPreprocessTexture()
        {
            string key = KeyOf(assetPath);
            if (key == null)
                return;
            var importer = (TextureImporter)assetImporter;
            Slices.TryGetValue(key, out var entry);

            bool cursor = key.EndsWith("ui_crosshair_2x");
            importer.textureType = cursor ? TextureImporterType.Cursor : TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (cursor)
                return;
            importer.spritePixelsPerUnit = ArtLibrary.PixelsPerUnit;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Vector2 pivot = entry?.pivot != null && entry.pivot.Length == 2 ? new Vector2(entry.pivot[0], entry.pivot[1]) : new Vector2(0.5f, 0.5f);
            Vector4 border = entry?.border != null && entry.border.Length == 4 ? new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]) : Vector4.zero;
            bool fullRect = entry?.mesh == "FullRect" || border != Vector4.zero;
            int fw = entry?.frame != null && entry.frame.Length == 2 ? entry.frame[0] : width;
            int fh = entry?.frame != null && entry.frame.Length == 2 ? entry.frame[1] : height;
            bool sheet = fw < width || fh < height;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)(sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteBorder = border;
            settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);

            if (!sheet)
                return;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var rects = new List<SpriteRect>();
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            int index = 0;
            for (int y = height - fh; y >= 0; y -= fh)
            for (int x = 0; x + fw <= width; x += fw)
            {
                string name = $"{baseName}_{index++}";
                rects.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(x, y, fw, fh),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = border,
                    spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate(),
                });
            }
            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/"))
                return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
            importer.loadInBackground = music;
        }
    }

    /// <summary>Builds the Animator controllers and the art library from the files in Assets/Art and Assets/Audio.</summary>
    public static class ArtPipeline
    {
        const string LibraryPath = "Assets/Resources/ArtLibrary.asset";
        const string AnimDir = "Assets/Art/Animation";
        const string CharDir = "Assets/Art/Characters/";

        /// <summary>One Animator state: the strip it plays, whether it loops, and the frames a foot lands on (walk and run).</summary>
        struct StateSpec
        {
            public string state, strip;
            public bool loop;
            public int[] steps;
            public int running;

            public StateSpec(string state, string strip, bool loop, int[] steps = null, int running = 0)
            {
                this.state = state;
                this.strip = strip;
                this.loop = loop;
                this.steps = steps;
                this.running = running;
            }
        }

        static readonly StateSpec[] States =
        {
            new StateSpec("Idle", "idle", true),
            new StateSpec("Walk", "walk", true, new[] { 0, 3 }, 0),
            new StateSpec("Run", "run", true, new[] { 1, 4 }, 1),
            new StateSpec("Rise", "rise", false),
            new StateSpec("Fall", "fall", true),
            new StateSpec("Land", "land", false),
            new StateSpec("Duck", "duck", false),
            new StateSpec("DuckWalk", "duckwalk", true),
            new StateSpec("Dead", "death", false),
        };

        [MenuItem("Ricochet/Build Art Library")]
        public static void Build()
        {
            EnsureSortingLayers();
            ArtImport.ReloadSlices();
            if (AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.IsValidFolder("Assets/Audio"))
                AssetDatabase.ImportAsset("Assets/Audio", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            EnsureFolder("Assets/Resources");
            EnsureFolder(AnimDir);

            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            s_HandSprites.Clear();
            s_HandOffsets.Clear();
            lib.player = BuildCharacter("char_player");
            lib.enemyStandard = BuildCharacter("char_enemy_red");
            lib.enemyTwin = BuildCharacter("char_enemy_orange");
            lib.enemyHeavy = BuildCharacter("char_enemy_purple");
            lib.boss = BuildCharacter("char_boss");
            lib.handSprites = s_HandSprites.ToArray();
            lib.handOffsets = s_HandOffsets.ToArray();
            lib.bossCrackHead = Single(CharDir + "char_boss_crack_head.png");
            lib.bossCrackTorso = Single(CharDir + "char_boss_crack_torso.png");

            lib.backgroundWall = Single("Assets/Art/Background/bg_wall.png");
            const string obj = "Assets/Art/Objects/";
            lib.floor = Single(obj + "obj_floor.png");
            lib.ceiling = Single(obj + "obj_ceiling.png");
            lib.wallConcrete = Single(obj + "obj_wall_concrete.png");
            lib.wallPartition = Single(obj + "obj_wall_partition.png");
            lib.labBench = Single(obj + "obj_lab_bench.png");
            lib.ceilingDuct = Single(obj + "obj_ceiling_duct.png");
            lib.pipe = Single(obj + "obj_pipe.png");
            lib.pipeHazard = Single(obj + "obj_pipe_hazard.png");
            lib.hangerRod = Single(obj + "obj_hanger_rod.png");
            lib.pipeClamp = Single(obj + "obj_pipe_clamp.png");
            lib.lightPanel = Single(obj + "obj_light_panel.png");
            lib.cable = Single(obj + "obj_cable.png");
            lib.cableSnapped = Single(obj + "obj_cable_snapped.png");
            lib.doorSafeZone = Single(obj + "obj_door_safezone.png");
            lib.doorArena = Single(obj + "obj_door_arena.png");
            lib.doorExit = Single(obj + "obj_door_exit.png");
            lib.exitSign = Single(obj + "obj_exit_sign.png");
            lib.doorLed = Single(obj + "obj_door_led.png");
            var plates = new List<(int angle, Sprite sprite)>();
            const string platePrefix = "obj_light_panel_";
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite " + platePrefix, new[] { obj + "Plates" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!name.StartsWith(platePrefix))
                    continue;
                string suffix = name.Substring(platePrefix.Length);
                if (int.TryParse(suffix.Substring(1), out int angle))
                    plates.Add((suffix[0] == 'm' ? -angle : angle, Single(path)));
            }
            plates.Sort((a, b) => a.angle.CompareTo(b.angle));
            lib.plateAngles = plates.Select(p => p.angle).ToArray();
            lib.plateRotations = plates.Select(p => p.sprite).ToArray();

            const string wpn = "Assets/Art/Weapons/";
            lib.pistolPlayer = Single(wpn + "wpn_pistol_player.png");
            lib.twinPlayer = Single(wpn + "wpn_twin_player.png");
            lib.twinPickup = Single(wpn + "wpn_twin_pickup.png");
            lib.reloadPistol = Book(wpn + "wpn_pistol_player_reload.png");
            lib.reloadTwin = Book(wpn + "wpn_twin_player_reload.png");
            lib.standardEnemy = Single(wpn + "wpn_standard_enemy.png");
            lib.twinEnemy = Single(wpn + "wpn_twin_enemy.png");
            lib.heavyEnemy = Single(wpn + "wpn_heavy_enemy.png");
            lib.standardBoss = Single(wpn + "wpn_standard_boss.png");
            lib.twinBoss = Single(wpn + "wpn_twin_boss.png");
            lib.heavyBoss = Single(wpn + "wpn_heavy_boss.png");

            const string fx = "Assets/Art/FX/";
            lib.paintballFresh = Single(fx + "fx_paintball_fresh.png");
            lib.paintballLive = Single(fx + "fx_paintball_live.png");
            lib.paintballLiveGlow = Single(fx + "fx_paintball_live_glow.png");
            lib.paintballEnemy = Single(fx + "fx_paintball_enemy.png");
            lib.paintballHeavy = Single(fx + "fx_paintball_heavy.png");
            lib.paintballEnemyLive = Single(fx + "fx_paintball_enemy_live.png");
            lib.paintballEnemyLiveGlow = Single(fx + "fx_paintball_enemy_live_glow.png");
            lib.paintballHeavyLive = Single(fx + "fx_paintball_heavy_live.png");
            lib.paintballHeavyLiveGlow = Single(fx + "fx_paintball_heavy_live_glow.png");
            lib.splatBlue = Strip(fx + "fx_splat_blue.png");
            lib.splatOrange = Strip(fx + "fx_splat_orange.png");
            lib.splatMagenta = Strip(fx + "fx_splat_magenta.png");
            lib.splatPurple = Strip(fx + "fx_splat_purple.png");
            lib.armFlash = Book(fx + "fx_arm_flash.png");
            lib.armFlashRed = Book(fx + "fx_arm_flash_red.png");
            lib.bounceSpark = Book(fx + "fx_bounce_spark.png");
            lib.blockedSplat = Book(fx + "fx_blocked_splat.png");
            lib.hitMagenta = Book(fx + "fx_hit_magenta.png");
            lib.hitPurple = Book(fx + "fx_hit_purple.png");
            lib.burstPaint = Book(fx + "fx_elim_burst_green.png");
            lib.pickupGlow = Single(fx + "fx_pickup_glow.png");

            const string ui = "Assets/Art/UI/";
            lib.ammoFull = Tex(ui + "ui_ammo_full.png");
            lib.ammoEmpty = Tex(ui + "ui_ammo_empty.png");
            lib.heartFull = Tex(ui + "ui_heart_full.png");
            lib.heartEmpty = Tex(ui + "ui_heart_empty.png");
            lib.bounceIcon = Single(ui + "ui_bounce_icon.png");
            lib.laser = Single(ui + "ui_laser.png");
            lib.crosshair = Tex(ui + "ui_crosshair_2x.png");
            lib.buttonNormal = Tex(ui + "ui_button_normal_2x.png");
            lib.buttonHover = Tex(ui + "ui_button_hover_2x.png");
            lib.buttonPressed = Tex(ui + "ui_button_pressed_2x.png");
            lib.buttonGhost = Tex(ui + "ui_button_ghost_2x.png");

            const string tut = ui + "Tutorial/tut_";
            lib.tutorial = new TutorialArt
            {
                walk = Book(tut + "walk.png"),
                sprint = Book(tut + "sprint.png"),
                jump = Book(tut + "jump.png"),
                duck = Book(tut + "duck.png"),
                aim = Book(tut + "aim.png"),
                shoot = Book(tut + "shoot.png"),
                pistol = Book(tut + "pistol.png"),
                magazine = Book(tut + "magazine.png"),
                ricochet = Book(tut + "ricochet.png"),
                twinGun = Book(tut + "twin_gun.png"),
                switchGuns = Book(tut + "switch_guns.png"),
                twinShot = Book(tut + "twin_shot.png"),
                topBarrel = Book(tut + "top_barrel.png"),
            };

            const string sfx = "Assets/Audio/SFX/";
            lib.shotPlayer = Clip(sfx + "sfx_shot_player.wav");
            lib.shotEnemy = Clip(sfx + "sfx_shot_enemy.wav");
            lib.shotHeavy = Clip(sfx + "sfx_shot_heavy.wav");
            lib.ricochetArmed = Clip(sfx + "sfx_ricochet_armed.wav");
            lib.ricochet = Clip(sfx + "sfx_ricochet.wav");
            lib.splatHarmless = Clip(sfx + "sfx_splat_harmless.wav");
            lib.elimination = Clip(sfx + "sfx_elimination.wav");
            lib.playerHit = Clip(sfx + "sfx_player_hit.wav");
            lib.footstepsWalk = Clips(sfx, "sfx_step_walk_");
            lib.footstepsRun = Clips(sfx, "sfx_step_run_");
            lib.jump = Clip(sfx + "sfx_jump.wav");
            lib.land = Clip(sfx + "sfx_land.wav");
            lib.crouch = Clip(sfx + "sfx_crouch.wav");
            lib.uiClick = Clip(sfx + "sfx_ui_click.wav");
            lib.roundStart = Clip(sfx + "sfx_round_start.wav");
            lib.roundClear = Clip(sfx + "sfx_round_clear.wav");
            lib.roundLost = Clip(sfx + "sfx_round_lost.wav");
            lib.gunSwitch = Clip(sfx + "sfx_gun_switch.wav");
            lib.reload = Clip(sfx + "sfx_reload.wav");
            lib.musicMenu = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/music_menu.ogg");
            lib.musicGameplay = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/music_gameplay.ogg");

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtPipeline] Art library built: {Report(lib)}");
        }

        static string Report(ArtLibrary lib)
        {
            var missing = new List<string>();
            foreach (var field in typeof(ArtLibrary).GetFields())
            {
                object v = field.GetValue(lib);
                bool empty = v == null || (v is UnityEngine.Object o && o == null) || (v is Array a && a.Length == 0)
                             || (v is CharacterArt c && !c.IsValid) || (v is Flipbook b && !b.IsValid)
                             || (v is TutorialArt t && !t.IsComplete);
                if (empty)
                    missing.Add(field.Name);
            }
            return missing.Count == 0 ? "everything assigned" : "missing: " + string.Join(", ", missing);
        }

        // ---- characters

        static readonly List<Sprite> s_HandSprites = new List<Sprite>();
        static readonly List<Vector2> s_HandOffsets = new List<Vector2>();

        static CharacterArt BuildCharacter(string prefix)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var spec in States)
            {
                string path = CharDir + prefix + "_" + spec.strip + "_right.png";
                var frames = Strip(path);
                if (frames.Length == 0)
                    continue;
                clips[spec.state] = BuildClip(prefix, spec, frames, ArtImport.FpsOf(path));
                // Where the hands are on each frame, so the gun's grip can follow them.
                var hands = ArtImport.HandsOf(path);
                if (hands != null)
                    for (int i = 0; i < frames.Length && i * 2 + 1 < hands.Length; i++)
                    {
                        s_HandSprites.Add(frames[i]);
                        s_HandOffsets.Add(new Vector2(hands[i * 2], hands[i * 2 + 1]) / ArtLibrary.PixelsPerUnit);
                    }
            }
            if (!clips.ContainsKey("Idle"))
                return new CharacterArt();

            string controllerPath = $"{AnimDir}/{prefix}.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var p in new[] { "Speed", "VelocityY" })
                controller.AddParameter(p, AnimatorControllerParameterType.Float);
            foreach (var p in new[] { "Sprint", "Grounded", "Ducking", "Landing", "Dead" })
                controller.AddParameter(p, AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            foreach (var kv in clips)
            {
                var state = machine.AddState(kv.Key);
                state.motion = kv.Value;
                states[kv.Key] = state;
            }
            machine.defaultState = states["Idle"];

            // Every state is reached from Any State on mutually exclusive conditions, so the controller always matches
            // what the gameplay code reports, whatever state it was in before.
            AnimatorState Pick(params string[] names) => names.Where(states.ContainsKey).Select(n => states[n]).FirstOrDefault();
            void To(AnimatorState target, params (AnimatorConditionMode mode, float value, string param)[] conditions)
            {
                if (target == null)
                    return;
                var t = machine.AddAnyStateTransition(target);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                foreach (var c in conditions)
                    t.AddCondition(c.mode, c.value, c.param);
            }
            const AnimatorConditionMode If = AnimatorConditionMode.If, Not = AnimatorConditionMode.IfNot;
            const AnimatorConditionMode More = AnimatorConditionMode.Greater, Less = AnimatorConditionMode.Less;
            var alive = (Not, 0f, "Dead");
            To(Pick("Dead"), (If, 0f, "Dead"));
            To(Pick("Rise", "Fall", "Idle"), alive, (Not, 0f, "Grounded"), (More, 0.1f, "VelocityY"));
            To(Pick("Fall", "Rise", "Idle"), alive, (Not, 0f, "Grounded"), (Less, 0.1f, "VelocityY"));
            To(Pick("Land", "Idle"), alive, (If, 0f, "Grounded"), (If, 0f, "Landing"), (Not, 0f, "Ducking"));
            To(Pick("Duck", "Idle"), alive, (If, 0f, "Grounded"), (If, 0f, "Ducking"), (Less, 0.1f, "Speed"));
            To(Pick("DuckWalk", "Duck", "Idle"), alive, (If, 0f, "Grounded"), (If, 0f, "Ducking"), (More, 0.1f, "Speed"));
            To(Pick("Idle"), alive, (If, 0f, "Grounded"), (Not, 0f, "Ducking"), (Not, 0f, "Landing"), (Less, 0.1f, "Speed"));
            To(Pick("Walk", "Run", "Idle"), alive, (If, 0f, "Grounded"), (Not, 0f, "Ducking"), (Not, 0f, "Landing"), (More, 0.1f, "Speed"), (Not, 0f, "Sprint"));
            To(Pick("Run", "Walk", "Idle"), alive, (If, 0f, "Grounded"), (Not, 0f, "Ducking"), (Not, 0f, "Landing"), (More, 0.1f, "Speed"), (If, 0f, "Sprint"));

            EditorUtility.SetDirty(controller);
            var idleFrames = Strip(CharDir + prefix + "_idle_right.png");
            return new CharacterArt { controller = controller, idle = idleFrames[0] };
        }

        static AnimationClip BuildClip(string prefix, StateSpec spec, Sprite[] frames, float fps)
        {
            string path = $"{AnimDir}/{prefix}_{spec.strip}.anim";
            AssetDatabase.DeleteAsset(path);
            var clip = new AnimationClip { frameRate = fps, name = $"{prefix}_{spec.strip}" };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            // One key per frame; Unity gives the last key a full frame of its own (clip length = frames / fps).
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (spec.steps != null)
            {
                var events = spec.steps.Where(f => f < frames.Length).Select(f => new AnimationEvent
                {
                    time = f / fps,
                    functionName = nameof(CharacterVisual.Footstep),
                    intParameter = spec.running,
                }).ToArray();
                AnimationUtility.SetAnimationEvents(clip, events);
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---- loading helpers

        static Sprite Single(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).FirstOrDefault();

        static Sprite[] Strip(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => -s.rect.y).ThenBy(s => s.rect.x).ToArray();

        static Flipbook Book(string path)
        {
            var frames = Strip(path);
            var sequence = ArtImport.SequenceOf(path);
            if (sequence != null && frames.Length > 0)
                frames = sequence.Select(i => frames[Mathf.Clamp(i, 0, frames.Length - 1)]).ToArray();
            return new Flipbook { frames = frames, fps = ArtImport.FpsOf(path) };
        }

        static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        static AudioClip[] Clips(string folder, string prefix)
        {
            if (!AssetDatabase.IsValidFolder(folder.TrimEnd('/')))
                return new AudioClip[0];
            return AssetDatabase.FindAssets(prefix + " t:AudioClip", new[] { folder.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p).StartsWith(prefix))
                .OrderBy(p => p)
                .Select(Clip)
                .ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---- sorting layers

        /// <summary>Background (reserved, behind everything) · Default · Objects · Characters · ProjectilesFX · UI.</summary>
        [MenuItem("Ricochet/Add Sorting Layers")]
        public static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            string[] wanted = { SortLayers.Background, "Default", SortLayers.Objects, SortLayers.Characters, SortLayers.Effects, SortLayers.UI };
            var existing = new List<string>();
            for (int i = 0; i < layers.arraySize; i++)
                existing.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
            var positions = wanted.Select(n => existing.IndexOf(n)).ToList();
            if (positions.All(p => p >= 0) && positions.SequenceEqual(positions.OrderBy(p => p)))
            {
                LightAllSortingLayers();
                return;
            }

            // Rebuild the list in the wanted order, keeping the IDs of layers that already exist.
            var ids = new Dictionary<string, long>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                ids[e.FindPropertyRelative("name").stringValue] = e.FindPropertyRelative("uniqueID").longValue;
            }
            var order = wanted.Concat(existing.Where(n => !wanted.Contains(n))).ToList();
            layers.arraySize = order.Count;
            var random = new System.Random();
            for (int i = 0; i < order.Count; i++)
            {
                var e = layers.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = order[i];
                e.FindPropertyRelative("uniqueID").longValue = ids.TryGetValue(order[i], out long id) ? id : (order[i] == "Default" ? 0 : random.Next(1, int.MaxValue));
                e.FindPropertyRelative("locked").boolValue = false;
            }
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtPipeline] Sorting layers: " + string.Join(" · ", order));
            LightAllSortingLayers();
        }

        /// <summary>
        /// A 2D light only lights the sorting layers it targets, and a light made before a layer existed doesn't target it,
        /// so sprites on that layer would render black. Points every 2D light in the open scenes at every sorting layer, and saves.
        /// </summary>
        static void LightAllSortingLayers()
        {
            int[] all = SortingLayer.layers.Select(l => l.id).ToArray();
            foreach (var light in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(light);
                var prop = so.FindProperty("m_ApplyToSortingLayers");
                if (prop == null)
                    continue;
                bool same = prop.arraySize == all.Length && Enumerable.Range(0, all.Length).All(i => prop.GetArrayElementAtIndex(i).intValue == all[i]);
                if (same)
                    continue;
                prop.arraySize = all.Length;
                for (int i = 0; i < all.Length; i++)
                    prop.GetArrayElementAtIndex(i).intValue = all[i];
                so.ApplyModifiedProperties();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(light.gameObject.scene);
                Debug.Log($"[ArtPipeline] {light.name} now lights all {all.Length} sorting layers.");
            }
        }
    }
}
