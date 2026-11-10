"""Renders the animated pictures on the tutorial boards (levels 1 and 6) from the game's own sprites.

Usage: tutorial_gifs.py <Assets/Art dir> <gif preview dir> [scene ...]

Each scene is a small stage rebuilt in pixels from the real art: the player and enemy strips with their hand points, the
guns held at the grip, the laser sight, paintballs that turn live on their first bounce, the effects and the HUD
magazine. Frames are 100x75, drawn 1:1 into the card's 100x75 picture slot at 1680x840.

Writes, per scene:
  Assets/Art/UI/Tutorial/tut_<name>.png   the distinct frames in a grid, for Unity (sliced by art_slices.json)
  art_slices.json entry                   frame size, fps and "sequence" (frame order, repeats included)
  <gif dir>/tut_<name>.gif                the same loop as a GIF at 2x, the game's pixel density, to look at

Nearest-neighbour only. Shots are slowed and jumps lowered against the real game so they read in a 3 m wide frame.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

ART = sys.argv[1]
GIF_DIR = sys.argv[2]
ONLY = set(sys.argv[3:])

W, H = 100, 75
FPS = 20
DT = 1.0 / FPS
GRID_COLUMNS = 20

with open(os.path.join(ART, 'art_slices.json')) as f:
    SLICES = json.load(f)

# ---- colours (from the game code)

BG = (217, 214, 207)                      # GameBootstrap.BackgroundColor (0.85, 0.84, 0.81)
LASER = (255, 51, 51, 0.6)                # LaserSight.color
BOUNCE_ICON = (0.16, 0.83, 1.0, 0.9)      # LaserSight.BounceIconColor
MAG_BLUE = ((51, 115, 242), 0.85)         # GameBootstrap.MagazineColor
MAG_RED = ((230, 46, 51), 0.85)           # GameBootstrap.MagazineEmptyColor
GOLD = (1.0, 0.85, 0.2)                   # GunPickup.GlowColor
KEY_FACE = (235, 237, 242)                # GuiKit.KeyFace
KEY_EDGE = (128, 135, 153)                # GuiKit.KeyEdge
KEY_TEXT = (31, 33, 41)                   # GuiKit.KeyText
KEY_OUTLINE = (58, 62, 74)                # so the light keys stand out against the light backdrop
ACCENT_L1 = (97, 158, 255)                # TutorialBoard.Controls accent
ACCENT_L6 = (255, 209, 64)                # TutorialBoard.TwinGun accent

# ---- sprites


class Sprite:
    """One frame of art and its pivot, in pixels from its top-left corner (fractions stay: .5 is a pixel centre)."""

    def __init__(self, image, pivot, hands=None):
        self.image = image
        self.pivot = pivot
        self.hands = hands  # (x, y) from the feet pivot, y up, facing right

    @property
    def w(self):
        return self.image.width

    @property
    def h(self):
        return self.image.height


def load(rel):
    """All frames of an Assets/Art image, sliced and pivoted the way the Unity importer does it."""
    entry = SLICES[rel]
    image = Image.open(os.path.join(ART, rel + '.png')).convert('RGBA')
    fw, fh = entry['frame']
    px, py = entry['pivot']
    hands = entry.get('hands')
    frames = []
    for i, x in enumerate(range(0, image.width - fw + 1, fw)):
        frame = image.crop((x, 0, x + fw, fh))
        hand = (hands[i * 2], hands[i * 2 + 1]) if hands and i * 2 + 1 < len(hands) else None
        frames.append(Sprite(frame, (px * fw, (1 - py) * fh), hand))
    return frames


class Anim:
    def __init__(self, rel, loop=True):
        self.frames = load(rel)
        self.fps = SLICES[rel].get('fps', 10)
        self.loop = loop

    def at(self, t):
        i = int(math.floor(t * self.fps + 1e-6))
        i = i % len(self.frames) if self.loop else min(i, len(self.frames) - 1)
        return self.frames[max(i, 0)]

    @property
    def duration(self):
        return len(self.frames) / self.fps


def single(rel):
    return load(rel)[0]


PLAYER = {s: Anim('Characters/char_player_%s_right' % s, s not in ('rise', 'land', 'duck', 'death'))
          for s in ('idle', 'walk', 'run', 'rise', 'fall', 'land', 'duck', 'duckwalk', 'death')}
ENEMY = {s: Anim('Characters/char_enemy_red_%s_right' % s, s != 'death') for s in ('idle', 'death')}

PISTOL = single('Weapons/wpn_pistol_player')
TWIN = single('Weapons/wpn_twin_player')
TWIN_PICKUP = single('Weapons/wpn_twin_pickup')
RELOAD_PISTOL = Anim('Weapons/wpn_pistol_player_reload')
RELOAD_TWIN = Anim('Weapons/wpn_twin_player_reload')
ENEMY_GUN = single('Weapons/wpn_standard_enemy')

BALL_FRESH = single('FX/fx_paintball_fresh')
BALL_LIVE = single('FX/fx_paintball_live')
BALL_LIVE_GLOW = single('FX/fx_paintball_live_glow')
BALL_ENEMY_LIVE = single('FX/fx_paintball_enemy_live')
BALL_ENEMY_LIVE_GLOW = single('FX/fx_paintball_enemy_live_glow')
ARM_FLASH = Anim('FX/fx_arm_flash', loop=False)
BLOCKED_SPLAT = Anim('FX/fx_blocked_splat', loop=False)
BURST = Anim('FX/fx_elim_burst_green', loop=False)
PICKUP_GLOW = single('FX/fx_pickup_glow')

FLOOR_TILE = Image.open(os.path.join(ART, 'Objects/obj_floor.png')).convert('RGBA')
WALL_TILE = Image.open(os.path.join(ART, 'Objects/obj_wall_concrete.png')).convert('RGBA')
BENCH = single('Objects/obj_lab_bench')
BOUNCE = single('UI/ui_bounce_icon')
CROSSHAIR = single('UI/ui_crosshair')
LASER_DASH = [p[3] > 0 for p in Image.open(os.path.join(ART, 'UI/ui_laser.png')).convert('RGBA').getdata()]


def handless_pistol():
    """The pistol without the player's hands on it, for its close-up: the skin pixels take the grip colours from the
    hand-free twin pickup, which shares its grip and pivot row."""
    skin = {(0xe4, 0xa8, 0x89), (0xb0, 0x70, 0x5a)}
    image = PISTOL.image.copy()
    for y in range(image.height):
        for x in range(image.width):
            if image.getpixel((x, y))[:3] in skin:
                image.putpixel((x, y), TWIN_PICKUP.image.getpixel((x, y)) if y < TWIN_PICKUP.h else (0, 0, 0, 0))
    return Sprite(image, PISTOL.pivot)


# ---- metres -> pixels: 30 px per metre (PPU 30), as in the game

PPU = 30
BARREL = 0.85 * PPU                       # PlayerController.barrelLength
SHOT_RADIUS = 0.12 * PPU                  # ProjectileSettings.radius
TAIL = 1.5 * PPU                          # LaserSight.tailLength
HITBOX = (0.8 * PPU, 1.6 * PPU)           # player and enemy standing hitbox
SHOT_SPEED = 230.0                        # px/s; the game's is 600 (20 m/s), far too fast to follow at this size

# ---- the frame


class Frame:
    """A 100x75 picture. Scene coordinates: x right, y up, in pixels from the bottom-left corner."""

    def __init__(self, bg=BG):
        self.im = Image.new('RGBA', (W, H), bg + (255,))

    def blit(self, sprite, x, y, flip=False, angle=0.0, alpha=1.0, tint=None, scale_x=1.0, flip_y=False):
        """Draws a sprite with its pivot at scene point (x, y): mirrored, rotated about the pivot (degrees, anticlockwise),
        squashed along its own x (the gun switch), tinted (multiplied) and faded."""
        im = sprite.image
        px, py = sprite.pivot
        if flip:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
            px = sprite.w - px
        if flip_y:
            im = im.transpose(Image.FLIP_TOP_BOTTOM)
            py = sprite.h - py
        if scale_x != 1.0:
            nw = max(0, int(round(sprite.w * scale_x)))
            if nw == 0:
                return
            im = im.resize((nw, sprite.h), Image.NEAREST)
            px *= nw / sprite.w
        if tint is not None or alpha < 1.0:
            a = np.asarray(im).astype(np.float32)
            if tint is not None:
                a[..., 0] *= tint[0]
                a[..., 1] *= tint[1]
                a[..., 2] *= tint[2]
                if len(tint) > 3:
                    a[..., 3] *= tint[3]
            a[..., 3] *= alpha
            im = Image.fromarray(np.clip(a + 0.5, 0, 255).astype(np.uint8))
        if abs(angle) > 0.01:
            # Rotate about the pivot: pad so the pivot is the exact centre, then rotate nearest-neighbour, as Unity draws it.
            r = int(math.ceil(math.hypot(max(px, im.width - px), max(py, im.height - py)))) + 2
            pad = Image.new('RGBA', (2 * r, 2 * r))
            ox, oy = int(round(r - px)), int(round(r - py))
            pad.paste(im, (ox, oy))
            im = pad.rotate(angle, resample=Image.NEAREST, center=(ox + px, oy + py))
            px, py = ox + px, oy + py
        left = int(math.floor(x - px + 0.5))
        top = int(math.floor((H - y) - py + 0.5))
        layer = Image.new('RGBA', (W, H))
        layer.paste(im, (left, top))
        self.im.alpha_composite(layer)

    def fill(self, x0, y0, x1, y1, colour, alpha=1.0):
        """Fills scene rect [x0, x1) x [y0, y1) (y up)."""
        x0, x1 = max(0, int(round(x0))), min(W, int(round(x1)))
        top, bottom = max(0, int(round(H - y1))), min(H, int(round(H - y0)))
        if x1 <= x0 or bottom <= top:
            return
        layer = Image.new('RGBA', (W, H))
        layer.paste(colour + (int(round(255 * alpha)),), (x0, top, x1, bottom))
        self.im.alpha_composite(layer)

    def floor(self, top, scroll=0):
        """The polished concrete floor, tiled, its top edge at height top."""
        for x in range(-(int(scroll) % 60), W, 60):
            self._paste_clip(FLOOR_TILE, x, H - top)

    def wall(self, left):
        """The exposed concrete wall, from x = left to the right edge, floor to top."""
        for y in range(-60, H, 60):
            self._paste_clip(WALL_TILE, int(left), y)

    def _paste_clip(self, tile, x, y):
        layer = Image.new('RGBA', (W, H))
        layer.paste(tile, (x, y))
        self.im.alpha_composite(layer)

    def line(self, a, b, colour, alpha, offset=0.0, dashed=True):
        """A 1 px laser segment from a to b, dashed like ui_laser (4 on, 2 off) counted from offset along the beam."""
        ax, ay = a
        bx, by = b
        length = math.hypot(bx - ax, by - ay)
        if length < 0.5:
            return
        pixels = {}
        steps = int(length * 3) + 1
        for i in range(steps + 1):
            s = length * i / steps
            if dashed and not LASER_DASH[int(offset + s) % len(LASER_DASH)]:
                continue
            x = ax + (bx - ax) * s / length
            y = ay + (by - ay) * s / length
            pixels[(int(math.floor(x)), int(math.floor(H - y)))] = True
        layer = np.zeros((H, W, 4), np.uint8)
        for (x, y) in pixels:
            if 0 <= x < W and 0 <= y < H:
                layer[y, x] = colour + (int(round(255 * alpha)),)
        self.im.alpha_composite(Image.fromarray(layer))

    def fade(self, amount, colour=BG):
        """Fades the whole frame towards colour (0 = untouched, 1 = solid)."""
        if amount <= 0:
            return
        layer = Image.new('RGBA', (W, H), colour + (int(round(255 * min(1.0, amount))),))
        self.im.alpha_composite(layer)


# ---- tiny pixel font and input icons for the corner indicator

FONT = {
    'A': ['.#.', '#.#', '###', '#.#', '#.#'],
    'D': ['##.', '#.#', '#.#', '#.#', '##.'],
    'F': ['###', '#..', '##.', '#..', '#..'],
    'H': ['#.#', '#.#', '###', '#.#', '#.#'],
    'I': ['###', '.#.', '.#.', '.#.', '###'],
    'S': ['.##', '#..', '.#.', '..#', '##.'],
    'T': ['###', '.#.', '.#.', '.#.', '.#.'],
    'W': ['#...#', '#...#', '#.#.#', '#.#.#', '.#.#.'],
}


def text_width(label):
    return sum(len(FONT[ch][0]) + 1 for ch in label) - 1


def keycap(frame, x, y_top, label, pressed, accent):
    """A keyboard key in the frame's top-left corner area: a light face over a darker edge, the label in a 3x5 font.
    Pressed, the face sinks a pixel and lights up in the board's accent colour. x, y_top in image pixels."""
    w = max(11, text_width(label) + 6)
    d = ImageDraw(frame)
    # Dark outline, then the grey edge showing under the face; pressed, the face sinks onto the edge.
    d.rect(x, y_top, w, 12, KEY_OUTLINE)
    d.rect(x + 1, y_top + 1, w - 2, 10, KEY_EDGE)
    sink = 1 if pressed else 0
    d.rect(x + 1, y_top + 1 + sink, w - 2, 8, accent if pressed else KEY_FACE)
    tx = x + (w - text_width(label) + 1) // 2
    for ch in label:
        for r, row in enumerate(FONT[ch]):
            for c, on in enumerate(row):
                if on == '#':
                    d.px(tx + c, y_top + 3 + sink + r, KEY_TEXT)
        tx += len(FONT[ch][0]) + 1
    return w


