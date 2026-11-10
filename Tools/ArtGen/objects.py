"""Hand-authored modern infrastructure pixel art, as character grids.

Each object is a grid of single-character palette keys ('.' = transparent). The grids are packed into one atlas recipe
for PixelLab's pixelart_workbench (draw --recipe), and can also be previewed locally.
Usage: objects.py preview <out.png> | recipe <out.json> | layout <out.json>
"""
import json
import random
import sys

PAL = {
    'o': '#14161c',
    'a': '#8f97a6', 'b': '#b9c0cc', 'c': '#dde2e9', 'd': '#f4f6f9',      # laminate
    'e': '#1f232b', 'f': '#2e333d', 'g': '#444b58', 'h': '#5f6878',      # graphite
    'i': '#5d6573', 'j': '#8a93a2', 'k': '#b8c0cc', 'l': '#e2e7ee',      # aluminium
    'm': '#5a7f8f', 'n': '#7fa6b5', 'p': '#a9cdd8', 'q': '#dff1f6',      # frosted glass
    'r': '#4a4f57', 's': '#636a74', 't': '#7f8792', 'u': '#a0a8b2',      # concrete
    'y': '#9c7a12', 'z': '#e0b030', 'A': '#f5d670',                      # safety yellow
    'B': '#ffe9a8', 'C': '#fffbe8',                                      # LED
    'D': '#ffffff', 'E': '#d0d4da', 'F': '#a8aeb8',                      # tint masks
    'G': '#3f8cff', 'H': '#2357b8', 'I': '#8cc2ff',                      # player accent (recoloured per team)
    'N': '#3a6fd8', 'O': '#d84a4a', 'P': '#3aa860',                      # whiteboard marker ink
}


class Grid:
    def __init__(self, w, h, fill='.'):
        self.w, self.h = w, h
        self.g = [[fill] * w for _ in range(h)]

    def px(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.g[y][x] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, c)

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)

    def border(self, c='o'):
        self.hline(0, self.w - 1, 0, c)
        self.hline(0, self.w - 1, self.h - 1, c)
        self.vline(0, 0, self.h - 1, c)
        self.vline(self.w - 1, 0, self.h - 1, c)

    def bevel(self, x0, y0, x1, y1, fill, light, dark):
        self.rect(x0, y0, x1, y1, fill)
        self.hline(x0, x1, y0, light)
        self.vline(x0, y0, y1, light)
        self.hline(x0, x1, y1, dark)
        self.vline(x1, y0, y1, dark)

    def speckle(self, x0, y0, x1, y1, colours, density, seed):
        rnd = random.Random(seed)
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if rnd.random() < density:
                    self.px(x, y, rnd.choice(colours))

    def outline_alpha(self, c='o'):
        """Outline every opaque pixel that touches transparency (used on irregular shapes)."""
        src = [row[:] for row in self.g]
        for y in range(self.h):
            for x in range(self.w):
                if src[y][x] == '.':
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < self.w and 0 <= ny < self.h) or src[ny][nx] == '.':
                        self.g[y][x] = c
                        break

    def rows(self):
        return [''.join(r) for r in self.g]


# ---------------------------------------------------------------- objects

def lab_bench():
    g = Grid(36, 24, 'c')
    # graphite epoxy worktop with aluminium edge trim
    g.hline(1, 34, 1, 'h')
    g.rect(1, 2, 34, 2, 'g')
    for x in (4, 5, 14, 26, 27):
        g.px(x, 2, 'h')
    g.hline(1, 34, 3, 'f')
    g.hline(1, 34, 4, 'k')
    g.hline(1, 34, 5, 'e')
    # three white laminate units: drawers, door, door
    g.bevel(1, 6, 11, 12, 'c', 'd', 'b')
    g.bevel(1, 13, 11, 19, 'c', 'd', 'b')
    g.bevel(13, 6, 23, 19, 'c', 'd', 'b')
    g.bevel(25, 6, 34, 19, 'c', 'd', 'b')
    g.vline(12, 6, 19, 'a')
    g.vline(24, 6, 19, 'a')
    # aluminium bar pulls
    for y in (8, 15):
        g.hline(4, 8, y, 'k')
        g.hline(4, 8, y + 1, 'i')
    for x in (21, 27):
        g.vline(x, 9, 15, 'k')
        g.vline(x + 1, 9, 15, 'i')
    # recessed toe kick, brushed aluminium plate
    g.hline(1, 34, 20, 'e')
    g.hline(1, 34, 21, 'j')
    g.hline(1, 34, 22, 'i')
    g.border()
    return g


