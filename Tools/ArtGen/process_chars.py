"""Turns PixelLab character animations into game sprite strips that fit the hitboxes.

Per frame: the lowest opaque pixel goes on the bottom row (feet planted on the collider's base), the same ground-relative
rows are dropped from every frame (nearest-neighbour height fit, through flat torso colour), and frames that must fit a
smaller hitbox (ducking) lose further rows from their flattest band. Frames keep their horizontal position relative to the
character's idle centre, so motion doesn't jitter. Output: one horizontal strip per animation, pivot bottom-centre.

Usage: process_chars.py <config.json>
"""
import colorsys
import glob
import json
import os
import sys

from PIL import Image


def load_frames(folder):
    return [Image.open(f).convert('RGBA') for f in sorted(glob.glob(os.path.join(folder, '*.png')))]


def drop_canvas_rows(im, heights_above_ground, ground):
    """Removes the rows at the given heights above `ground` (row index of the feet), shifting everything above down."""
    rows = sorted({ground - h for h in heights_above_ground if 0 <= ground - h < im.height}, reverse=True)
    out = im
    for r in rows:
        top = out.crop((0, 0, out.width, r))
        rest = out.crop((0, r + 1, out.width, out.height))
        n = Image.new('RGBA', (out.width, out.height))
        n.paste(top, (0, 1))
        n.paste(rest, (0, r + 1))
        out = n
    return out


def row_diff(px, w, a, b):
    return sum(sum(abs(i - j) for i, j in zip(px[x, a], px[x, b])) for x in range(w))


def squeeze_to(im, max_h):
    """Drops the flattest rows of the opaque area until its height is at most max_h."""
    while True:
        bb = im.getbbox()
        if bb is None or bb[3] - bb[1] <= max_h:
            return im
        top, bottom = bb[1], bb[3]
        px = im.load()
        h = bottom - top
        lo, hi = top + int(h * 0.3), top + int(h * 0.85)
        best = min(range(max(lo, 1), max(hi, lo + 1)), key=lambda y: row_diff(px, im.width, y, y - 1))
        upper = im.crop((0, 0, im.width, best))
        lower = im.crop((0, best + 1, im.width, im.height))
        n = Image.new('RGBA', im.size)
        n.paste(upper, (0, 1))
        n.paste(lower, (0, best + 1))
        im = n


def recolour(im, hue_from, hue_to, v_scale=1.0, sat_min=0.42):
    """Shifts saturated pixels whose hue is within ±18° of hue_from to hue_to (degrees), keeping saturation and value."""
    im = im.copy()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            dh = min(abs(h * 360 - hue_from), 360 - abs(h * 360 - hue_from))
            if s >= sat_min and dh <= 18 and v > 0.18:
                r2, g2, b2 = colorsys.hsv_to_rgb((hue_to % 360) / 360, s, min(1.0, v * v_scale))
                px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    return im


def main(cfg):
    src = cfg['source']                      # .../<state>/animations
    out_dir = cfg['out_dir']
    W, H = cfg['canvas']
    drop = cfg.get('drop_heights', [])
    idle = load_frames(os.path.join(src, cfg['idle_anim'], 'east'))
    bb = idle[0].getbbox()
    # The body's centre relative to the canvas centre: animations come on different canvas sizes (template vs v3).
    centre_off = (bb[0] + bb[2]) // 2 - idle[0].width // 2
    report = {}
    for anim in cfg['anims']:
        frames = load_frames(os.path.join(src, anim['source'], 'east'))
        if 'frames' in anim:
            frames = [frames[i] for i in anim['frames']]
        fitted = []
        for i, f in enumerate(frames):
            b = f.getbbox()
            ground = b[3] - 1
            f = drop_canvas_rows(f, drop, ground)
            limit = anim.get('max_h')
            if limit and (anim.get('max_h_from', 0) <= i):
                f = squeeze_to(f, limit)
            b = f.getbbox()
            ground = b[3] - 1
            cell = Image.new('RGBA', (W, H))
            # feet on the bottom row, idle centre on the cell centre
            cell.paste(f, (W // 2 - (f.width // 2 + centre_off), H - 1 - ground), f)
            fitted.append(cell)
        strip = Image.new('RGBA', (W * len(fitted), H))
        for i, c in enumerate(fitted):
            strip.paste(c, (W * i, 0))
        if cfg.get('lighten'):
            strip = lighten(strip)
        hand_cfg = cfg.get('hands')
        hands = []
        if hand_cfg and not anim.get('no_hands'):
            for c in fitted:
                hands.extend(find_hands(c, hand_cfg['head'], hand_cfg['band'], hand_cfg.get('grip_up', 2)))
        for variant, hues in cfg.get('variants', {'': None}).items():
            im = strip if hues is None else recolour(strip, *hues)
            name = cfg['name'].replace('{v}', variant) + '_' + anim['name'] + '_right'
            os.makedirs(out_dir, exist_ok=True)
            im.save(os.path.join(out_dir, name + '.png'))
            report[name] = {'frame': [W, H], 'pivot': [0.5, 0.0], 'fps': anim.get('fps', 10),
                            'heights': [c.getbbox()[3] - c.getbbox()[1] if c.getbbox() else 0 for c in fitted]}
            if hands:
                report[name]['hands'] = hands
    return report


def find_hands(cell, head, band, grip_up):
    """
    The hands in a two-handed hold are the forward-most part of the body at chest height: below the head (`head` px down
    from the top of the figure) within `band` rows. Returns the gun pivot for this frame, in px from the feet pivot
    (bottom centre), y up: one pixel behind the front of the hands and `grip_up` px above them (the barrel axis).
    """
    b = cell.getbbox()
    if b is None:
        return [0.0, 0.0]
    px = cell.load()
    W, H = cell.size
    best = None
    for y in range(b[1] + head, min(b[3], b[1] + head + band)):
        for x in range(W - 1, -1, -1):
            if px[x, y][3]:
                if best is None or x > best[0]:
                    best = (x, y)
                break
    if best is None:
        return [0.0, 0.0]
    hx, hy = best[0] - 1, best[1] - grip_up
    return [round(hx + 0.5 - W / 2, 2), round(H - (hy + 0.5), 2)]


def lighten(im):
    """The boss's armour a few steps lighter (a mid steel blue), keeping the black outline and the yellow stripes."""
    im = im.copy()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if v < 0.08 or (35 / 360 <= h <= 60 / 360 and s > 0.45):
                continue
            r2, g2, b2 = colorsys.hsv_to_rgb(h, s * 0.75, min(1.0, v * 1.6 + 0.12))
            px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    return im


if __name__ == '__main__':
    cfg = json.load(open(sys.argv[1]))
    rep = main(cfg)
    slices_path = cfg['slices']
    slices = json.load(open(slices_path)) if os.path.exists(slices_path) else {}
    rel_dir = cfg['slices_prefix']
    for name, e in rep.items():
        print(name, 'heights', e['heights'], 'hands', e.get('hands', [])[:6])
        slices[rel_dir + name] = {k: v for k, v in e.items() if k != 'heights'}
    json.dump(slices, open(slices_path, 'w'), indent=1, sort_keys=True)