MOUSE_ROWS = [
    '..#####..',
    '.#LLL#RR#',
    '#LLLL#RRR#',
    '#LLLL#RRR#',
    '#LLLL#RRR#',
    '##########',
    '#BBBBBBBB#',
    '#BBBBBBBB#',
    '#BBBBBBBB#',
    '#BBBBBBBB#',
    '.#BBBBBB#.',
    '..######..',
]


def mouse_icon(frame, x, y_top, clicked, accent):
    """The mouse, its left button lit in the accent colour while clicked."""
    d = ImageDraw(frame)
    for r, row in enumerate(MOUSE_ROWS):
        for c, ch in enumerate(row):
            if ch == '#':
                d.px(x + c, y_top + r, KEY_OUTLINE)
            elif ch == 'L':
                d.px(x + c, y_top + r, accent if clicked else KEY_FACE)
            elif ch in 'RB':
                d.px(x + c, y_top + r, KEY_FACE)


class ImageDraw:
    """Opaque pixel writes in image coordinates (y down)."""

    def __init__(self, frame):
        self.im = frame.im

    def px(self, x, y, colour):
        if 0 <= x < W and 0 <= y < H:
            self.im.putpixel((x, y), colour + (255,))

    def rect(self, x, y, w, h, colour):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.px(xx, yy, colour)


