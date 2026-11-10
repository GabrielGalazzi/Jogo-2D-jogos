"""Synthesises the game's sound effects (original, CC0) as 44.1 kHz 16-bit mono WAVs.

Design rule: every sound is short with a fast attack and no reverb tail, and each gameplay sound owns its own band and
timbre so they never blur together when they overlap:
  shot       low pneumatic thump + mid noise puff       (80-3000 Hz, toneless)
  armed      bright metallic ping gliding UP            (1.9-2.9 kHz + inharmonic partial, tonal)
  ricochet   short dry tink, no glide                   (1.3 kHz + 3.4 kHz, very short)
  harmless   dull wet plap sliding DOWN                 (lowpassed noise, 400->140 Hz squelch)
  eliminate  big wet splat + low hit + two-note chime   (wideband + E6/B6 square chime)
All are loudness-matched (RMS of the active part) with peaks capped at -1 dBFS; the game mixes them with per-call volume.
Usage: synth_sfx.py <output dir>
"""
import os
import sys
import wave

import numpy as np

SR = 44100
rng = np.random.default_rng(7)


def t(d):
    return np.arange(int(SR * d)) / SR


def env(n, attack=0.002, decay=0.1, curve=1.0):
    x = np.arange(n) / SR
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    d = np.exp(-x / max(decay, 1e-4)) ** curve
    return a * d


def noise(d):
    return rng.uniform(-1, 1, int(SR * d))


def onepole_lp(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def lp(x, cutoff, order=2):
    for _ in range(order):
        x = onepole_lp(x, cutoff)
    return x


def hp(x, cutoff, order=1):
    return x - lp(x, cutoff, order)


def bp(x, lo, hi):
    return hp(lp(x, hi), lo)


def sweep(f0, f1, d, shape='sine', curve=1.0):
    n = int(SR * d)
    k = (np.arange(n) / max(n - 1, 1)) ** curve
    f = f0 + (f1 - f0) * k
    ph = 2 * np.pi * np.cumsum(f) / SR
    if shape == 'sine':
        return np.sin(ph)
    if shape == 'square':
        return np.sign(np.sin(ph)) * 0.6
    if shape == 'tri':
        return 2 / np.pi * np.arcsin(np.sin(ph))
    if shape == 'saw':
        return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
    raise ValueError(shape)


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, delay):
    return np.concatenate([np.zeros(int(SR * delay)), x])


def note(freq, d, shape='square', decay=None):
    s = sweep(freq, freq, d, shape)
    return s * env(len(s), 0.003, decay or d * 0.6)


def finish(x, target_rms_db=-15.0):
    x = x - np.mean(x)
    fade = int(SR * 0.004)
    if len(x) > fade:
        x[-fade:] *= np.linspace(1, 0, fade)
    active = np.abs(x) > np.max(np.abs(x)) * 0.05
    rms = np.sqrt(np.mean(x[active] ** 2)) if active.any() else 1e-9
    x = x * (10 ** (target_rms_db / 20) / rms)
    peak = np.max(np.abs(x))
    limit = 10 ** (-1 / 20)
    if peak > limit:
        x = x * (limit / peak)
    return x


def save(path, x):
    data = (np.clip(x, -1, 1) * 32767).astype('<i2')
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def shot_player():
    thump = sweep(150, 70, 0.09) * env(int(SR * 0.09), 0.001, 0.035)
    puff = bp(noise(0.07), 900, 3200) * env(int(SR * 0.07), 0.001, 0.018)
    click = bp(noise(0.006), 3000, 8000) * 0.6
    return mix(thump * 0.9, puff * 1.4, click)


def shot_enemy():
    thump = sweep(120, 60, 0.1) * env(int(SR * 0.1), 0.001, 0.04)
    puff = bp(noise(0.08), 500, 1600) * env(int(SR * 0.08), 0.001, 0.022)
    return mix(thump, puff * 1.3)


def shot_heavy():
    boom = sweep(85, 38, 0.22) * env(int(SR * 0.22), 0.002, 0.08)
    puff = lp(noise(0.14), 700) * env(int(SR * 0.14), 0.002, 0.045)
    return mix(boom * 1.1, puff * 1.6)


def ricochet_armed():
    d = 0.24
    n = int(SR * d)
    p1 = sweep(1900, 2900, d, curve=0.5)
    p2 = sweep(1900 * 2.76, 2900 * 2.76, d, curve=0.5) * 0.35
    body = (p1 + p2) * env(n, 0.001, 0.07)
    hit = bp(noise(0.01), 4000, 9000) * 0.5
    return mix(body, hit)


def ricochet():
    d = 0.1
    n = int(SR * d)
    body = (np.sin(2 * np.pi * 1300 * t(d)) + 0.5 * np.sin(2 * np.pi * 3400 * t(d))) * env(n, 0.001, 0.025)
    hit = bp(noise(0.008), 2500, 7000) * 0.4
    return mix(body, hit)


def splat_harmless():
    d = 0.12
    goo = lp(noise(d), 900) * env(int(SR * d), 0.002, 0.035)
    squelch = sweep(400, 140, d, 'tri') * env(int(SR * d), 0.002, 0.04) * 0.5
    return mix(goo * 1.6, squelch)