def ceiling_duct():
    g = Grid(36, 24, 'k')
    g.hline(1, 34, 1, 'f')
    g.hline(1, 34, 2, 'g')
    for y in (4, 7, 15):
        g.hline(2, 33, y, 'j')                     # brushed lines
    g.vline(1, 3, 17, 'l')
    g.vline(34, 3, 17, 'i')
    # vent slots
    for row, off in ((9, 0), (11, 3), (13, 0)):
        for x in range(4 + off, 32, 6):
            g.hline(x, min(x + 2, 32), row, 'f')
            g.hline(x, min(x + 2, 32), row + 1, 'l')
    g.hline(1, 34, 18, 'i')
    # linear LED diffuser on the underside
    g.rect(1, 19, 34, 21, 'j')
    g.rect(3, 19, 32, 20, 'C')
    g.hline(3, 32, 21, 'B')
    g.hline(1, 34, 22, 'g')
    g.border()
    return g


def floor_tile():
    g = Grid(60, 30, 's')
    g.hline(0, 59, 0, 'o')
    g.hline(0, 59, 1, 'u')
    g.rect(0, 2, 59, 5, 't')
    # glossy reflections on the polished surface
    for x0 in (6, 33, 47):
        for i in range(4):
            g.px(x0 + i, 2 + (i % 2), 'u')
    g.hline(0, 59, 6, 'r')
    g.speckle(0, 7, 59, 29, 'rtt', 0.06, 11)
    g.vline(0, 1, 29, 'r')                         # expansion joint every 2 m
    g.vline(1, 2, 29, 'u')
    return g


def ceiling_tile():
    g = Grid(60, 30, 'r')
    g.speckle(0, 0, 59, 17, 'sf', 0.07, 21)
    g.hline(0, 59, 18, 'e')
    # graphite micro-perforated acoustic panels (dark, so the HUD text over them stays readable)
    g.rect(0, 19, 59, 26, 'g')
    for y in (21, 23, 25):
        for x in range(2, 60, 3):
            g.px(x, y, 'f')
    g.hline(0, 59, 19, 'h')
    g.vline(0, 19, 27, 'e')
    g.vline(30, 19, 27, 'e')
    g.hline(0, 59, 27, 'f')
    g.hline(0, 59, 28, 'j')                         # aluminium T-bar
    for x0 in (7, 37):                              # recessed linear LEDs
        g.hline(x0, x0 + 15, 28, 'C')
        g.px(x0, 28, 'B')
        g.px(x0 + 15, 28, 'B')
    g.hline(0, 59, 29, 'o')
    return g


def wall_concrete():
    g = Grid(30, 60, 't')
    g.speckle(2, 0, 27, 59, 'su', 0.05, 31)
    g.vline(0, 0, 59, 'o')
    g.vline(29, 0, 59, 'o')
    g.vline(1, 0, 59, 'u')
    g.vline(28, 0, 59, 's')
    for y in (29, 59):                              # formwork joints
        g.hline(1, 28, y, 's')
    g.hline(1, 28, 0, 'u')
    g.hline(1, 28, 30, 'u')
    for x, y in ((7, 13), (21, 13), (7, 44), (21, 44)):   # tie holes
        g.rect(x, y, x + 1, y + 1, 'f')
        g.px(x + 2, y + 1, 'u')
        g.px(x + 1, y + 2, 'u')
    return g


def wall_partition():
    g = Grid(30, 60, 'c')
    g.vline(0, 0, 59, 'o')
    g.vline(29, 0, 59, 'o')
    g.vline(1, 0, 59, 'd')
    g.vline(28, 0, 59, 'a')
    g.vline(14, 0, 59, 'j')                          # aluminium reveal between panels
    g.vline(15, 0, 59, 'i')
    g.hline(1, 28, 59, 'a')
    g.hline(1, 28, 0, 'd')
    g.vline(27, 0, 59, 'b')
    g.vline(13, 0, 59, 'b')
    return g


def ledge():
    g = Grid(30, 15, 'g')
    g.hline(0, 29, 0, 'o')
    g.hline(1, 28, 1, 'l')
    g.hline(1, 28, 2, 'k')
    g.hline(1, 28, 3, 'i')
    g.hline(1, 28, 4, 'h')
    for x in (8, 20):                                # bolts
        g.px(x, 6, 'h')
        g.px(x, 7, 'e')
    g.hline(1, 28, 10, 'f')
    g.hline(1, 28, 11, 'C')
    g.px(2, 11, 'B')
    g.px(27, 11, 'B')
    g.hline(1, 28, 12, 'f')
    g.hline(1, 28, 13, 'e')
    g.hline(0, 29, 14, 'o')
    g.vline(0, 0, 14, 'o')
    g.vline(29, 0, 14, 'o')
    g.vline(1, 3, 13, 'h')
    g.vline(28, 3, 13, 'f')
    return g