# ---- characters and guns


def player_frame(state, t):
    return PLAYER[state].at(t)


def draw_character(frame, sprite, x, floor, right=True, alpha=1.0):
    frame.blit(sprite, x, floor, flip=not right, alpha=alpha)


def hands_at(sprite, x, floor, right=True):
    hx, hy = sprite.hands
    return (x + hx if right else x - hx), floor + hy


def draw_gun(frame, gun, pivot, angle, right=True, scale_x=1.0, alpha=1.0):
    """A gun at its grip, aimed at angle (degrees from +x). Facing left it's mirrored across its own axis, as the game
    flips the gun pivot's y scale, so the hopper stays on top."""
    frame.blit(gun, pivot[0], pivot[1], angle=angle, flip_y=not right, scale_x=scale_x, alpha=alpha)


def direction(angle):
    a = math.radians(angle)
    return math.cos(a), math.sin(a)


def rotate(v, degrees):
    a = math.radians(degrees)
    return v[0] * math.cos(a) - v[1] * math.sin(a), v[0] * math.sin(a) + v[1] * math.cos(a)


# ---- geometry: floors, walls and targets the laser and the shots meet


class Stage:
    def __init__(self, floor, wall_x=None, targets=None):
        self.floor = floor
        self.wall_x = wall_x
        self.targets = targets or []   # (x0, y0, x1, y1) boxes that stop a shot

    def cast(self, origin, d, radius, max_distance=400.0):
        """First thing a ball of radius meets moving from origin along d: (distance, centre, normal, target index)."""
        ox, oy = origin
        best = (max_distance, None, None, None)
        if d[1] < 0:
            t = (self.floor + radius - oy) / d[1]
            if 0 <= t < best[0]:
                best = (t, (ox + d[0] * t, oy + d[1] * t), (0.0, 1.0), None)
        if self.wall_x is not None and d[0] > 0:
            t = (self.wall_x - radius - ox) / d[0]
            if 0 <= t < best[0]:
                best = (t, (ox + d[0] * t, oy + d[1] * t), (-1.0, 0.0), None)
        for i, (x0, y0, x1, y1) in enumerate(self.targets):
            t = slab(origin, d, (x0 - radius, y0 - radius, x1 + radius, y1 + radius))
            if t is not None and 0 <= t < best[0]:
                best = (t, (ox + d[0] * t, oy + d[1] * t), None, i)
        return best


def slab(o, d, box):
    t0, t1 = -1e9, 1e9
    for axis in (0, 1):
        lo, hi = box[axis], box[axis + 2]
        if abs(d[axis]) < 1e-9:
            if not lo <= o[axis] <= hi:
                return None
            continue
        a, b = (lo - o[axis]) / d[axis], (hi - o[axis]) / d[axis]
        t0, t1 = max(t0, min(a, b)), min(t1, max(a, b))
    return t0 if t0 <= t1 and t1 >= 0 else None


def reflect(d, n):
    k = 2 * (d[0] * n[0] + d[1] * n[1])
    return d[0] - k * n[0], d[1] - k * n[1]


