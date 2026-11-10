# Asset Manifest

Inventory of everything in the game that needs art or audio, and the status of each asset. See STYLE_GUIDE.md for the
look, palette and prompt descriptors, CREDITS.md for sources and licenses, and Tools/ArtGen for the generator scripts.

## How the project works

- **No prefabs.** `GameBootstrap` builds every level at runtime from `Levels.cs`. Before this pass every visible object
  was a white square (`Shapes.Square`/`Circle`/`Ring`) tinted with `SpriteRenderer.color` and stretched with
  `transform.localScale`, with `BoxCollider2D` size 1. There were no Animators, AudioSources or imported sprites.
- **Now:** `Assets/Resources/ArtLibrary.asset` (an `ArtLibrary`, read through `Art.Lib`) holds every sprite, Animator
  controller, effect and sound. The runtime builders read it, and each falls back to its old placeholder if something is
  missing, so the game always runs.
- **Colliders are unchanged in world size.** Level blocks now carry their size in `BoxCollider2D.size` at scale 1
  instead of in the transform scale. That is the same geometry, expressed so the art can render at its own pixel density.
  Character and boss hitboxes are untouched: the old scaled boxes stay as hidden hitbox frames, because boss armour-region
  math reads from them, and the art sits on an unscaled child at the feet.
- **Pixels Per Unit: 30 for every sprite.** The camera shows 14 m vertically at 1680×840, so 60 screen px per metre, and
  each art pixel is exactly 2×2 screen pixels.
- **Sorting layers:** Background (the back wall) · Default · Objects · Characters ·
  ProjectilesFX · UI. The scene's Global Light 2D lights all of them; the pipeline sets that whenever it adds a layer.
- **Rebuild:** after changing files in Assets/Art or Assets/Audio, run **Ricochet ▸ Build Art Library**. It applies
  import settings (Sprite, Point, uncompressed, PPU 30, pivots, 9-slice borders, strip slicing from
  `Assets/Art/art_slices.json`), rebuilds the Animator controllers and clips, and refills the library.

### Animator parameters (shared by every character controller)

`Speed` (float, horizontal m/s) · `VelocityY` (float) · `Sprint` (bool) · `Grounded` (bool) · `Ducking` (bool) ·
`Landing` (bool, 0.12 s after touching down) · `Dead` (bool). Every state is entered from Any State on mutually
exclusive conditions, so the animation always matches what the gameplay code reports. Walk and run clips carry
`Footstep` animation events on the foot-contact frames (walk 0 and 3, run 1 and 4), which play the walk or run footstep sets.

## Inventory and status

World size → pixel size at PPU 30. "Body" is the opaque figure; character frames sit on a wider transparent canvas so
limbs can swing, with the pivot at the feet (bottom centre).

### Characters

| Object | Collider (m) | Body (px) | Frame canvas | States (Animator) | Files (Assets/Art/Characters) | Status |
|---|---|---|---|---|---|---|
| Player | 0.8×1.6, ducking 0.8×0.96 | ≤24×48, ducking 29 tall | 56×48 | Idle, Walk, Run (sprint), Rise, Fall, Land, Duck, DuckWalk, Dead | `char_player_{idle,walk,run,rise,fall,land,duck,duckwalk,death}_right.png` | done |
| Enemy Standard (red) | 0.8×1.6, ducking 0.8×0.96 | 48 tall, ducking 29 | 56×48 | Idle, Rise/Fall (hop), Duck, Dead. No walk or run: these enemies never move sideways (confirmed by the user). Hangers are the same art rotated 180° by the game. | `char_enemy_red_*_right.png` | done |
| Enemy Twin (orange) | same | same | same | same | `char_enemy_orange_*_right.png` (recolour of red) | done |
| Enemy Heavy (purple) | same | same | same | same | `char_enemy_purple_*_right.png` (recolour of red) | done |
| Boss "The Warden" | 1.4×2.8, ducking 1.4×1.68 | ≤84 tall, ducking 50 | 96×84 | Idle, Walk, Run (dodge dash), Rise/Fall (jump and ceiling leap), Duck, Dead. Black armour while invulnerable is a runtime tint; cracked regions show `char_boss_crack_{head,torso}.png` overlays. | `char_boss_{idle,walk,run,rise,fall,duck,death}_right.png` | done |

All characters face right and are mirrored with `flipX` for left. The player faces the mouse, and enemies and the boss
face the player.

### Weapons (Assets/Art/Weapons, pivot = back end of the barrel axis, mirrored vertically when aiming left)

