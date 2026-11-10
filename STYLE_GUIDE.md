# Style Guide

Every sprite in the game follows this guide. PixelLab prompts reuse the **descriptor blocks** below word for word so the
results stay consistent.

## Scale

| Setting | Value | Why |
|---|---|---|
| Pixels Per Unit | **30** (all sprites) | The 1680×840 window shows 14 world units vertically (orthographic size 7), so 1 unit = 60 screen px. At PPU 30 every art pixel is exactly 2×2 screen pixels, with no shimmer. |
| Filter / compression | Point, None | Crisp pixels. |
| Character body | 24×48 px (0.8×1.6 m collider) | Ducking: 24×29 px (collider shrinks to 60 %). |
| Boss body | 42×84 px (1.4×2.8 m) | Ducking: 42×50 px. |

Pixel size = world size × 30. Art adapts to the colliders; colliders never change to fit the art.

## Look

- Pixel art, **side view**, light from the **top-left**.
- **Outline:** single-pixel dark outline (`#14161c`) on every character, interactable object and projectile.
- **Shading:** basic. 2–3 tones per material plus one highlight, no dithering, no gradients, no anti-aliasing.
- Interactable objects (every ricochet surface) are **solid, hard and opaque**: they fill their collider rectangle edge
  to edge, with strong silhouettes, crisp edges, and more contrast and saturation than anything decorative.
- The **back wall** (`Background/bg_wall`, on the Background layer) is pale and low contrast: off-white laminate
  `#d9d6cf` with grey panel seams, frosted-glass bays `#cdd4d8` with a soft lighter glow in each pane, cream LED strips
  `#f3e8d6`, and a grey soffit under the ceiling. It has no outline and nothing in it is a surface. Foreground art keeps
  its dark outline and stronger contrast so it reads in front of it.
- Nothing decorative may look like a surface. If it has no collider, it doesn't get a hard outline or a solid fill
  (for example, no table legs under a floating ledge).

## Palette

A **modern** university building (the user's direction, 2026-11-09): white and light-grey laminate, brushed aluminium,
graphite, frosted glass, polished concrete and LED strips. No old wood, brick or ornament. Saturated team-paint colours
provide the contrast.

| Role | Dark | Mid | Light | Highlight |
|---|---|---|---|---|
| Outline | `#14161c` | | | |
| Laminate (cabinets, panels, doors) | `#8f97a6` | `#b9c0cc` | `#dde2e9` | `#f4f6f9` |
| Graphite (epoxy worktops, frames, trims) | `#1f232b` | `#2e333d` | `#444b58` | `#5f6878` |
| Brushed aluminium (rails, edges, handles) | `#5d6573` | `#8a93a2` | `#b8c0cc` | `#e2e7ee` |
| Frosted glass | `#5a7f8f` | `#7fa6b5` | `#a9cdd8` | `#dff1f6` |
| Polished concrete (floor, walls, slabs) | `#4a4f57` | `#636a74` | `#7f8792` | `#a0a8b2` |
| Light oak veneer (sparing accent) | `#8a6440` | `#b38a5c` | `#d6b48a` | |
| Safety-yellow edge accent | `#9c7a12` | `#e0b030` | `#f5d670` | |
| LED light | `#ffe9a8` | `#fffbe8` | | |
| Skin (varied per character) | `#7a4a32` | `#b07a55` | `#e0b48c` | |

Team and paint colours (saturated; reserved for characters, shots and FX):

| Use | Core | Shade | Highlight | Notes |
|---|---|---|---|---|
| Player team (hoodie) | `#3f8cff` | `#2357b8` | `#8cc2ff` | Matches the old placeholder body colour. |
| Player shot, **fresh** (no bounce yet: harmless) | `#ff8a1f` | `#c2520a` | `#ffd08a` | Plain, matte, no glow. |
| Player shot, **live** (bounced: lethal) | `#2ad4ff` | `#0a7bff` | `#e6fbff` | Bright, with a cyan glow halo and a short trail. |
| Enemy Standard (red) | `#e0303a` | `#8e1a22` | `#ff7a7f` | |
| Enemy Twin (orange) | `#f27a1f` | `#a8480c` | `#ffb46b` | |
| Enemy Heavy (purple) | `#8c40d9` | `#55208a` | `#c08cff` | |
| Enemy paint, standard and twin shots (magenta) | `#ff40d9` | `#a3168a` | `#ff9cf0` | Existing gameplay colour. |
| Enemy paint, heavy shots (purple) | `#a659ff` | `#6420b8` | `#d6b0ff` | Existing gameplay colour. |
| Boss armour | light steel blue | | | Lightened from the generated navy (the user's call): black is reserved for when he's immune. Cracked regions get a yellow crack overlay. |
| Enemy shot, **live** (bounced) | `#ff3b30` | `#a30f1a` | `#fff1ee` | Red-hot with a red glow halo and trail: the enemy mirror of the player's cyan. |
| Paint splash on an enemy (any hit, elimination burst included) | `#1fff32` | `#0ac219` | `#8aff94` | Green, at the user's request (2026-11-09). |

Hanging fixtures (the user's direction): floating platforms and bunker roofs are aluminium pipes hung from the ceiling
on rods, and the angled plates are broken LED light panels dangling from one cable, their second cable snapped. Rods and
cables are decoration (no collider): thin, dark and unoutlined, so they don't read as surfaces.

## Descriptor blocks (paste into prompts)

- **STYLE:** `pixel art, side view, single pixel dark outline, basic shading, light from top left, crisp hard edges, no anti-aliasing, muted academic colours`
- **CHARACTER:** `{STYLE}, university student paintball player, full body, standing in profile facing right, arms down with empty hands, no weapon`
- **OBJECT:** `{STYLE}, solid opaque modern university fixture, white laminate, brushed aluminium, graphite and frosted glass, clean minimal lines, fills the whole frame edge to edge, strong silhouette, high contrast`
- **FX:** `{STYLE}, bright saturated paint colours, high contrast`

## Characters

- Drawn facing **right**. Left uses `flipX` (all designs are symmetric enough).
- **Two-handed hold** (the user's reference, "Soldies sprite exemple.webp"): every pose holds both hands together at
  chest height, as if gripping a rifle. The marker itself is a separate sprite whose grip (with a hand drawn over it)
  sits exactly where each frame's hands are. It pivots there, so the aim can turn freely and the shot leaves its muzzle.
- No gun in the body sprite itself, so the aim can turn freely.
- Pivot at the **feet** (bottom centre). Frames share one canvas, padded sideways for limb swing; the body column is 24 px wide.
- Ceiling hangers reuse the floor sprites: the game rotates them 180°, so they hang feet-first from the ceiling.

## Projectiles

- Fresh vs live must read instantly. Fresh is a matte orange ball. Live is a bright cyan-blue ball with a white core
  highlight, a glow halo and a short streak trail.
- Sizes (diameter): player 7 px (0.24 m), enemy 8 px (0.28 m), heavy 19 px (0.64 m).