def draw_laser(frame, stage, origin, d, dash_offset=0.0):
    """LaserSight.Trace: from the barrel tip to where the line meets the surface, the bounce icon just off it, then a tail
    fading in four steps along the ricochet. It stops dead on a target."""
    dist, centre, normal, target = stage.cast(origin, d, SHOT_RADIUS)
    start = (origin[0] + d[0] * BARREL, origin[1] + d[1] * BARREL)
    colour, alpha = LASER[:3], LASER[3]
    if centre is None:
        frame.line(start, (origin[0] + d[0] * 300, origin[1] + d[1] * 300), colour, alpha, dash_offset)
        return
    if target is not None:
        frame.line(start, centre, colour, alpha, dash_offset)
        return
    reach = min(SHOT_RADIUS / -(d[0] * normal[0] + d[1] * normal[1]), SHOT_RADIUS * 4)
    impact = (centre[0] + d[0] * reach, centre[1] + d[1] * reach)
    frame.line(start, impact, colour, alpha, dash_offset)
    bounced = reflect(d, normal)
    nxt = stage.cast((centre[0] + normal[0] * 0.05, centre[1] + normal[1] * 0.05), bounced, SHOT_RADIUS, TAIL)
    tail = nxt[0]
    piece = tail / 4
    for i in range(4):
        a = (impact[0] + bounced[0] * piece * i, impact[1] + bounced[1] * piece * i)
        b = (impact[0] + bounced[0] * piece * (i + 1), impact[1] + bounced[1] * piece * (i + 1))
        frame.line(a, b, colour, alpha * (1 - (i + 0.5) / 4), dash_offset + math.hypot(impact[0] - start[0], impact[1] - start[1]) + piece * i)
    return (impact[0] + normal[0] * 0.32 * PPU, impact[1] + normal[1] * 0.32 * PPU)


def cursor_on_surface(stage, origin, d):
    """Where the mouse is: on the spot of floor or wall the player points at, just past where the line meets it."""
    dist, centre, normal, target = stage.cast(origin, d, 0.0)
    dist = dist if centre is not None else 60.0
    return origin[0] + d[0] * (dist + 4), origin[1] + d[1] * (dist + 4)


def draw_bounce_icon(frame, at):
    if at is not None:
        frame.blit(BOUNCE, at[0], at[1], tint=BOUNCE_ICON)


# ---- shots: simulated ahead of time, then drawn per frame


class Shot:
    """A paintball fired at time t0: its path, where it bounced, what it hit. Player shots turn live on the first bounce."""

    def __init__(self, stage, t0, origin, d, enemy=False, live=False, speed=SHOT_SPEED, max_bounces=3, lifetime=1.6):
        self.t0 = t0
        self.enemy = enemy
        self.radius = 4.0 if enemy else SHOT_RADIUS
        self.events = []            # (time, kind, point): 'arm', 'bounce', 'hit', 'splat'
        self.path = []              # (time, point, live)
        self.end = t0 + lifetime
        self.hit_target = None
        p = (origin[0] + d[0] * BARREL, origin[1] + d[1] * BARREL) if not enemy else origin
        bounces = 1 if live else 0
        t = t0
        self.path.append((t, p, bounces > 0))
        while t < self.end:
            dist, centre, normal, target = stage.cast(p, d, self.radius, 1000)
            t_hit = t + dist / speed
            if centre is None or t_hit >= self.end:
                break
            t = t_hit
            p = centre
            if target is not None:
                self.path.append((t, p, bounces > 0))
                self.events.append((t, 'hit', p, bounces > 0))
                self.hit_target = target
                self.end = t
                break
            contact = (p[0] - normal[0] * self.radius, p[1] - normal[1] * self.radius)
            if bounces >= max_bounces:
                self.path.append((t, p, True))
                self.end = t
                break
            self.path.append((t, p, bounces > 0))
            bounces += 1
            d = reflect(d, normal)
            self.events.append((t, 'arm' if bounces == 1 else 'bounce', contact, True))
        else:
            pass
        if self.path[-1][0] < self.end:
            last_t, last_p, last_live = self.path[-1]
            s = (self.end - last_t) * speed
            self.path.append((self.end, (last_p[0] + d[0] * s, last_p[1] + d[1] * s), bounces > 0))
        self.final_live = bounces > 0

    def at(self, t):
        """(point, live) at time t, or None outside its flight."""
        if t < self.t0 or t > self.end:
            return None
        for (ta, pa, la), (tb, pb, lb) in zip(self.path, self.path[1:]):
            if ta <= t <= tb:
                k = 0 if tb == ta else (t - ta) / (tb - ta)
                return (pa[0] + (pb[0] - pa[0]) * k, pa[1] + (pb[1] - pa[1]) * k), (la if k < 1 else lb) if tb > ta else lb
        return self.path[-1][1], self.final_live

    def live_at(self, t):
        arm = [e for e in self.events if e[1] == 'arm']
        return bool(arm) and t >= arm[0][0] or (self.enemy and not arm and self.path[0][2])

    def draw(self, frame, t):
        here = self.at(t)
        if here is None:
            return
        point, _ = here
        live = self.live_at(t) or (self.path[0][2])
        if self.enemy:
            ball, glow = BALL_ENEMY_LIVE, BALL_ENEMY_LIVE_GLOW
        else:
            ball, glow = (BALL_LIVE, BALL_LIVE_GLOW) if live else (BALL_FRESH, None)
        if live:
            # After-image trail: ghosts of the ball fading out behind it (SpriteFx.Ghost), spaced along the path.
            for k in range(1, 5):
                back = self.at(t - k * 0.022)
                arm_t = next((e[0] for e in self.events if e[1] == 'arm'), self.t0)
                if back is None or (not self.path[0][2] and t - k * 0.022 < arm_t):
                    continue
                frame.blit(ball, back[0][0], back[0][1], alpha=0.45 * (1 - k / 5.0))
            age = t - self.t0
            frame.blit(glow, point[0], point[1], alpha=0.38 + 0.12 * math.sin(age * 40))
        frame.blit(ball, point[0], point[1])

    def draw_effects(self, frame, t):
        for (te, kind, p, _) in self.events:
            if kind == 'arm' and not self.enemy and te <= t < te + ARM_FLASH.duration:
                frame.blit(ARM_FLASH.at(t - te), p[0], p[1])


# ---- magazine HUD (GameBootstrap.Magazine), at the picture's scale

PIP = 10
PIP_GAP = 4
PIP_INSET = 4