| File | Size (px) | Matches |
|---|---|---|
| wpn_pistol_player / wpn_twin_player | 26×9 / 26×11 | 0.85 m barrel (projectile spawns at 0.85 m) |
| wpn_standard_enemy (red), wpn_twin_enemy (orange), wpn_heavy_enemy (purple) | 23×9 / 23×11 / 23×13 | 0.75 m barrel |
| wpn_standard_boss, wpn_twin_boss, wpn_heavy_boss (yellow) | 30×9 / 30×11 / 30×13 | 1.0 m barrel |

### Interactable objects (all ricochet surfaces), modern style (the user's direction)

| Object (theme) | Code (`BlockKind`) | Size (m) | Sprite (px) | Draw mode | Status |
|---|---|---|---|---|---|
| Polished concrete floor | Floor | (length+8)×1 | obj_floor 60×30 | Tiled | done |
| Graphite acoustic ceiling with LED strips | Ceiling | (length+8)×1 | obj_ceiling 60×30 | Tiled | done |
| Exposed concrete wall | Wall | 1×12 | obj_wall_concrete 30×60 | Tiled | done |
| White laminate partition (safe-zone and arena walls) | Partition | 1×9.5 | obj_wall_partition 30×60 | Tiled | done |
| Modern lab bench (white laminate, graphite epoxy top) | LowWall | 1.2×0.8 | obj_lab_bench 36×24 | exact | done |
| Suspended duct bulkhead with linear LED | CeilingDrop | 1.2×0.8 | obj_ceiling_duct 36×24 | exact | done |
| Steel mezzanine ledge with LED underside | Platform | 2.0–3.5×0.5 | obj_ledge 30×15, 9-slice 3 px caps | Tiled (caps kept) | done |
| Concrete slab with hazard edge (bunker roofs) | Slab | 3.2–3.4×0.5 | obj_bunker_roof 30×15, 9-slice | Tiled (caps kept) | done |
| Aluminium-framed whiteboard | Plate | 2.5×0.4 at ±30–60° | obj_whiteboard 75×12 + Plates/obj_whiteboard_{p,m}{30..60} pre-rotated (14 angles) | exact, drawn level while the collider keeps its angle | done |
| Frosted-glass safe-zone door + status LED | one-way door | 0.6×2.5 | obj_door_safezone 18×75 + obj_door_led (tinted green/red) | exact | done |
| Steel security arena door + status LED | one-way door | 0.6×2.5 | obj_door_arena 18×75 + obj_door_led | exact | done |
| Glass exit doors + EXIT sign (decorative) | exit door | 1.2×2.2 | obj_door_exit 36×66 + obj_exit_sign 21×9 (tinted red/green) | exact | done |

The back wall is decoration, not a ricochet surface: no collider, on the Background layer, built by
`GameBootstrap.BuildBackWall` across the floor's span.

| Object (theme) | Size (m) | Sprite (px) | Draw mode | Status |
|---|---|---|---|---|
| Two-storey back wall: soffit, laminate, frosted-glass bays with soft centre glow, warm LED strips | (length+8)×12 | Background/bg_wall 52×360 (Full Rect) | Tiled | done |

### Projectiles and FX (Assets/Art/FX)