def bunker_roof():
    g = Grid(30, 15, 't')
    g.speckle(2, 2, 27, 8, 'su', 0.06, 41)
    g.hline(0, 29, 0, 'o')
    g.hline(1, 28, 1, 'u')
    g.hline(1, 28, 9, 's')
    for y in (10, 11, 12):                           # hazard edge
        for x in range(1, 29):
            g.px(x, y, 'z' if ((x + y) // 3) % 2 == 0 else 'e')
    g.hline(1, 28, 13, 'f')
    g.hline(0, 29, 14, 'o')
    g.vline(0, 0, 14, 'o')
    g.vline(29, 0, 14, 'o')
    g.vline(1, 1, 9, 'u')
    g.vline(28, 1, 9, 'r')
    return g


def whiteboard():
    g = Grid(75, 12, 'd')
    g.hline(1, 73, 1, 'l')
    g.hline(1, 73, 2, 'k')
    g.hline(1, 73, 9, 'k')
    g.hline(1, 73, 10, 'j')
    g.vline(1, 1, 10, 'l')
    g.vline(2, 1, 10, 'k')
    g.vline(72, 1, 10, 'j')
    g.vline(73, 1, 10, 'i')
    # faint marker scribbles
    for x, y, c in ((8, 4, 'N'), (9, 4, 'N'), (10, 5, 'N'), (11, 5, 'N'), (12, 4, 'N'), (13, 4, 'N'),
                    (8, 6, 'N'), (9, 6, 'N'), (10, 6, 'N'), (40, 5, 'O'), (41, 5, 'O'), (42, 4, 'O'),
                    (43, 6, 'O'), (44, 6, 'O'), (58, 4, 'P'), (59, 5, 'P'), (60, 6, 'P'), (61, 5, 'P'), (62, 4, 'P')):
        g.px(x, y, c)
    for x in range(20, 33):
        g.px(x, 7, 'b')
    g.rect(52, 10, 55, 10, 'N')                       # marker on the tray
    g.border()
    return g


def door_glass():
    g = Grid(18, 75, 'n')
    g.rect(1, 1, 16, 2, 'l')
    g.hline(1, 16, 2, 'k')
    g.vline(1, 1, 73, 'l')
    g.vline(2, 1, 73, 'k')
    g.vline(15, 1, 73, 'E')                            # status LED channel (tinted overlay sits here)
    g.vline(16, 1, 73, 'i')
    # glass reflections and frosted privacy band
    for k in range(0, 12):
        g.px(3 + k, 20 - k, 'q')
        g.px(4 + k, 20 - k, 'p')
        g.px(3 + k, 50 - k, 'p')
    g.rect(3, 27, 14, 34, 'p')
    g.hline(3, 14, 27, 'q')
    # push bar
    g.hline(3, 14, 40, 'k')
    g.hline(3, 14, 41, 'i')
    # kick plate
    g.rect(3, 60, 14, 72, 'k')
    for y in range(61, 72, 2):
        g.hline(3, 14, y, 'j')
    g.hline(3, 14, 59, 'i')
    g.hline(1, 16, 73, 'i')
    g.border()
    return g


def door_steel():
    g = Grid(18, 75, 'g')
    g.vline(1, 1, 73, 'h')
    g.vline(15, 1, 73, 'E')
    g.vline(16, 1, 73, 'f')
    g.hline(1, 16, 1, 'h')
    for y in (24, 50):                                 # reinforcement bands
        g.hline(2, 14, y, 'k')
        g.hline(2, 14, y + 1, 'j')
        g.hline(2, 14, y + 2, 'i')
        for x in (4, 12):
            g.px(x, y + 1, 'l')
    # wired-glass vision panel
    g.rect(5, 7, 11, 18, 'n')
    for y in range(7, 19, 3):
        g.hline(5, 11, y, 'm')
    for x in range(5, 12, 3):
        g.vline(x, 7, 18, 'm')
    g.hline(4, 12, 6, 'o')
    g.hline(4, 12, 19, 'o')
    g.vline(4, 6, 19, 'o')
    g.vline(12, 6, 19, 'o')
    # lever handle
    g.hline(3, 7, 38, 'k')
    g.px(3, 39, 'i')
    # hazard foot band
    for y in range(62, 72):
        for x in range(2, 15):
            g.px(x, y, 'z' if ((x + y) // 3) % 2 == 0 else 'e')
    g.hline(1, 16, 73, 'f')
    g.border()
    return g


def door_exit():
    g = Grid(36, 66, 'n')
    g.rect(1, 1, 34, 2, 'k')
    g.hline(1, 34, 1, 'l')
    g.vline(1, 1, 64, 'l')
    g.vline(2, 1, 64, 'k')
    g.vline(33, 1, 64, 'j')
    g.vline(34, 1, 64, 'i')
    g.vline(17, 3, 64, 'j')                            # centre mullion
    g.vline(18, 3, 64, 'i')
    for x0 in (3, 19):
        for k in range(0, 13):
            g.px(x0 + k, 22 - k, 'q')
            g.px(x0 + k, 46 - k, 'p')
        g.hline(x0, x0 + 13, 34, 'k')                  # push bars
        g.hline(x0, x0 + 13, 35, 'i')
        g.rect(x0, 54, x0 + 13, 63, 'k')               # kick plates
        for y in range(55, 63, 2):
            g.hline(x0, x0 + 13, y, 'j')
    g.hline(1, 34, 64, 'i')
    g.border()
    return g


FONT = {  # 3x5
    'E': ['###', '#..', '##.', '#..', '###'],
    'X': ['#.#', '#.#', '.#.', '#.#', '#.#'],
    'I': ['###', '.#.', '.#.', '.#.', '###'],
    'T': ['###', '.#.', '.#.', '.#.', '.#.'],
}


def exit_sign():
    g = Grid(21, 9, 'f')
    g.border()
    g.hline(1, 19, 1, 'g')
    x = 3
    for ch in 'EXIT':
        for dy, row in enumerate(FONT[ch]):
            for dx, v in enumerate(row):
                if v == '#':
                    g.px(x + dx, 2 + dy, 'D')
        x += 4
    return g


def led_strip():
    g = Grid(1, 73, 'D')
    return g


def marker(length, barrels, accent, heavy=False):
    """Paintball marker along +x from the pivot (left edge, vertical centre). barrels: list of (y_center, thickness)."""
    h = 13 if heavy else (11 if len(barrels) > 1 else 9)
    g = Grid(length, h)
    cy = h // 2
    # barrels
    for bc, th in barrels:
        y0 = cy + bc - th // 2
        g.rect(9, y0, length - 1, y0 + th - 1, 'j')
        g.hline(9, length - 1, y0, 'k')
        g.hline(9, length - 1, y0 + th - 1, 'i')
        g.vline(length - 1, y0, y0 + th - 1, 'f')     # muzzle
    # receiver body
    g.rect(1, cy - 2, 11, cy + 2, 'g')
    g.hline(1, 11, cy - 2, 'h')
    g.hline(1, 11, cy + 2, 'f')
    # hopper (team colour) on top
    top = max(0, cy - 5)
    g.rect(3, top, 8, cy - 3, accent[0])
    g.hline(3, 8, top, accent[2])
    g.vline(8, top, cy - 3, accent[1])
    # grip
    g.rect(6, cy + 3, 8, h - 1, 'f')
    g.outline_alpha()
    return g


def splat(seed, size=18):
    """Paint splat decal: a lumpy blob with droplets. Colours: G core, H shade, I highlight (recoloured per paint)."""
    rnd = random.Random(seed)
    g = Grid(size, size)
    c = (size - 1) / 2
    lobes = [(c + rnd.uniform(-2.5, 2.5), c + rnd.uniform(-2.5, 2.5), rnd.uniform(2.6, 4.2)) for _ in range(5)]
    for y in range(size):
        for x in range(size):
            if any((x - lx) ** 2 + (y - ly) ** 2 <= r * r for lx, ly, r in lobes):
                g.px(x, y, 'G')
    for _ in range(6):                                   # droplets
        ang = rnd.uniform(0, 6.283)
        dist = rnd.uniform(5.5, 7.8)
        import math
        dx, dy = c + math.cos(ang) * dist, c + math.sin(ang) * dist
        r = rnd.choice((0.6, 0.9, 1.2))
        for y in range(size):
            for x in range(size):
                if (x - dx) ** 2 + (y - dy) ** 2 <= r * r + 0.3:
                    g.px(x, y, 'G')
    # shade bottom-right, highlight top-left
    src = [row[:] for row in g.g]
    for y in range(size):
        for x in range(size):
            if src[y][x] != 'G':
                continue
            if x + 1 >= size or y + 1 >= size or src[y + 1][x] == '.' or src[y][x + 1] == '.':
                g.g[y][x] = 'H'
            elif x > 0 and y > 0 and (src[y - 1][x] == '.' or src[y][x - 1] == '.'):
                g.g[y][x] = 'I'
    return g


def pickup_glow():
    g = Grid(33, 33)
    import math
    for y in range(33):
        for x in range(33):
            d = math.hypot(x - 16, y - 16)
            if d <= 16.2:
                g.px(x, y, 'F' if d > 12.5 else ('E' if d > 8 else 'D'))
    return g


def ammo_icon(full):
    g = Grid(14, 14)
    import math
    for y in range(14):
        for x in range(14):
            d = math.hypot(x - 6.5, y - 6.5)
            if d <= 6.6:
                if not full:
                    g.px(x, y, 'g' if d <= 5.0 else 'h')
                else:
                    g.px(x, y, 'G')
    if full:
        src = [row[:] for row in g.g]
        for y in range(14):
            for x in range(14):
                if src[y][x] == '.':
                    continue
                if math.hypot(x - 8, y - 8) > 4.6:
                    g.g[y][x] = 'H'
                if math.hypot(x - 4.5, y - 4.5) < 2.2:
                    g.g[y][x] = 'I'
    g.outline_alpha()
    return g


def stroke(g, pts, c='D'):
    for x, y in pts:
        g.px(x, y, c)
    src = [row[:] for row in g.g]
    for y in range(g.h):
        for x in range(g.w):
            if src[y][x] == '.' and any(0 <= x + dx < g.w and 0 <= y + dy < g.h and src[y + dy][x + dx] == c
                                        for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                g.g[y][x] = 'o'


def bounce_icon():
    """A shot path coming down onto a surface and ricocheting up, with an arrowhead: tinted cyan in game."""
    g = Grid(15, 13)
    path = [(1, 1), (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 7), (8, 6), (9, 5), (10, 4), (11, 3), (12, 2), (13, 1)]
    head = [(11, 1), (12, 1), (13, 2), (13, 3)]
    stroke(g, path + head)
    g.hline(1, 13, 9, 'D')
    g.hline(0, 14, 8, 'o')
    g.hline(0, 14, 10, 'o')
    g.px(0, 9, 'o')
    g.px(14, 9, 'o')
    return g


def crosshair():
    g = Grid(15, 15)
    for i in range(15):
        if i < 5 or i > 9:
            g.px(7, i, 'D')
            g.px(i, 7, 'D')
    g.px(7, 7, 'D')
    for dx, dy in ((5, 5), (9, 5), (5, 9), (9, 9)):
        g.px(dx, dy, 'D')
    g.outline_alpha_keep = None
    # dark outline around the white strokes
    src = [row[:] for row in g.g]
    for y in range(15):
        for x in range(15):
            if src[y][x] == '.':
                if any(0 <= x + dx < 15 and 0 <= y + dy < 15 and src[y + dy][x + dx] == 'D'
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    g.g[y][x] = 'o'
    return g


def button(state):
    face = {'normal': ('G', 'I', 'H'), 'hover': ('I', 'D', 'G'), 'pressed': ('H', 'G', 'e'),
            'ghost': ('f', 'h', 'e')}[state]
    g = Grid(24, 12, face[0])
    g.hline(1, 22, 1, face[1])
    g.vline(1, 1, 10, face[1])
    g.hline(1, 22, 10, face[2])
    g.vline(22, 1, 10, face[2])
    g.border()
    for (x, y) in ((0, 0), (23, 0), (0, 11), (23, 11)):
        g.px(x, y, '.')
    return g


def laser():
    g = Grid(6, 1)
    g.rect(0, 0, 3, 0, 'D')
    return g


PAL.update({
    'R': '#ff3b30', 'S': '#a30f1a', 'T': '#ffd2cc',                      # enemy live (red-hot) / hearts
    'K': '#e4a889', 'L': '#b0705a',                                      # skin, skin shade (hands)
    'U': '#f2f4f7', 'V': '#ff8a1f', 'W': '#c2520a',                      # paint pod tube, paint balls
})


def heart(full):
    rows = ['.oo...oo.',
            'oXXo.oXXo',
            'oXXXoXXXo',
            'oXXXXXXXo',
            '.oXXXXXo.',
            '..oXXXo..',
            '...oXo...',
            '....o....']
    g = Grid(14, 14)
    ox, oy = 2, 3
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '.':
                continue
            if ch == 'X':
                ch = 'R' if full else 'f'
                if full and (x, y) in ((2, 1), (1, 2), (2, 2)):
                    ch = 'T'
                elif full and y >= 4 and x >= 4:
                    ch = 'S'
            g.px(ox + x, oy + y, ch)
    # thicken to fill the 14 px pip: scale the heart drawing 1.5x would blur; instead draw a 2nd outline ring
    return g


def heart_big(full):
    """A 13x12 heart drawn by hand to fill the 14 px pip."""
    rows = ['..ooo...ooo..',
            '.oXXXo.oXXXo.',
            'oXHXXXoXXXXXo',
            'oXHXXXXXXXXXo',
            'oXXXXXXXXXXSo',
            'oXXXXXXXXXXSo',
            '.oXXXXXXXXSo.',
            '..oXXXXXXSo..',
            '...oXXXXSo...',
            '....oXXSo....',
            '.....oSo.....',
            '......o......']
    g = Grid(14, 14)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '.':
                continue
            if not full and ch in 'XHS':
                ch = 'g' if ch != 'H' else 'h'
            elif ch == 'X':
                ch = 'R'
            elif ch == 'H':
                ch = 'T'
            g.px(x, y + 1, ch)
    return g


def pipe(hazard=False):
    """Aluminium pipe seen side-on, 15 px (0.5 m) thick, lit from above; tiles horizontally with 3 px end caps."""
    g = Grid(30, 15, 'k')
    shade = {1: 'l', 2: 'l', 3: 'k', 4: 'l', 5: 'k', 6: 'k', 7: 'k', 8: 'j', 9: 'j', 10: 'j', 11: 'i', 12: 'i', 13: 'h'}
    for y, c in shade.items():
        g.hline(1, 28, y, c)
    g.hline(0, 29, 0, 'o')
    g.hline(0, 29, 14, 'o')
    # flange joint mid-tile
    for x in (14, 15):
        g.vline(x, 1, 13, 'j' if x == 14 else 'i')
    g.px(14, 1, 'l')
    g.px(14, 2, 'l')
    if hazard:
        for x0 in (5, 20):
            for x in range(x0, x0 + 5):
                for y in range(1, 14):
                    g.px(x, y, 'z' if ((x - y) // 2) % 2 == 0 else 'e')
            g.vline(x0 - 1, 1, 13, 'o')
            g.vline(x0 + 5, 1, 13, 'o')
    # end caps: darker ring with a rim
    g.vline(0, 0, 14, 'o')
    g.vline(29, 0, 14, 'o')
    g.vline(1, 1, 13, 'h')
    g.vline(2, 1, 13, 'j')
    g.vline(27, 1, 13, 'i')
    g.vline(28, 1, 13, 'h')
    return g


def hanger_rod():
    g = Grid(2, 30)
    g.vline(0, 0, 29, 'h')
    g.vline(1, 0, 29, 'f')
    return g


def pipe_clamp():
    rows = ['.ooo.',
            'ojijo',
            'oj.jo',
            'oj.jo']
    g = Grid(5, 4)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            g.px(x, y, ch)
    return g


def light_panel():
    """A suspended LED light panel, 75x12 (2.5x0.4 m), its diffuser cracked and half dead: it hangs broken at an angle."""
    g = Grid(75, 12, 'C')
    g.hline(1, 73, 1, 'l')
    g.hline(1, 73, 2, 'k')
    g.hline(1, 73, 9, 'j')
    g.hline(1, 73, 10, 'i')
    g.vline(1, 1, 10, 'l')
    g.vline(2, 1, 10, 'k')
    g.vline(72, 1, 10, 'j')
    g.vline(73, 1, 10, 'i')
    # diffuser: lit segments, a dead stretch, and cracks
    g.rect(3, 3, 71, 8, 'C')
    g.hline(3, 71, 8, 'B')
    for x in range(14, 72, 14):
        g.vline(x, 3, 8, 'k')                       # segment dividers
    g.rect(43, 3, 56, 8, 'g')                       # dead segment
    g.hline(43, 56, 3, 'h')
    for x, y in ((20, 3), (21, 4), (21, 5), (22, 6), (23, 6), (24, 7), (25, 8), (22, 4), (23, 3),
                 (60, 8), (61, 7), (62, 6), (62, 5), (63, 4), (64, 3), (61, 5), (60, 4)):
        g.px(x, y, 'h')                             # cracks
    for x, y in ((47, 5), (48, 6), (50, 4), (52, 7), (53, 6)):
        g.px(x, y, 'f')
    g.border()
    for x in (6, 68):                               # cable eyelets on top
        g.px(x, 0, 'j')
    return g


def cable():
    g = Grid(1, 30)
    g.vline(0, 0, 29, 'e')
    return g


def cable_snapped():
    """A cable hanging from the ceiling and ending in a frayed tip (the panel's broken support): 5x30, top-anchored."""
    g = Grid(5, 30)
    g.vline(2, 0, 25, 'e')
    for x, y in ((1, 26), (3, 26), (0, 27), (4, 27), (2, 26), (2, 27), (1, 28), (3, 28), (0, 29), (4, 29)):
        g.px(x, y, 'e' if y < 28 else 'h')
    return g


def gun(length, barrels, accent, hands=('K', 'L'), heavy=False, with_hands=True, front_hand=False, stock=7):
    """
    A paintball marker held in both hands, drawn around its pivot: the rear hand on the grip. Columns left of the pivot
    are the stock (`stock` px); the muzzle is `length` px right of the pivot (the shot's barrel length). Returns
    (grid, pivot_x, pivot_y). Rows: hopper above, barrel(s) on the pivot row, grip and hands below.
    """
    h = 15 if heavy else (13 if len(barrels) > 1 else 12)
    cy = 6 if not heavy else 7
    W = stock + length + 1
    g = Grid(W, h)
    px0 = stock                                     # pivot column (rear hand)
    # barrels
    for bc, th in barrels:
        y0 = cy + bc - th // 2
        g.rect(px0 + 6, y0, W - 2, y0 + th - 1, 'j')
        g.hline(px0 + 6, W - 2, y0, 'k')
        g.hline(px0 + 6, W - 2, y0 + th - 1, 'i')
        g.vline(W - 2, y0, y0 + th - 1, 'f')
    # stock and receiver
    g.rect(1, cy - 1, px0 + 7, cy + 2, 'g')
    g.hline(1, px0 + 7, cy - 1, 'h')
    g.hline(1, px0 + 7, cy + 2, 'f')
    g.rect(1, cy - 2, 3, cy + 3, 'g')               # butt pad
    # hopper (team colour)
    g.rect(px0 - 1, cy - 6, px0 + 5, cy - 2, accent[0])
    g.hline(px0 - 1, px0 + 5, cy - 6, accent[2])
    g.vline(px0 + 5, cy - 6, cy - 2, accent[1])
    # grip
    g.rect(px0 - 1, cy + 3, px0 + 1, min(h - 1, cy + 5), 'f')
    # front grip under the barrel
    g.rect(px0 + 9, cy + 2, px0 + 10, cy + 3, 'f')
    g.outline_alpha()
    if with_hands:
        k, l = hands
        g.rect(px0 - 1, cy + 1, px0 + 1, cy + 3, k)     # rear hand round the grip
        g.hline(px0 - 1, px0 + 1, cy + 3, l)
        g.px(px0 + 2, cy + 1, l)
        if front_hand:
            g.rect(px0 + 8, cy + 1, px0 + 11, cy + 3, k)  # front hand on the foregrip
            g.hline(px0 + 8, px0 + 11, cy + 3, l)
        # outline the hands where they meet transparency
        src = [row[:] for row in g.g]
        for y in range(h):
            for x in range(W):
                if src[y][x] == '.' and any(0 <= x + dx < W and 0 <= y + dy < h and src[y + dy][x + dx] in (k, l)
                                            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    g.g[y][x] = 'o'
    return g, px0, cy


def reload_frames(length, barrels, accent):
    """Six frames of a reload: the front hand leaves the foregrip, fetches a paint pod and pours it into the hopper."""
    frames = []
    for f in range(6):
        g, px0, cy = gun(length, barrels, accent)
        # grow the canvas upwards for the pod
        top = 10
        big = Grid(g.w, g.h + top + 5)
        for y in range(g.h):
            for x in range(g.w):
                big.g[y + top][x] = g.g[y][x]
        cyb = cy + top
        hop_y = cyb - 6                             # hopper top row
        if f == 1:                                  # hand drops to the belt pouch
            stroke_hand(big, px0 + 6, cyb + 4)
        if f in (2, 3, 4):
            tilt = f - 2                            # pod comes up, tips over, pours
            tx = px0 + 2 + tilt
            ty = hop_y - 7 + (0 if f == 2 else 2)
            pod(big, tx, ty, tilt)
            stroke_hand(big, tx + 3, ty + 3)
            if f >= 3:
                for i, (bx, by) in enumerate(((px0 + 2, hop_y - 2), (px0 + 3, hop_y - 1)) if f == 3 else
                                             ((px0 + 1, hop_y - 1), (px0 + 3, hop_y), (px0 + 2, hop_y - 3))):
                    big.px(bx, by, 'V')
        if f == 4:
            big.hline(px0, px0 + 4, hop_y + 1, 'V')  # hopper filling up
        frames.append((big, px0, cyb))
    return frames


def stroke_hand(g, x, y):
    g.rect(x, y, x + 2, y + 2, 'K')
    g.hline(x, x + 2, y + 2, 'L')
    ring = [(xx, yy) for xx in range(x - 1, x + 4) for yy in (y - 1, y + 3)] + \
           [(xx, yy) for yy in range(y, y + 3) for xx in (x - 1, x + 3)]
    for xx, yy in ring:
        if 0 <= xx < g.w and 0 <= yy < g.h and g.g[yy][xx] == '.':
            g.g[yy][xx] = 'o'


def pod(g, x, y, tilt):
    """A paint pod: a clear tube of orange balls, 3x7, leaning right by `tilt` px over its height."""
    for i in range(7):
        xx = x + (tilt * i) // 6
        g.px(xx - 1, y + i, 'o')
        g.px(xx + 1 + 1, y + i, 'o')
        g.px(xx, y + i, 'V' if i % 2 else 'U')
        g.px(xx + 1, y + i, 'W' if i % 2 else 'V')
    g.hline(x - 1, x + 2, y - 1, 'o')


OBJECTS = {
    'Objects/obj_lab_bench': lab_bench,
    'Objects/obj_ceiling_duct': ceiling_duct,
    'Objects/obj_floor': floor_tile,
    'Objects/obj_ceiling': ceiling_tile,
    'Objects/obj_wall_concrete': wall_concrete,
    'Objects/obj_wall_partition': wall_partition,
    'Objects/obj_ledge': ledge,
    'Objects/obj_bunker_roof': bunker_roof,
    'Objects/obj_whiteboard': whiteboard,
    'Objects/obj_door_safezone': door_glass,
    'Objects/obj_door_arena': door_steel,
    'Objects/obj_door_exit': door_exit,
    'Objects/obj_exit_sign': exit_sign,
    'Objects/obj_door_led': led_strip,
    'Objects/obj_pipe': pipe,
    'Objects/obj_pipe_hazard': lambda: pipe(True),
    'Objects/obj_hanger_rod': hanger_rod,
    'Objects/obj_pipe_clamp': pipe_clamp,
    'Objects/obj_light_panel': light_panel,
    'Objects/obj_cable': cable,
    'Objects/obj_cable_snapped': cable_snapped,
    'UI/ui_heart_full': lambda: heart_big(True),
    'UI/ui_heart_empty': lambda: heart_big(False),
    'Weapons/wpn_pistol': lambda: marker(26, [(0, 3)], 'GHI'),
    'Weapons/wpn_twin': lambda: marker(26, [(-2, 2), (2, 2)], 'GHI'),
    'Weapons/wpn_pistol_enemy': lambda: marker(23, [(0, 3)], 'GHI'),
    'Weapons/wpn_twin_enemy': lambda: marker(23, [(-2, 2), (2, 2)], 'GHI'),
    'Weapons/wpn_heavy_enemy': lambda: marker(23, [(0, 7)], 'GHI', heavy=True),
    'Weapons/wpn_pistol_boss': lambda: marker(30, [(0, 3)], 'GHI'),
    'Weapons/wpn_twin_boss': lambda: marker(30, [(-2, 2), (2, 2)], 'GHI'),
    'Weapons/wpn_heavy_boss': lambda: marker(30, [(0, 7)], 'GHI', heavy=True),
    'FX/fx_splat_a': lambda: splat(1),
    'FX/fx_splat_b': lambda: splat(7),
    'FX/fx_splat_c': lambda: splat(13),
    'FX/fx_pickup_glow': pickup_glow,
    'UI/ui_ammo_full': lambda: ammo_icon(True),
    'UI/ui_ammo_empty': lambda: ammo_icon(False),
    'UI/ui_bounce_icon': bounce_icon,
    'UI/ui_crosshair': crosshair,
    'UI/ui_button_normal': lambda: button('normal'),
    'UI/ui_button_hover': lambda: button('hover'),
    'UI/ui_button_pressed': lambda: button('pressed'),
    'UI/ui_button_ghost': lambda: button('ghost'),
    'UI/ui_laser': laser,
}


def build():
    grids = {k: f() for k, f in OBJECTS.items()}
    # shelf-pack into an atlas, 2 px gaps
    order = sorted(grids, key=lambda k: -grids[k].h)
    W = 200
    x = y = 0
    shelf = 0
    layout = {}
    for k in order:
        gr = grids[k]
        if x + gr.w > W:
            x, y = 0, y + shelf + 2
            shelf = 0
        layout[k] = (x, y, gr.w, gr.h)
        x += gr.w + 2
        shelf = max(shelf, gr.h)
    H = y + shelf
    return grids, layout, (W, H)


if __name__ == '__main__':
    grids, layout, (W, H) = build()
    cmd, out = sys.argv[1], sys.argv[2]
    if cmd == 'preview':
        from PIL import Image
        im = Image.new('RGBA', (W, H), (40, 44, 56, 255))
        for k, (x, y, w, h) in layout.items():
            for yy, row in enumerate(grids[k].rows()):
                for xx, ch in enumerate(row):
                    if ch != '.':
                        im.putpixel((x + xx, y + yy), tuple(int(PAL[ch][i:i + 2], 16) for i in (1, 3, 5)) + (255,))
        im.resize((W * 4, H * 4), Image.NEAREST).save(out)
        print('atlas', W, H)
    elif cmd == 'recipe':
        nodes = {}
        for k, (x, y, w, h) in layout.items():
            nodes[k.split('/')[-1]] = {'layer': 'atlas', 'at': [x, y],
                                       'draw': [{'op': 'grid', 'at': [0, 0], 'legend': PAL, 'rows': grids[k].rows()}]}
        recipe = {'job': {'canvas': [W, H], 'frame_count': 1, 'views': [{'id': 'side', 'offset': [0, 0]}],
                          'source_bookends_exact': False},
                  'scene': {'layers': [{'id': 'atlas', 'name': 'Objects - modern set'}],
                            'views': {'side': {'nodes': nodes}}}}
        json.dump(recipe, open(out, 'w'), separators=(',', ':'))
        print('recipe bytes', len(json.dumps(recipe, separators=(',', ':'))))
    elif cmd == 'layout':
        json.dump({'size': [W, H], 'items': layout}, open(out, 'w'), indent=1)
