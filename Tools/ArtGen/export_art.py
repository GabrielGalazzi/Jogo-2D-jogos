"""Writes the hand-authored objects, recolours, pre-rotated plates and VFX strips into Assets/Art, plus art_slices.json.

Usage: export_art.py <Assets/Art dir> <vfx download dir>
Nearest-neighbour only. Recolours swap exact ramp colours or shift hue; nothing is smoothed.
"""
import colorsys
import json
import math
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import objects  # noqa: E402

ART, VFX = sys.argv[1], sys.argv[2]
SLICES = {}  # path (relative to Assets/Art, no extension) -> {frame:[w,h], pivot:[x,y] (0..1, from bottom-left), fps, border, mesh}


def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


def grid_image(grid, remap=None):
    remap = remap or {}
    im = Image.new('RGBA', (grid.w, grid.h))
    for y, row in enumerate(grid.rows()):
        for x, ch in enumerate(row):
            if ch == '.':
                continue
            colour = remap.get(ch, objects.PAL[ch])
            im.putpixel((x, y), hexrgb(colour) + (255,))
    return im


def save(im, rel, pivot=(0.5, 0.5), frame=None, fps=None, border=None, mesh=None):
    path = os.path.join(ART, rel + '.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    entry = {'frame': list(frame or im.size), 'pivot': [round(pivot[0], 4), round(pivot[1], 4)]}
    if fps:
        entry['fps'] = fps
    if border:
        entry['border'] = border  # left, bottom, right, top
    if mesh:
        entry['mesh'] = mesh
    SLICES[rel] = entry


RAMPS = {  # core, shade, highlight
    'player': ('#3f8cff', '#2357b8', '#8cc2ff'),
    'red': ('#e0303a', '#8e1a22', '#ff7a7f'),
    'orange': ('#f27a1f', '#a8480c', '#ffb46b'),
    'purple': ('#8c40d9', '#55208a', '#c08cff'),
    'boss': ('#e0b030', '#9c7a12', '#f5d670'),
    'paint_blue': ('#2ad4ff', '#0a7bff', '#e6fbff'),
    'paint_orange': ('#ff8a1f', '#c2520a', '#ffd08a'),
    'paint_magenta': ('#ff40d9', '#a3168a', '#ff9cf0'),
    'paint_purple': ('#a659ff', '#6420b8', '#d6b0ff'),
}


def accent(ramp):
    c, s, h = RAMPS[ramp]
    return {'G': c, 'H': s, 'I': h}


grids = {k: f() for k, f in objects.OBJECTS.items()}

# ---- objects
for key in ('obj_lab_bench', 'obj_ceiling_duct', 'obj_door_safezone', 'obj_door_arena', 'obj_door_exit', 'obj_exit_sign',
            'obj_door_led'):
    save(grid_image(grids['Objects/' + key]), 'Objects/' + key)
for key in ('obj_floor', 'obj_ceiling', 'obj_wall_concrete', 'obj_wall_partition'):
    save(grid_image(grids['Objects/' + key]), 'Objects/' + key, mesh='FullRect')

# ---- weapons, held in both hands: the pivot is the rear hand on the grip (the shot's origin), the muzzle sits the
# barrel length in front of it. Player 0.85 m (26 px), enemies 0.75 m (23 px), boss 1.0 m (30 px).
SKIN = {'K': '#e4a889', 'L': '#b0705a'}
GAUNTLET = {'K': '#9aa6c0', 'L': '#5d6888'}
GLOVE = {'K': '#3a383b', 'L': '#222023'}         # the enemies' black gloves

def gun_pivot(w, h, px0, cy):
    return ((px0 + 0.5) / w, (h - cy - 0.5) / h)

def save_gun(out, length, barrels, ramp, hands=SKIN, heavy=False, with_hands=True):
    g, px0, cy = objects.gun(length, barrels, 'GHI', heavy=heavy, with_hands=with_hands)
    remap = dict(accent(ramp))
    remap.update(hands)
    im = grid_image(g, remap)
    save(im, 'Weapons/' + out, pivot=gun_pivot(im.width, im.height, px0, cy))

TWIN = [(-2, 2), (2, 2)]
save_gun('wpn_pistol_player', 26, [(0, 3)], 'player')
save_gun('wpn_twin_player', 26, TWIN, 'player')
save_gun('wpn_twin_pickup', 26, TWIN, 'player', with_hands=False)
save_gun('wpn_standard_enemy', 23, [(0, 3)], 'red', GLOVE)
save_gun('wpn_twin_enemy', 23, TWIN, 'orange', GLOVE)
save_gun('wpn_heavy_enemy', 23, [(0, 7)], 'purple', GLOVE, heavy=True)
save_gun('wpn_standard_boss', 30, [(0, 3)], 'boss', GAUNTLET)
save_gun('wpn_twin_boss', 30, TWIN, 'boss', GAUNTLET)
save_gun('wpn_heavy_boss', 30, [(0, 7)], 'boss', GAUNTLET, heavy=True)

# reload: six frames per player gun, same pivot as the gun so it can swap in place
for name, barrels in (('pistol', [(0, 3)]), ('twin', TWIN)):
    frames = objects.reload_frames(26, barrels, 'GHI')
    remap = dict(accent('player'))
    remap.update(SKIN)
    ims = [grid_image(g, remap) for g, _, _ in frames]
    fw, fh = ims[0].size
    strip = Image.new('RGBA', (fw * len(ims), fh))
    for i, im in enumerate(ims):
        strip.paste(im, (fw * i, 0))
    _, px0, cyb = frames[0]
    save(strip, 'Weapons/wpn_%s_player_reload' % name, pivot=gun_pivot(fw, fh, px0, cyb), frame=[fw, fh], fps=3)

# ---- hanging pipes (floating platforms and bunker roofs), their rods and clamps; broken hanging light panels, cables
save(grid_image(grids['Objects/obj_pipe']), 'Objects/obj_pipe', border=[3, 0, 3, 0], mesh='FullRect')
save(grid_image(grids['Objects/obj_pipe_hazard']), 'Objects/obj_pipe_hazard', border=[3, 0, 3, 0], mesh='FullRect')
save(grid_image(grids['Objects/obj_hanger_rod']), 'Objects/obj_hanger_rod', mesh='FullRect')
save(grid_image(grids['Objects/obj_cable']), 'Objects/obj_cable', mesh='FullRect')
save(grid_image(grids['Objects/obj_pipe_clamp']), 'Objects/obj_pipe_clamp', pivot=(0.5, 0.0))
save(grid_image(grids['Objects/obj_cable_snapped']), 'Objects/obj_cable_snapped', pivot=(0.5, 1.0))
save(grid_image(grids['Objects/obj_light_panel']), 'Objects/obj_light_panel')
save(grid_image(grids['UI/ui_heart_full']), 'UI/ui_heart_full')
save(grid_image(grids['UI/ui_heart_empty']), 'UI/ui_heart_empty')

# ---- splat decals: 3 variants per paint, as a strip
for paint in ('paint_blue', 'paint_orange', 'paint_magenta', 'paint_purple'):
    frames = [grid_image(grids['FX/fx_splat_' + v], accent(paint)) for v in 'abc']
    strip = Image.new('RGBA', (18 * 3, 18))
    for i, f in enumerate(frames):
        strip.paste(f, (18 * i, 0))
    save(strip, 'FX/fx_splat_' + paint.split('_')[1], frame=[18, 18])

save(grid_image(grids['FX/fx_pickup_glow']), 'FX/fx_pickup_glow')

# ---- UI
save(grid_image(grids['UI/ui_ammo_full'], accent('paint_orange')), 'UI/ui_ammo_full')
save(grid_image(grids['UI/ui_ammo_empty']), 'UI/ui_ammo_empty')
for key in ('ui_bounce_icon', 'ui_crosshair', 'ui_laser'):
    save(grid_image(grids['UI/' + key]), 'UI/' + key, mesh='FullRect' if key == 'ui_laser' else None)
for state in ('normal', 'hover', 'pressed', 'ghost'):
    save(grid_image(grids['UI/ui_button_' + state]), 'UI/ui_button_' + state, border=[3, 3, 3, 3])


# ---- pre-rotated whiteboard plates (one per angle the levels use)
def rotate_plate(src, angle):
    """RotSprite-style: strip the outline, upscale 8x nearest, rotate nearest, sample block centres, re-outline."""
    w, h = src.size
    inner = src.copy()
    px = inner.load()
    for x in range(w):  # replace the 1 px outline with the neighbouring frame colour
        px[x, 0] = px[x, 1]
        px[x, h - 1] = px[x, h - 2]
    for y in range(h):
        px[0, y] = px[1, y]
        px[w - 1, y] = px[w - 2, y]
    k = 8
    big = inner.resize((w * k, h * k), Image.NEAREST)
    rot = big.rotate(angle, resample=Image.NEAREST, expand=True)
    rw, rh = math.ceil(rot.width / k), math.ceil(rot.height / k)
    canvas = Image.new('RGBA', (rw * k, rh * k))
    canvas.paste(rot, ((rw * k - rot.width) // 2, (rh * k - rot.height) // 2))
    out = Image.new('RGBA', (rw, rh))
    cp, op = canvas.load(), out.load()
    for y in range(rh):
        for x in range(rw):
            p = cp[x * k + k // 2, y * k + k // 2]
            if p[3] > 0:
                op[x, y] = p
    ink = hexrgb(objects.PAL['o']) + (255,)
    src_px = [[op[x, y] for x in range(rw)] for y in range(rh)]
    for y in range(rh):
        for x in range(rw):
            if src_px[y][x][3] == 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < rw and 0 <= ny < rh) or src_px[ny][nx][3] == 0:
                    op[x, y] = ink
                    break
    return out

plate = Image.open(os.path.join(ART, 'Objects/obj_light_panel.png')).convert('RGBA')
for angle in (30, -30, 35, -35, 40, -40, 45, -45, 50, -50, 55, -55, 60, -60):
    name = 'Objects/Plates/obj_light_panel_%s%d' % ('m' if angle < 0 else 'p', abs(angle))
    save(rotate_plate(plate, angle), name)


# ---- VFX strips from the effects tool's sheets
def hue_shift(im, hue_deg, sat_scale=1.0):
    im = im.copy()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.12:
                continue
            r2, g2, b2 = colorsys.hsv_to_rgb(hue_deg / 360, min(1, s * sat_scale), v)
            px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    return im


def vfx_strip(name, out, recolours=None, keep_blank=0):
    sheet = Image.open(os.path.join(VFX, name + '.png')).convert('RGBA')
    meta = json.load(open(os.path.join(VFX, name + '.json')))
    frames = []
    pivot = None
    for f in meta['frames']:
        r = f['frame']
        frames.append(sheet.crop((r['x'], r['y'], r['x'] + r['w'], r['y'] + r['h'])))
        pivot = f.get('pivot', {'x': 0.5, 'y': 0.5})
    while len(frames) > 1 and frames[-1].getbbox() is None:
        frames.pop()
    fw, fh = frames[0].size
    durations = [f['duration'] for f in meta['frames']]
    fps = round(1000 / (sum(durations) / len(durations)))
    strip = Image.new('RGBA', (fw * len(frames), fh))
    for i, f in enumerate(frames):
        strip.paste(f, (fw * i, 0))
    piv = (pivot['x'], 1 - pivot['y'])  # json pivot is from the top-left
    save(strip, out, pivot=piv, frame=[fw, fh], fps=fps)
    for suffix, hue in (recolours or {}).items():
        save(hue_shift(strip, hue), out.replace('_red', '_' + suffix), pivot=piv, frame=[fw, fh], fps=fps)

# Every paint splash on an enemy is green (the user's call), the elimination burst included.
vfx_strip('elim', 'FX/fx_elim_burst_green')
_burst = os.path.join(ART, 'FX/fx_elim_burst_green.png')
hue_shift(Image.open(_burst).convert('RGBA'), 125, 1.0).save(_burst)
vfx_strip('spark', 'FX/fx_bounce_spark')
vfx_strip('arm', 'FX/fx_arm_flash')
# The flash when an enemy shot bounces and turns lethal: the arming flash in red.
vfx_strip('arm', 'FX/fx_arm_flash_red')
_red = os.path.join(ART, 'FX/fx_arm_flash_red.png')
hue_shift(Image.open(_red).convert('RGBA'), 2, 1.0).save(_red)
vfx_strip('blocked', 'FX/fx_blocked_splat')
# The user's call (2026-11-09): a paintball's splash on an enemy is green.
_blocked = os.path.join(ART, 'FX/fx_blocked_splat.png')
hue_shift(Image.open(_blocked).convert('RGBA'), 125, 1.0).save(_blocked)

# already-made paintballs keep their entries; enemy shots drawn here
def ball(d, ramp, outline='#14161c'):
    c, s, hl = (hexrgb(x) for x in RAMPS[ramp])
    im = Image.new('RGBA', (d, d))
    px = im.load()
    r = d / 2
    for y in range(d):
        for x in range(d):
            dx, dy = x + 0.5 - r, y + 0.5 - r
            dist = math.hypot(dx, dy)
            if dist > r:
                continue
            col = c
            if dist > r - 1.0:
                col = hexrgb(outline)
            elif dx + dy > r * 0.55:
                col = s
            elif math.hypot(dx + r * 0.35, dy + r * 0.35) < r * 0.3:
                col = hl
            px[x, y] = col + (255,)
    return im

save(ball(8, 'paint_magenta'), 'FX/fx_paintball_enemy')
save(ball(19, 'paint_purple'), 'FX/fx_paintball_heavy')

# Enemy shots once they've bounced and turned lethal: red-hot with a white core, and a red glow halo.
RAMPS['enemy_live'] = ('#ff3b30', '#a30f1a', '#fff1ee')
save(ball(8, 'enemy_live', '#4a0a10'), 'FX/fx_paintball_enemy_live')
save(ball(19, 'enemy_live', '#4a0a10'), 'FX/fx_paintball_heavy_live')

def glow(d, ramp):
    dark, mid, light = (hexrgb(x) for x in ramp)
    im = Image.new('RGBA', (d, d))
    px = im.load()
    r = d / 2
    for y in range(d):
        for x in range(d):
            dist = math.hypot(x + 0.5 - r, y + 0.5 - r)
            if dist <= r:
                px[x, y] = (dark if dist > r * 0.72 else mid if dist > r * 0.45 else light) + (255,)
    return im

save(glow(17, ('#a30f1a', '#ff3b30', '#ff9a8f')), 'FX/fx_paintball_enemy_live_glow')
save(glow(33, ('#a30f1a', '#ff3b30', '#ff9a8f')), 'FX/fx_paintball_heavy_live_glow')
for n in ('fx_paintball_fresh', 'fx_paintball_live', 'fx_paintball_live_glow'):
    SLICES['FX/' + n] = {'frame': list(Image.open(os.path.join(ART, 'FX', n + '.png')).size), 'pivot': [0.5, 0.5]}

# ---- 2x versions for screen-space IMGUI (buttons 9-sliced at border 6) and the hardware cursor
for state in ('normal', 'hover', 'pressed', 'ghost'):
    im = grid_image(grids['UI/ui_button_' + state])
    save(im.resize((im.width * 2, im.height * 2), Image.NEAREST), 'UI/ui_button_%s_2x' % state, border=[6, 6, 6, 6])
cross = grid_image(grids['UI/ui_crosshair'])
save(cross.resize((cross.width * 2, cross.height * 2), Image.NEAREST), 'UI/ui_crosshair_2x')

# ---- enemy paint splashing on the player: the harmless-hit splash recoloured to the enemy paints
vfx_strip('blocked', 'FX/fx_hit_red', {'magenta': 312, 'purple': 272})
os.remove(os.path.join(ART, 'FX/fx_hit_red.png'))
SLICES.pop('FX/fx_hit_red', None)


# ---- boss armour crack overlays, sized to its armour regions (head 1.2x0.64 m, each torso half 0.55x1.76 m)
def crack(w, h, seed):
    import random
    rnd = random.Random(seed)
    im = Image.new('RGBA', (w, h))
    px = im.load()
    core, mid = hexrgb('#f5d670') + (255,), hexrgb('#e0b030') + (255,)
    pts = set()
    for branch in range(5 if h > w else 4):
        x, y = w // 2 + rnd.randint(-2, 2), h // 2 + rnd.randint(-3, 3)
        dx, dy = rnd.choice((-1, 1)), rnd.choice((-1, 1))
        for _ in range(max(w, h) // 2):
            pts.add((x, y))
            if rnd.random() < 0.55:
                x += dx
            else:
                y += dy
            if rnd.random() < 0.15:
                dx = -dx if rnd.random() < 0.3 else dx
            if not (1 <= x < w - 1 and 1 <= y < h - 1):
                break
    for (x, y) in pts:
        px[x, y] = core if (x + y) % 3 else mid
    ink = hexrgb('#14161c') + (255,)
    for (x, y) in list(pts):
        for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + ox, y + oy
            if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] == 0:
                px[nx, ny] = ink
    return im

save(crack(36, 19, 5), 'Characters/char_boss_crack_head')
save(crack(16, 53, 9), 'Characters/char_boss_crack_torso')

prev = {}
manifest = os.path.join(ART, 'art_slices.json')
if os.path.exists(manifest):
    prev = json.load(open(manifest))
prev.update(SLICES)
json.dump(prev, open(manifest, 'w'), indent=1, sort_keys=True)
print('wrote', len(SLICES), 'entries')