| Asset | Size (px) | Used for | Status |
|---|---|---|---|
| fx_paintball_fresh | 7×7 (⌀0.24 m) | Player shot before any bounce: matte orange, harmless | done |
| fx_paintball_live + fx_paintball_live_glow | 7×7 + 15×15 | Player shot after its first bounce: cyan, white-hot core, pulsing halo, after-image trail | done |
| fx_paintball_enemy / fx_paintball_heavy | 8×8 / 19×19 | Enemy shots (magenta), heavy shots (purple) | done |
| fx_splat_{blue,orange,magenta,purple} | 3 variants of 18×18 | Decals left where a shot ends on a surface, in its paint | done |
| fx_blocked_splat | 22×18, 8 frames | **Green** splash when a paintball hits an enemy without eliminating it (the user's change) | done |
| fx_hit_{magenta,purple} | 22×18, 8 frames | Enemy paint splashing on the player | done |
| fx_arm_flash | 22×20, 5 frames | Cyan flash where a player shot bounces and turns live | done |
| fx_bounce_spark | 18×16, 4 frames | Later bounces (any shot) | done |
| fx_elim_burst_{red,orange,purple,boss,blue} | 45×39, 8 frames | Elimination burst per enemy type | done |
| fx_pickup_glow | 33×33 | Twin-gun pickup halo (tinted gold) | done |

### UI (Assets/Art/UI)

| Asset | Where | Status |
|---|---|---|
| ui_laser (6×1 dash, tiled) | LaserSight beam | done |
| ui_bounce_icon (15×13, tinted cyan) | Bounce indicator at the laser's first ricochet: where the shot becomes lethal | done |
| ui_ammo_full / ui_ammo_empty (14×14, drawn ×2) | Magazine pips | done |
| ui_button_{normal,hover,pressed,ghost}_2x (48×24, 9-slice) | Menu buttons (`GuiKit.Button`) | done |
| ui_crosshair_2x (30×30, hardware cursor) | Cursor while playing | done |

### Tutorial animations (Assets/Art/UI/Tutorial)

One looping animation per tutorial card, 100×75 px at 20 fps, drawn 1:1 into the card's picture slot
(`ArtLibrary.tutorial`, played by `TutorialBoard`). `Tools/ArtGen/tutorial_gifs.py` composes them from the game's own
sprites: character strips and hand points, guns at the grip, the laser sight, shots turning live on their first bounce,
the effects and the HUD magazine. Shots are slowed and the jump lowered so they read in a 3 m frame. A small keycap or mouse
in a corner lights up in the board's accent colour while its input is held. Each sheet stores its distinct frames once, and
the play order is the `sequence` in art_slices.json. 2× GIF copies of every loop are in `TutorialGifs/` at the project root.

| Sheet | Card | Shows | Status |
|---|---|---|---|
| tut_walk | Move (A/D) | Walks right, turns, walks back | done |
| tut_sprint | Sprint (Shift) | Walks in, then runs off faster | done |
| tut_jump | Jump (W) | Jumps a live enemy shot skimming in low | done |
| tut_duck | Duck (S) | Ducks behind a lab bench under a live shot at head height | done |
| tut_aim | Aim (mouse) | The gun follows the cursor; the laser bounces off the floor or the wall, with the bounce icon and the fading tail | done |
| tut_shoot | Shoot (click) | Two shots off the floor, each turning cyan where it bounces | done |
| tut_pistol | Pistol | The pistol alone at 2×, one shot per click | done |
| tut_magazine | Magazine | Six shots empty the HUD panel right to left; it turns red and refills while the gun reloads | done |
| tut_ricochet | Ricochet to kill | A direct hit splashes green and does nothing; a floor bounce eliminates the enemy | done |
| tut_twin_gun | Twin gun | Walks into the floating pickup: a gold flash and it's gone | done |
| tut_switch_guns | Switch guns (F) | Pistol barrel draws in, twin barrels slide out, lasers off meanwhile; and back | done |
| tut_twin_shot | Twin shot (click) | Two lasers, two shots fanning out | done |
| tut_top_barrel | Last round | Three rounds: a twin shot leaves one, the bottom laser goes out, the top barrel fires alone, then the reload | done |

### Audio (Assets/Audio)

| Sound | Trigger | File(s) | Status |
|---|---|---|---|
| Marker shot (player / enemy / heavy) | `PlayerController.Fire`, `Enemy.Fire` | sfx_shot_player, sfx_shot_enemy, sfx_shot_heavy | done |
| Ricochet "armed" (first bounce of a player shot) | `Projectile.BecomeLive` | sfx_ricochet_armed | done |
| Ricochet (later bounces, any shot) | `Projectile.Ricochet` | sfx_ricochet | done |
| Harmless splat (hit without elimination, shot ending on a wall) | `Enemy.HarmlessHit`, `Projectile.SplatOnWall` | sfx_splat_harmless | done |
| Elimination hit | `Enemy.PlayElimination`, boss damage | sfx_elimination | done |
| Player hit | `PlayerController.OnShot` | sfx_player_hit | done |
| Footsteps, walk and run | Animation events on walk/run clips | sfx_step_walk_0–3, sfx_step_run_0–3 | done |
| Jump / landing / crouch | `PlayerController` | sfx_jump, sfx_land, sfx_crouch | done |
| UI click | `GuiKit.Button` | sfx_ui_click | done |
| Round start / clear / lost | `GameBootstrap` | sfx_round_start, sfx_round_clear, sfx_round_lost | done |
| Gun switch, reload | `PlayerController` | sfx_gun_switch, sfx_reload | done |
| Music: menu, gameplay | `GameBootstrap` (music at 0.32 volume, about 10 dB under the effects) | Music/music_menu.ogg, Music/music_gameplay.ogg | done |

## Generated assets: methods and prompts

| Asset | Method / prompt | Pixel size | Target | Status |
|---|---|---|---|---|
| Player character | PixelLab `create_character` standard, side, size 48 (id 5285e2c7): "pixel art, side view, university student paintball player, blue hoodie with the hood down, dark blue jeans, white sneakers, black paintball goggles pushed up on the forehead, short brown hair, arms down with empty hands, no weapon, muted academic colours, crisp hard edges" | 49 → 48 tall | Player | done |
| Player animations | Templates: breathing-idle, walk, running-6-frames, jumping-1 (split into rise/fall/land), falling-back-death. v3 custom: "ducking down into a very deep squat…" (duck) and "duck walking forward in a very deep squat…" (duck-walk). Unused: the `crouching` and `crouched-walking` templates, which only reached ~40 px when the ducked hitbox is 29 px. Fitted by `process_chars.py`: feet on the bottom row, 2 flat torso rows dropped (nearest-neighbour), duck frames trimmed to 29 px. | 56×48 frames | Player | done |
| Enemy character | `create_character` standard, side, size 48 (id 42e474e0): "pixel art, side view, university student paintball opponent, red hoodie, red team armband, grey cargo pants, black boots, black full-face paintball mask with a dark visor, arms down with empty hands, no weapon, crisp hard edges" | 51 → 48 tall | Enemies | done |
| Enemy animations | breathing-idle, two-footed-jump (hop rise/fall), v3 deep squat (duck), falling-back-death; orange and purple are hue recolours of the red hoodie (orange brightened ×1.3) | 56×48 frames | Enemies | done |
| Boss character | `create_character` standard, side, size 84, custom proportions (shoulders 2.0, hips 1.7, legs 0.85) (id 15ef1d90): "pixel art, side view, huge bulky armoured campus security warden boss, extremely wide barrel chest and very broad shoulders with big round steel shoulder pauldrons, thick arms and legs, wide heavy stance, dark navy tactical suit, heavy brushed steel armour plates on the helmet, chest and back, yellow warning stripes on the armour, dark visor, arms down with empty hands, no weapon, crisp hard edges". Two earlier attempts were discarded: 105 px tall at 26 wide, then 68 px tall. Animations: breathing-idle, walk, running-6-frames, two-footed-jump (rise/fall), v3 deep squat (duck), falling-back-death; 4 flat torso rows dropped (88 → 84). | 96×84 frames | Boss | done |
| Lab bench concept | `create_image_pixflux` 72×48 (job 88e77925). It came back with margins, so the final was drawn to size; the modern redraw replaced it. | — | Low wall | superseded |
| Modern objects, weapons, splats, UI | Hand-authored pixel grids in `Tools/ArtGen/objects.py` with the STYLE_GUIDE palette (the same method as PixelLab's pixelart_workbench grid drawing, written straight to PNG). Whiteboards pre-rotated with a RotSprite-style 8× nearest-neighbour rotation and a fresh 1 px outline. | see tables | Level, weapons, UI | done |
| Paint bursts, sparks, flashes, splashes | PixelLab `vfx_workbench` (saved to My effects): fx_elim_burst_red (flash_burst + 2× blood_splat parts in paint colours), fx_bounce_spark (hit_star), fx_arm_flash (flash_burst, cyan), fx_blocked_splat (blood_splat, recoloured green). Team recolours are hue shifts. | see FX table | FX | done |
| Paintballs (test round) | PixelLab `pixelart_workbench draw` (drawing 46790a70) | 7×7, 15×15 | Shots | done |
| itch.io cover | PixelLab `create_image_pro` 315×250, opaque (15 generations): a layout composed from the game's sprites as reference and style image, plus the player, red enemy and boss idle frames as labelled references. Title drawn afterwards in a hand-built pixel font. Files in `ItchCover/` (630×500 = 2× nearest). | 315×250 | Store page | done |
| Back wall | `Tools/ArtGen/background_wall.py`: one 52 px wall module recovered from the cover (per-pixel mode across its six repeats, masking the foreground), its storey stacked twice to reach 360 px | 52×360 | Background | done |
| Sound effects | `Tools/ArtGen/synth_sfx.py` (original synthesis, CC0) | — | All SFX | done |
| Music | Juhani Junkala, "Title Screen" and "Level 1" (CC0, OpenGameArt), loudness-matched to −16 LUFS | — | Menu, gameplay | done |