def elimination():
    splat = bp(noise(0.18), 180, 2400) * env(int(SR * 0.18), 0.001, 0.05)
    hit = sweep(110, 50, 0.16) * env(int(SR * 0.16), 0.001, 0.06)
    chime = mix(note(1318.5, 0.08, 'square', 0.05), at(note(1975.5, 0.14, 'square', 0.07), 0.07))
    return mix(splat * 1.5, hit * 1.1, at(chime * 0.55, 0.03))


def player_hit():
    d = 0.16
    buzz = sweep(220, 110, d, 'saw') * env(int(SR * d), 0.001, 0.06)
    wet = lp(noise(0.1), 1200) * env(int(SR * 0.1), 0.001, 0.03)
    return mix(lp(buzz, 2500) * 0.9, wet * 1.2)


def footstep(run, k):
    r = np.random.default_rng(100 + k + (50 if run else 0))
    d = 0.06 if run else 0.07
    n = int(SR * d)
    cut = r.uniform(1500, 2400) if run else r.uniform(900, 1500)
    scuff = lp(r.uniform(-1, 1, n), cut) * env(n, 0.001, 0.016 if run else 0.02)
    tap = sweep(r.uniform(140, 180), 70, 0.045) * env(int(SR * 0.045), 0.001, 0.012)
    click = hp(r.uniform(-1, 1, int(SR * 0.004)), 3000) * (0.35 if run else 0.2)
    return mix(scuff * 1.4, tap * 0.8, click)


def jump():
    d = 0.12
    whoosh = bp(noise(d), 800, 3000) * env(int(SR * d), 0.01, 0.04)
    up = sweep(280, 620, d, 'tri') * env(int(SR * d), 0.003, 0.045) * 0.5
    return mix(whoosh, up)


def land():
    thud = sweep(95, 48, 0.1) * env(int(SR * 0.1), 0.001, 0.035)
    grit = lp(noise(0.06), 1400) * env(int(SR * 0.06), 0.001, 0.015)
    return mix(thud * 1.2, grit)


def crouch():
    d = 0.13
    return bp(noise(d), 1800, 5000) * env(int(SR * d), 0.015, 0.045)


def ui_click():
    d = 0.03
    return mix(sweep(2200, 1800, d, 'square') * env(int(SR * d), 0.0005, 0.008), hp(noise(0.004), 4000) * 0.5)


def arpeggio(freqs, step, hold, shape='square'):
    parts = [at(note(f, hold if i == len(freqs) - 1 else step * 1.3, shape, (hold if i == len(freqs) - 1 else step) * 0.7), i * step)
             for i, f in enumerate(freqs)]
    return mix(*parts)


def round_start():
    return arpeggio([523.3, 659.3, 784.0, 1046.5], 0.075, 0.22)


def round_clear():
    return mix(arpeggio([784.0, 1046.5, 1318.5, 1568.0], 0.085, 0.4), at(note(523.3, 0.6, 'tri', 0.35) * 0.6, 0.255))


def round_lost():
    return arpeggio([659.3, 523.3, 440.0, 349.2], 0.12, 0.45, 'tri')


def gun_switch():
    c1 = mix(hp(noise(0.012), 2000), sweep(900, 900, 0.05) * env(int(SR * 0.05), 0.0005, 0.012) * 0.5)
    c2 = mix(hp(noise(0.012), 1500), sweep(650, 650, 0.06) * env(int(SR * 0.06), 0.0005, 0.015) * 0.5)
    return mix(c1, at(c2, 0.11))


def reload():
    ticks = [at(hp(noise(0.006), 2500) * (0.5 + 0.1 * i), 0.035 * i) for i in range(5)]
    clack = at(mix(hp(noise(0.015), 1200), sweep(500, 420, 0.07) * env(int(SR * 0.07), 0.0005, 0.02) * 0.6), 0.2)
    return mix(*ticks, clack)


SOUNDS = {
    'sfx_shot_player': shot_player,
    'sfx_shot_enemy': shot_enemy,
    'sfx_shot_heavy': shot_heavy,
    'sfx_ricochet_armed': ricochet_armed,
    'sfx_ricochet': ricochet,
    'sfx_splat_harmless': splat_harmless,
    'sfx_elimination': elimination,
    'sfx_player_hit': player_hit,
    'sfx_jump': jump,
    'sfx_land': land,
    'sfx_crouch': crouch,
    'sfx_ui_click': ui_click,
    'sfx_round_start': round_start,
    'sfx_round_clear': round_clear,
    'sfx_round_lost': round_lost,
    'sfx_gun_switch': gun_switch,
    'sfx_reload': reload,
}

if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fn in SOUNDS.items():
        save(os.path.join(out, name + '.wav'), finish(fn()))
    for k in range(4):
        save(os.path.join(out, 'sfx_step_walk_%d.wav' % k), finish(footstep(False, k)))
        save(os.path.join(out, 'sfx_step_run_%d.wav' % k), finish(footstep(True, k)))
    print('wrote', len(SOUNDS) + 8, 'sounds to', out)