def pip_sprites():
    """10 px versions of ui_ammo_full / ui_ammo_empty (14 px), redrawn so they keep their outline at this size."""
    o, s, h, c = (0x14, 0x16, 0x1c), (0xc2, 0x52, 0x0a), (0xff, 0xd0, 0x8a), (0xff, 0x8a, 0x1f)
    r, f = (0x5f, 0x68, 0x78), (0x44, 0x4b, 0x58)
    full = [
        '...oooo...',
        '..osssso..',
        '.oshhssso.',
        'oshhhcccso',
        'oshhccccco',
        'osscccccco',
        'osscccccco',
        '.osccccco.',
        '..occcco..',
        '...oooo...',
    ]
    empty = [
        '...oooo...',
        '..orrrro..',
        '.orffffro.',
        'orffffffro',
        'offfffffro',
        'offfffffro',
        'orffffffro',
        '.orffffro.',
        '..orrrro..',
        '...oooo...',
    ]
    palette = {'o': o, 's': s, 'h': h, 'c': c, 'r': r, 'f': f}

    def build(rows):
        im = Image.new('RGBA', (10, 10))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch != '.':
                    im.putpixel((x, y), palette[ch] + (255,))
        return im
    return build(full), build(empty)


PIP_FULL, PIP_EMPTY = pip_sprites()


def magazine(frame, x, y_top, rounds, ammo, reload_progress=None):
    """Six pips on a blue panel, emptied right to left. Reloading, the panel turns red and fills blue from the left,
    filling each pip back in as the edge passes it. x, y_top in image pixels."""
    w = rounds * PIP + (rounds - 1) * PIP_GAP + PIP_INSET * 2
    h = PIP + PIP_INSET * 2
    sy0 = H - y_top - h
    if reload_progress is not None:
        frame.fill(x, sy0, x + w, sy0 + h, MAG_RED[0], MAG_RED[1])
        fill_x = x + w * reload_progress
    else:
        fill_x = x + w
    frame.fill(x, sy0, fill_x, sy0 + h, MAG_BLUE[0], MAG_BLUE[1])
    for i in range(rounds):
        px = x + PIP_INSET + i * (PIP + PIP_GAP)
        py = y_top + PIP_INSET
        if reload_progress is None:
            frame.im.alpha_composite(PIP_FULL if i < ammo else PIP_EMPTY, (px, py))
            continue
        frame.im.alpha_composite(PIP_EMPTY, (px, py))
        filled = int(math.floor(max(0.0, min(1.0, (fill_x - px) / PIP)) * PIP))
        if filled > 0:
            frame.im.alpha_composite(PIP_FULL.crop((0, 0, filled, PIP)), (px, py))
    return w, h


# ---- scenes


def timeline(duration):
    n = int(round(duration * FPS))
    return [i * DT for i in range(n)]


