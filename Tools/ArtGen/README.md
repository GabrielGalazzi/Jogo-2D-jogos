# Art and audio generators

Scripts that produced the game's hand-authored art, fitted the PixelLab character animations to the hitboxes, and
synthesized the sound effects. Python 3 with Pillow and numpy; ffmpeg for the music conversion.

| Script | What it does |
|---|---|
| `objects.py` | The modern infrastructure set, weapons, splats and UI, as pixel grids (`objects.py preview out.png` to look at them). |
| `export_art.py <Assets/Art> <vfx dir>` | Writes those grids to Assets/Art with per-team recolours, the pre-rotated whiteboards, the VFX strips (from PixelLab VFX exports) and `art_slices.json`. |
| `process_chars.py <cfg.json>` | Turns a PixelLab character download into hitbox-fitted strips: feet on the bottom row, a few flat torso rows dropped (nearest-neighbour) to reach the 48 px hitbox (29 px when ducking), recolour variants. The `cfg_*.json` paths point at a temporary download folder; change `source` to wherever you unzip the character. |
| `synth_sfx.py <Assets/Audio/SFX>` | Synthesizes every sound effect (CC0). |
| `background_wall.py <cover png> <Assets/Art> [preview.png]` | Builds the back-wall tile `Assets/Art/Background/bg_wall.png` (52×360) from the itch.io cover's wall (`ItchCover/ricochet_cover_315x250_source.png`) and adds its `art_slices.json` entry. The foreground masks match that one image. |
| `tutorial_gifs.py <Assets/Art> <gif dir> [scene ...]` | Renders the tutorial cards' animations from the game's sprites into `Assets/Art/UI/Tutorial` (frame sheets plus their play order in `art_slices.json`) and 2× GIF copies into `<gif dir>` (the project uses `TutorialGifs`). Name scenes to render only those. Run it again after changing any art it uses. |

`hold_start/` holds the player, enemy and boss posed in the two-handed hold, facing right on their PixelLab canvases.
Every animation that shows the gun in hand starts from these frames. The `hands` block in a `cfg_*.json` finds the grip
on each fitted frame and writes it to `art_slices.json`, and the gun's grip follows that point in game.

The original characters belong to the first PixelLab account. From any other account, animate these frames with
`animate_image` or `animate_image_pixminimax` (8 frames, facing east), then point the config's `source` at the
downloaded frames.

After changing files in Assets/Art or Assets/Audio, run **Ricochet ▸ Build Art Library** in Unity.
