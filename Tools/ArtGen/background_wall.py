"""Builds the back-wall tile (Assets/Art/Background/bg_wall.png) from the itch.io cover's wall.

The cover (ItchCover/ricochet_cover_315x250_source.png, generated with PixelLab) shows a 200 px stretch of wall that
repeats every 52 px. Characters, shots, props and the boss cover parts of each repeat, so every pixel of the module is
the most common colour across the repeats where nothing known covers it.

The game's wall is 360 px tall (12 m at 30 px/m), floor to ceiling. The tile stacks the cover's storey (frosted glass
band, LED strip, laminate) twice under its soffit and upper laminate:

    soffit 17 | upper laminate 19 | glass 91 | LED 18 | laminate 53 | glass 91 | LED 18 | laminate 53   = 360 px

Usage: background_wall.py <cover png> <Assets/Art> [preview.png]
"""
import json
import os
import sys
from collections import Counter

import numpy as np
from PIL import Image

PERIOD, FIRST = 52, 3                # module width and the x of the first full repeat in the cover
WALL_TOP, WALL_BOTTOM = 22, 222      # ceiling underside and floor top in the cover
TILE_H = 360

# Band rows inside the extracted 200-row module (cover y minus WALL_TOP).
SOFFIT = (0, 17)
UPPER = (17, 38)
GLASS = (38, 129)
LED = (129, 147)

# Where the cover's foreground covers the wall (x0, y0, x1, y1), and its shot lines (a, b, radius).
BOXES = [
    (15, 22, 21, 133), (81, 22, 87, 133),          # hanger rods
    (4, 126, 102, 152),                            # pipe platform
    (28, 78, 98, 142),                             # player and gun
    (118, 16, 148, 34),                            # paint splat on the ceiling
    (224, 40, 315, 222),                           # boss
    (184, 38, 252, 108), (200, 22, 246, 50),       # hanging enemy, burst and duct
    (137, 106, 150, 119), (174, 116, 187, 129), (187, 176, 200, 189),  # enemy paintballs
    (108, 168, 124, 186),                          # fresh paintball
    (116, 192, 168, 222),                          # bounce flash
    (168, 160, 268, 222),                          # floor enemy and bench
    (62, 206, 94, 222),                            # paint splat on the floor
]
LINES = [((84, 112), (140, 222), 4), ((140, 215), (216, 82), 7)]  # laser sight, live shot


def occluder_mask(h, w):
    mask = np.zeros((h, w), bool)

    def box(x0, y0, x1, y1):
        mask[max(0, y0):min(h, y1), max(0, x0):min(w, x1)] = True

    for b in BOXES:
        box(*b)
    for a, b, r in LINES:
        for t in np.linspace(0, 1, 400):
            x, y = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
            box(int(x - r), int(y - r), int(x + r + 1), int(y + r + 1))
    return mask


def extract_module(cover):
    im = np.array(cover.convert("RGB"))
    h, w, _ = im.shape
    mask = occluder_mask(h, w)
    starts = [FIRST + PERIOD * k for k in range(w) if FIRST + PERIOD * (k + 1) <= w]
    mod = np.zeros((WALL_BOTTOM - WALL_TOP, PERIOD, 3), np.uint8)
    for y in range(WALL_TOP, WALL_BOTTOM):
        for x in range(PERIOD):
            clean = [tuple(im[y, s + x]) for s in starts if not mask[y, s + x]]
            if len(clean) < 2:
                clean = clean or [tuple(im[y, s + x]) for s in starts]
            mod[y - WALL_TOP, x] = Counter(clean).most_common(1)[0][0]
    return mod


def band(mod, rows):
    return mod[rows[0]:rows[1]]


def build_tile(mod):
    # The cover's lower laminate is the most occluded band, so both laminate bands reuse the upper laminate's most
    # common row (plain laminate with its panel seam), which also keeps the seams identical everywhere.
    upper = band(mod, UPPER)
    rows = Counter(r.tobytes() for r in upper)
    plain = np.frombuffer(rows.most_common(1)[0][0], np.uint8).reshape(1, PERIOD, 3)
    laminate = np.repeat(plain, 53, axis=0)
    storey = np.concatenate([band(mod, GLASS), band(mod, LED), laminate])
    tile = np.concatenate([band(mod, SOFFIT), upper[:19], storey, storey])
    assert tile.shape[0] == TILE_H, tile.shape
    return Image.fromarray(tile, "RGB").convert("RGBA")


def main():
    cover, art = sys.argv[1], sys.argv[2]
    tile = build_tile(extract_module(Image.open(cover)))
    out = os.path.join(art, "Background")
    os.makedirs(out, exist_ok=True)
    tile.save(os.path.join(out, "bg_wall.png"))

    manifest = os.path.join(art, "art_slices.json")
    slices = json.load(open(manifest)) if os.path.exists(manifest) else {}
    # Full Rect so the SpriteRenderer can tile it.
    slices["Background/bg_wall"] = {"frame": [PERIOD, TILE_H], "mesh": "FullRect", "pivot": [0.5, 0.5]}
    json.dump(slices, open(manifest, "w"), indent=1, sort_keys=True)
    print("wrote", os.path.join(out, "bg_wall.png"), tile.size)

    if len(sys.argv) > 3:
        strip = Image.new("RGBA", (PERIOD * 4, TILE_H))
        for i in range(4):
            strip.paste(tile, (i * PERIOD, 0))
        strip.resize((strip.width * 2, strip.height * 2), Image.NEAREST).save(sys.argv[3])


if __name__ == "__main__":
    main()