def ease_keys(t, keys):
    """Value at t from (time, value) keys, eased (cosine) between neighbours and held past the ends."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (ta, a), (tb, b) in zip(keys, keys[1:]):
        if ta <= t < tb:
            k = (t - ta) / (tb - ta)
            return a + (b - a) * (0.5 - 0.5 * math.cos(math.pi * k))
    return keys[-1][1]


def scene_walk():
    """A/D: walks right across the frame, turns, walks back."""
    floor, speed = 10, 30.0
    x0, x1 = 24, 76
    leg = (x1 - x0) / speed
    pause = 0.35
    total = 2 * (leg + pause)
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        if t < pause:
            state, x, right, ta, key = 'idle', x0, True, t, None
        elif t < pause + leg:
            state, x, right, ta, key = 'walk', x0 + (t - pause) * speed, True, t - pause, 'D'
        elif t < 2 * pause + leg:
            state, x, right, ta, key = 'idle', x1, False, t - pause - leg, None
        else:
            state, x, right, ta, key = 'walk', x1 - (t - 2 * pause - leg) * speed, False, t - 2 * pause - leg, 'A'
        sprite = player_frame(state, ta)
        draw_character(f, sprite, round(x), floor, right)
        draw_gun(f, PISTOL, hands_at(sprite, round(x), floor, right), 0 if right else 180, right)
        x_key = 3
        x_key += keycap(f, x_key, 3, 'A', key == 'A', ACCENT_L1) + 2
        keycap(f, x_key, 3, 'D', key == 'D', ACCENT_L1)
        frames.append(f)
    return frames


def scene_sprint():
    """Shift: walks in, then holds Shift and breaks into a run, faster, out of the frame."""
    floor = 10
    walk_speed, run_speed = 28.0, 50.0
    x_start, x_run, x_end = -16, 34, 124
    t_run = (x_run - x_start) / walk_speed
    t_end = t_run + (x_end - x_run) / run_speed
    total = t_end + 0.25
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprinting = False
        if t < t_run:
            state, x, ta = 'walk', x_start + t * walk_speed, t
        elif t < t_end:
            state, x, ta, sprinting = 'run', x_run + (t - t_run) * run_speed, t - t_run, True
        else:
            state, x, ta = None, None, 0
        if state:
            sprite = player_frame(state, ta)
            draw_character(f, sprite, round(x), floor)
            draw_gun(f, PISTOL, hands_at(sprite, round(x), floor), 0)
        keycap(f, 3, 3, 'SHIFT', sprinting, ACCENT_L1)
        frames.append(f)
    return frames


def scene_jump():
    """W: a live (red, bounced) enemy shot skims in low from the right; the player jumps it and lands."""
    floor = 6
    x = 30
    peak, air = 19.0, 0.62
    g = 8 * peak / air ** 2
    v0 = g * air / 2
    stage = Stage(floor)
    t_shot = 0.45
    shot_speed = 160.0
    shot_y = floor + 5
    t_cross = t_shot + (104 - x) / shot_speed
    t_jump = t_cross - air / 2
    shot = Shot(stage, t_shot, (104, shot_y), (-1, 0), enemy=True, live=True, speed=shot_speed, lifetime=1.2)
    total = 2.2
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        key = False
        if t < t_jump:
            sprite, y = player_frame('idle', t), floor
        elif t < t_jump + air:
            ta = t - t_jump
            y = floor + v0 * ta - 0.5 * g * ta * ta
            vy = v0 - g * ta
            sprite = player_frame('rise', ta) if vy > 0.1 * PPU else player_frame('fall', ta)
            key = ta < 0.25
        elif t < t_jump + air + 0.12:
            sprite, y = player_frame('land', t - t_jump - air), floor
        else:
            sprite, y = player_frame('idle', t - t_jump - air - 0.12), floor
        draw_character(f, sprite, x, round(y))
        draw_gun(f, PISTOL, hands_at(sprite, x, round(y)), 0)
        shot.draw(f, t)
        keycap(f, 3, 3, 'W', key, ACCENT_L1)
        frames.append(f)
    return frames


def scene_duck():
    """S: behind a lab bench, the player ducks under a live enemy shot coming in at head height, then stands."""
    floor = 10
    x = 30
    bench_x = 87  # centre; the bench runs 69..105, just clear of the barrel
    stage = Stage(floor)
    t_duck, t_stand = 0.45, 1.75
    shot = Shot(stage, 0.55, (104, floor + 40), (-1, 0), enemy=True, live=True, speed=160.0, lifetime=1.0)
    total = 2.4
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        f.blit(BENCH, bench_x, floor + BENCH.h / 2)
        ducking = t_duck <= t < t_stand
        sprite = player_frame('duck', t - t_duck) if ducking else player_frame('idle', t)
        draw_character(f, sprite, x, floor)
        draw_gun(f, PISTOL, hands_at(sprite, x, floor), 0)
        shot.draw(f, t)
        keycap(f, 3, 3, 'S', ducking, ACCENT_L1)
        frames.append(f)
    return frames


def scene_aim():
    """Mouse: the gun follows the cursor; the laser runs to the first bounce (floor or wall) and a little past it."""
    floor = 10
    x = 20
    stage = Stage(floor, wall_x=91)
    total = 2.5  # three breathing cycles, so the loop is seamless
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        f.wall(91)
        sprite = player_frame('idle', t)
        angle = -33 + 20 * math.sin(2 * math.pi * t / total)
        hands = hands_at(sprite, x, floor)
        d = direction(angle)
        draw_character(f, sprite, x, floor)
        draw_gun(f, PISTOL, hands, angle)
        icon = draw_laser(f, stage, hands, d)
        draw_bounce_icon(f, icon)
        f.blit(CROSSHAIR, *cursor_on_surface(stage, hands, d))
        frames.append(f)
    return frames


def scene_shoot():
    """Left click: fires where the gun points; the shot bounces off the floor, turns live and flies on."""
    floor = 10
    x = 16
    stage = Stage(floor)
    total = 2.5
    shots_at = [(0.35, -32.0), (1.6, -46.0)]
    frames = []
    shots = []
    for t0, angle in shots_at:
        sprite = player_frame('idle', t0)
        shots.append(Shot(stage, t0, hands_at(sprite, x, floor), direction(angle)))

    (ta, a), (tb, b) = shots_at
    # Holds on each shot's angle, easing from one to the next in between, and back to the first by the loop's end.
    keys = [(0, a), (ta + 0.5, a), (tb - 0.25, b), (tb + 0.5, b), (total - 0.1, a)]

    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprite = player_frame('idle', t)
        hands = hands_at(sprite, x, floor)
        angle = ease_keys(t, keys)
        d = direction(angle)
        draw_character(f, sprite, x, floor)
        draw_gun(f, PISTOL, hands, angle)
        icon = draw_laser(f, stage, hands, d)
        draw_bounce_icon(f, icon)
        clicked = False
        for s in shots:
            for tt in (t, t + total, t - total):
                s.draw(f, tt)
                s.draw_effects(f, tt)
            if 0 <= t - s.t0 < 0.15:
                clicked = True
        f.blit(CROSSHAIR, *cursor_on_surface(stage, hands, d))
        mouse_icon(f, 3, 3, clicked, ACCENT_L1)
        frames.append(f)
    return frames


def scene_pistol():
    """The pistol on its own, at the game's pixel size (2x here): one click, one shot."""
    gun = handless_pistol()
    scale = 2
    big = Sprite(gun.image.resize((gun.w * scale, gun.h * scale), Image.NEAREST), (gun.pivot[0] * scale, gun.pivot[1] * scale))
    ball = Sprite(BALL_FRESH.image.resize((14, 14), Image.NEAREST), (7, 7))
    gx, gy = 22, 40
    muzzle = gx + (34 - gun.pivot[0] - 2) * scale
    clicks = [0.3, 1.25, 1.65]
    total = 2.6
    speed = 200.0
    frames = []
    for t in timeline(total):
        f = Frame()
        kick = any(0 <= t - c < 0.1 for c in clicks)
        f.blit(big, gx - (2 if kick else 0), gy)
        for c in clicks:
            age = t - c
            if 0 <= age < 0.5:
                f.blit(ball, muzzle + age * speed, gy + 1)
        mouse_icon(f, W - 13, H - 15, any(0 <= t - c < 0.15 for c in clicks), ACCENT_L1)
        frames.append(f)
    return frames


def scene_magazine():
    """The magazine panel over the floor strip, bottom left, as in the HUD: six shots empty it right to left, then it
    turns red and refills from the left while the gun reloads by itself."""
    floor = 25
    x = 18
    stage = Stage(floor)
    interval = 0.32
    t_first = 0.3
    reload_time = 2.0
    t_reload = t_first + 5 * interval + 0.05
    total = t_reload + reload_time + 0.6
    fires = [t_first + i * interval for i in range(6)]
    sprite0 = player_frame('idle', 0)
    hands0 = hands_at(sprite0, x, floor)
    shots = [Shot(stage, t0, hands0, (1, 0), lifetime=0.5) for t0 in fires]
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprite = player_frame('idle', t)
        hands = hands_at(sprite, x, floor)
        draw_character(f, sprite, x, floor)
        reloading = t_reload <= t < t_reload + reload_time
        if reloading:
            k = (t - t_reload) / reload_time
            frame = RELOAD_PISTOL.frames[min(len(RELOAD_PISTOL.frames) - 1, int(k * len(RELOAD_PISTOL.frames)))]
            draw_gun(f, frame, hands, 28 * math.sin(k * math.pi))
        else:
            draw_gun(f, PISTOL, hands, 0)
        for s in shots:
            s.draw(f, t)
        ammo = 6 - sum(1 for t0 in fires if t >= t0)
        if t >= t_reload + reload_time:
            ammo = 6
        magazine(f, 3, H - 3 - (PIP + 2 * PIP_INSET), 6, ammo, (t - t_reload) / reload_time if reloading else None)
        frames.append(f)
    return frames


def scene_ricochet():
    """A direct hit splashes green and does nothing; a shot off the floor turns live and takes the enemy out."""
    floor = 10
    px, ex = 11, 89
    enemy_box = (ex - HITBOX[0] / 2, floor, ex + HITBOX[0] / 2, floor + HITBOX[1])
    stage = Stage(floor, targets=[enemy_box])
    empty_stage = Stage(floor)
    hands = hands_at(player_frame('idle', 0), px, floor)

    # The angle whose floor bounce meets the enemy's chest.
    def lands(angle):
        s = Shot(stage, 0, hands, direction(angle))
        return s.hit_target == 0 and s.final_live, s
    best = None
    for a10 in range(-300, -700, -5):
        ok, s = lands(a10 / 10)
        if ok:
            hit = [e for e in s.events if e[1] == 'hit'][0][2]
            score = abs(hit[1] - (floor + 30))
            if best is None or score < best[0]:
                best = (score, a10 / 10)
    bank = best[1]

    t_direct, t_bank = 0.5, 2.0
    direct = Shot(stage, t_direct, hands, (1, 0))
    banked = Shot(stage, t_bank, hands, direction(bank))
    t_direct_hit = direct.end
    t_kill = banked.end
    kill_point = banked.path[-1][1]
    death = ENEMY['death']
    t_gone = t_kill + death.duration + 0.6
    t_back = t_gone + 0.35
    total = t_back + 0.5

    def aim(t):
        if t < t_direct + 0.45:
            return 0.0
        if t < t_bank - 0.2:
            k = (t - t_direct - 0.45) / (t_bank - 0.2 - t_direct - 0.45)
            return bank * (0.5 - 0.5 * math.cos(math.pi * k))
        if t < t_kill + 0.6:
            return bank
        if t < t_back:
            k = (t - t_kill - 0.6) / (t_back - t_kill - 0.6)
            return bank * (0.5 + 0.5 * math.cos(math.pi * k))
        return 0.0

    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprite = player_frame('idle', t)
        h = hands_at(sprite, px, floor)
        angle = aim(t)
        alive = t < t_kill or t >= t_gone
        draw_character(f, sprite, px, floor)
        draw_gun(f, PISTOL, h, angle)
        # The enemy: idle facing the player, or its death playing out where the shot took it, then back in.
        if t < t_kill:
            es = ENEMY['idle'].at(t)
            draw_character(f, es, ex, floor, right=False)
            draw_gun(f, ENEMY_GUN, hands_at(es, ex, floor, right=False), 180, right=False)
        elif t < t_gone:
            age = t - t_kill
            alpha = 1.0 if age < death.duration + 0.3 else max(0.0, 1 - (age - death.duration - 0.3) / 0.3)
            draw_character(f, death.at(age), ex, floor, right=False, alpha=alpha)
        else:
            alpha = min(1.0, (t - t_gone) / 0.35)
            es = ENEMY['idle'].at(t)
            draw_character(f, es, ex, floor, right=False, alpha=alpha)
            draw_gun(f, ENEMY_GUN, hands_at(es, ex, floor, right=False), 180, right=False, alpha=alpha)
        icon = draw_laser(f, stage if alive else empty_stage, h, direction(angle))
        draw_bounce_icon(f, icon)
        direct.draw(f, t)
        banked.draw(f, t)
        banked.draw_effects(f, t)
        if t_direct_hit <= t < t_direct_hit + BLOCKED_SPLAT.duration:
            p = direct.path[-1][1]
            f.blit(BLOCKED_SPLAT.at(t - t_direct_hit), p[0], p[1])
        if t_kill <= t < t_kill + BURST.duration:
            centre = (ex, floor + HITBOX[1] / 2)
            f.blit(BURST.at(t - t_kill), (centre[0] + kill_point[0]) / 2, (centre[1] + kill_point[1]) / 2)
        frames.append(f)
    return frames


def scene_twin_gun():
    """Walks into the floating twin gun: a gold flash, and it's the player's."""
    floor = 10
    pickup = (68, floor + 24)
    speed = 30.0
    x_start, x_stop = -14, 52
    reach_x = HITBOX[0] / 2 + 0.9 * PPU / 2
    t_take = (pickup[0] - reach_x - x_start) / speed
    t_stop = (x_stop - x_start) / speed
    t_fade = t_stop + 0.9
    total = t_fade + 0.3
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        if t < t_take:
            bob = math.sin(t * 3) * 0.08 * PPU
            glow = 0.3 + 0.15 * math.sin(t * 4.5)
            f.blit(PICKUP_GLOW, pickup[0], pickup[1], tint=GOLD + (glow,))
            centre = ((TWIN_PICKUP.w / 2 - TWIN_PICKUP.pivot[0]), 0)
            f.blit(TWIN_PICKUP, pickup[0] - centre[0], pickup[1] + round(bob))
        if t < t_stop:
            x = x_start + t * speed
            sprite = player_frame('walk', t)
        else:
            x = x_stop
            sprite = player_frame('idle', t - t_stop)
        draw_character(f, sprite, round(x), floor)
        draw_gun(f, PISTOL, hands_at(sprite, round(x), floor), 0)
        if t_take <= t < t_take + ARM_FLASH.duration:
            f.blit(ARM_FLASH.at(t - t_take), pickup[0], pickup[1], tint=GOLD)
        if t >= t_fade:
            f.fade((t - t_fade) / 0.3)
        elif t < 0.2:
            f.fade(1 - t / 0.2)
        frames.append(f)
    return frames


def twin_directions(angle):
    d = direction(angle)
    return rotate(d, 2.0), rotate(d, -2.0)


def scene_switch():
    """F: the pistol's barrel draws back into the grip and the twin gun's slide out; neither laser shows meanwhile."""
    floor = 10
    x = 22
    angle = -32.0
    stage = Stage(floor)
    total = 10 / 3  # four breathing cycles
    switches = [(0.8, 'twin'), (2.5, 'pistol')]
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprite = player_frame('idle', t)
        hands = hands_at(sprite, x, floor)
        draw_character(f, sprite, x, floor)
        gun, extension, switching = 'pistol', 1.0, False
        for ts, to in switches:
            if t >= ts:
                gun = to
            if ts <= t < ts + 0.5:
                k = (t - ts) / 0.5
                switching = True
                gun = to if k >= 0.5 else ('pistol' if to == 'twin' else 'twin')
                extension = abs(1 - 2 * k)
        draw_gun(f, TWIN if gun == 'twin' else PISTOL, hands, angle, scale_x=extension)
        if not switching:
            if gun == 'twin':
                top, bottom = twin_directions(angle)
                draw_bounce_icon(f, draw_laser(f, stage, hands, top))
                draw_bounce_icon(f, draw_laser(f, stage, hands, bottom))
            else:
                draw_bounce_icon(f, draw_laser(f, stage, hands, direction(angle)))
        keycap(f, 3, 3, 'F', switching, ACCENT_L6)
        frames.append(f)
    return frames


def scene_twin_shot():
    """Left click with the twin gun: two shots fan out, one per barrel, each with its own laser."""
    floor = 10
    x = 14
    angle = -28.0
    stage = Stage(floor)
    total = 2.5
    fires = [0.4, 1.6]
    sprite0 = player_frame('idle', 0)
    shots = []
    for t0 in fires:
        hands = hands_at(player_frame('idle', t0), x, floor)
        top, bottom = twin_directions(angle)
        shots += [Shot(stage, t0, hands, top), Shot(stage, t0, hands, bottom)]
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprite = player_frame('idle', t)
        hands = hands_at(sprite, x, floor)
        draw_character(f, sprite, x, floor)
        draw_gun(f, TWIN, hands, angle)
        top, bottom = twin_directions(angle)
        draw_bounce_icon(f, draw_laser(f, stage, hands, top))
        draw_bounce_icon(f, draw_laser(f, stage, hands, bottom))
        for s in shots:
            s.draw(f, t)
            s.draw_effects(f, t)
        mouse_icon(f, 3, 3, any(0 <= t - t0 < 0.15 for t0 in fires), ACCENT_L6)
        frames.append(f)
    return frames


def scene_top_barrel():
    """The magazine with three rounds: a twin shot leaves one, the bottom laser goes out, and the next click fires only
    the top barrel. Then the magazine refills."""
    floor = 25
    x = 18
    stage = Stage(floor)
    t_fade_in = 0.2
    fires = [0.45, 1.5]
    reload_time = 2.0
    t_reload = fires[1] + 0.05
    t_full = t_reload + reload_time
    total = t_full + 0.55
    hands0 = hands_at(player_frame('idle', 0), x, floor)
    top, bottom = twin_directions(0)
    shots = [Shot(stage, fires[0], hands0, top, lifetime=0.5), Shot(stage, fires[0], hands0, bottom, lifetime=0.5),
             Shot(stage, fires[1], hands0, top, lifetime=0.5)]
    frames = []
    for t in timeline(total):
        f = Frame()
        f.floor(floor)
        sprite = player_frame('idle', t)
        hands = hands_at(sprite, x, floor)
        draw_character(f, sprite, x, floor)
        reloading = t_reload <= t < t_full
        ammo = 3 if t < fires[0] else 1 if t < fires[1] else 0
        if t >= t_full:
            ammo = 6
        if reloading:
            k = (t - t_reload) / reload_time
            frame = RELOAD_TWIN.frames[min(len(RELOAD_TWIN.frames) - 1, int(k * len(RELOAD_TWIN.frames)))]
            draw_gun(f, frame, hands, 28 * math.sin(k * math.pi))
        else:
            draw_gun(f, TWIN, hands, 0)
            draw_laser(f, stage, hands, top)
            if ammo != 1:
                draw_laser(f, stage, hands, bottom)
        for s in shots:
            s.draw(f, t)
        magazine(f, 3, H - 3 - (PIP + 2 * PIP_INSET), 6, ammo, k if reloading else None)
        mouse_icon(f, W - 13, 3, any(0 <= t - t0 < 0.15 for t0 in fires), ACCENT_L6)
        if t >= total - 0.25:
            f.fade((t - (total - 0.25)) / 0.25)
        elif t < t_fade_in:
            f.fade(1 - t / t_fade_in)
        frames.append(f)
    return frames


SCENES = {
    'walk': scene_walk,
    'sprint': scene_sprint,
    'jump': scene_jump,
    'duck': scene_duck,
    'aim': scene_aim,
    'shoot': scene_shoot,
    'pistol': scene_pistol,
    'magazine': scene_magazine,
    'ricochet': scene_ricochet,
    'twin_gun': scene_twin_gun,
    'switch_guns': scene_switch,
    'twin_shot': scene_twin_shot,
    'top_barrel': scene_top_barrel,
}


# ---- output


def export(name, frames):
    """Writes the distinct frames as a grid sheet plus the play order, and a 2x GIF of the loop."""
    unique, sequence, seen = [], [], {}
    for f in frames:
        key = f.im.tobytes()
        if key not in seen:
            seen[key] = len(unique)
            unique.append(f.im)
        sequence.append(seen[key])
    cols = min(GRID_COLUMNS, len(unique))
    rows = (len(unique) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * W, rows * H))
    for i, im in enumerate(unique):
        sheet.paste(im, ((i % cols) * W, (i // cols) * H))
    rel = 'UI/Tutorial/tut_' + name
    os.makedirs(os.path.join(ART, 'UI/Tutorial'), exist_ok=True)
    sheet.save(os.path.join(ART, rel + '.png'))
    SLICES[rel] = {'frame': [W, H], 'pivot': [0.5, 0.5], 'fps': FPS, 'mesh': 'FullRect', 'sequence': sequence}

    os.makedirs(GIF_DIR, exist_ok=True)
    big = [f.im.convert('RGB').resize((W * 2, H * 2), Image.NEAREST) for f in frames]
    palette_src = Image.new('RGB', (W * 2, H * 2 * min(len(big), 16)))
    for i, im in enumerate(big[::max(1, len(big) // 16)][:16]):
        palette_src.paste(im, (0, i * H * 2))
    pal = palette_src.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    gif = [im.quantize(palette=pal, dither=Image.Dither.NONE) for im in big]
    gif[0].save(os.path.join(GIF_DIR, 'tut_%s.gif' % name), save_all=True, append_images=gif[1:],
                duration=int(1000 / FPS), loop=0, disposal=1, optimize=False)
    print('%-12s %3d frames, %3d distinct, %.2f s' % (name, len(frames), len(unique), len(frames) / FPS))


def main():
    for name, build in SCENES.items():
        if ONLY and name not in ONLY:
            continue
        export(name, build())
    with open(os.path.join(ART, 'art_slices.json'), 'w') as f:
        json.dump(SLICES, f, indent=1, sort_keys=True)


if __name__ == '__main__':
    main()
