"""Generate all 15 ESCAPE sound effects into Assets/Audio/SFX/.

Deterministic: fixed seeds, pure numpy math, plain PCM WAV output.
Run twice -> byte-identical files.

Usage:  tools/.venv/bin/python tools/sfxgen/generate_all.py

Style notes: bright, retro-chiptune-adjacent but soft-edged. Triangle and
sine carry most tones; squares are always lowpassed; bitcrush is applied
gently (8-10 bits). This is a cozy cave puzzler, not a harsh arcade cab.
"""

from pathlib import Path

import numpy as np

from synth import (SR, add, adsr, bitcrush, env_perc, env_swell, fade,
                   finalize, lowpass, noise, pitch_sweep, samples, sine,
                   square, timeline, triangle, vibrato, write_wav)

OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Audio" / "SFX"

# Note frequencies (Hz), equal temperament
A3, C4, E4 = 220.00, 261.63, 329.63
G4, C5, E5, G5 = 392.00, 523.25, 659.25, 783.99
C6, E6, G6 = 1046.50, 1318.51, 1567.98


def chime(freq, dur, tau, bright=0.35):
    """Soft chime voice: triangle body + sine fundamental + faint octave."""
    x = (0.55 * triangle(freq, dur)
         + 0.45 * sine(freq, dur)
         + bright * sine(freq * 2.0, dur) * 0.4)
    return x * env_perc(dur, attack=0.006, tau=tau)


# ---------------------------------------------------------------------------
# the 15 sounds
# ---------------------------------------------------------------------------

def sfx_jump():
    """Short rising blip."""
    dur = 0.15
    f = pitch_sweep(330.0, 740.0, dur, "exp")
    x = 0.7 * triangle(f, dur) + 0.3 * sine(f, dur)
    x *= env_perc(dur, attack=0.006, tau=0.07)
    x = lowpass(x, 4500.0)
    return bitcrush(x, bits=10)


def sfx_land():
    """Soft thud: low sine drop plus a puff of filtered noise."""
    dur = 0.10
    body = sine(pitch_sweep(150.0, 55.0, dur, "exp"), dur)
    body *= env_perc(dur, attack=0.003, tau=0.035)
    puff = lowpass(noise(dur, seed=11), 600.0)
    puff *= env_perc(dur, attack=0.002, tau=0.02)
    return body + 0.5 * puff


def sfx_shoot():
    """Quick whoosh with pitch drop."""
    dur = 0.20
    tone = square(pitch_sweep(950.0, 280.0, dur, "exp"), dur, duty=0.4)
    tone = lowpass(tone, 2500.0)
    tone *= env_perc(dur, attack=0.004, tau=0.06)
    whoosh = lowpass(noise(dur, seed=23), pitch_sweep(4000.0, 800.0, dur, "exp"))
    whoosh *= env_perc(dur, attack=0.01, tau=0.05)
    return 0.8 * tone + 0.6 * whoosh


def sfx_fireball_impact():
    """Small crunchy burst: bitcrushed noise + low punch."""
    dur = 0.25
    crunch = bitcrush(noise(dur, seed=37), bits=6, rate_div=6)
    crunch = lowpass(crunch, 3200.0)
    crunch *= env_perc(dur, attack=0.002, tau=0.06)
    punch = sine(pitch_sweep(210.0, 60.0, dur, "exp"), dur)
    punch *= env_perc(dur, attack=0.002, tau=0.08)
    return 0.8 * crunch + 0.7 * punch


def sfx_enemy_poof():
    """Descending dissolve: wobbly falling tone melting into soft noise."""
    dur = 0.30
    f = vibrato(pitch_sweep(620.0, 150.0, dur, "exp"), dur, rate=28.0, depth=0.04)
    tone = triangle(f, dur) * env_perc(dur, attack=0.005, tau=0.10)
    dust = lowpass(noise(dur, seed=41), pitch_sweep(2200.0, 300.0, dur, "exp"))
    dust *= adsr(dur, attack=0.05, decay=0.10, sustain=0.4, release=0.14)
    return 0.65 * tone + 0.55 * dust


def sfx_powerup():
    """Ascending 3-note chime: C5 -> E5 -> G5."""
    dur = 0.40
    c = timeline(dur)
    add(c, chime(C5, 0.16, tau=0.06), at=0.00)
    add(c, chime(E5, 0.16, tau=0.06), at=0.12)
    add(c, chime(G5, 0.16, tau=0.09, bright=0.5), at=0.24)
    return lowpass(c, 6000.0)


def sfx_lever():
    """Mechanical clunk: two quick low taps."""
    dur = 0.20

    def tap(freq, seed):
        tdur = 0.07
        th = square(freq, tdur, duty=0.5) * env_perc(tdur, attack=0.001, tau=0.018)
        click = lowpass(noise(tdur, seed=seed), 1500.0)
        click *= env_perc(tdur, attack=0.001, tau=0.008)
        return lowpass(th + 0.7 * click, 900.0)

    c = timeline(dur)
    add(c, tap(120.0, seed=53), at=0.00)
    add(c, tap(95.0, seed=59), at=0.09, gain=0.9)
    return c


def sfx_bridge():
    """Low rumble swell for the bridge extending."""
    dur = 0.60
    swell = env_swell(dur, rise=0.45, fall=0.4)
    lowf = pitch_sweep(48.0, 62.0, dur, "linear")
    bed = sine(lowf, dur) + 0.5 * sine(lowf * 1.5, dur)
    rumble = lowpass(noise(dur, seed=67), 240.0)
    return (0.75 * bed + 0.8 * rumble) * swell


def sfx_crystal():
    """Bright pling with sparkle overtones (bell-like inharmonic partials)."""
    dur = 0.35
    x = (sine(G6, dur) * env_perc(dur, 0.002, 0.11)
         + 0.45 * sine(G6 * 2.01, dur) * env_perc(dur, 0.002, 0.06)
         + 0.30 * sine(G6 * 2.76, dur) * env_perc(dur, 0.002, 0.045)
         + 0.35 * sine(G6 * 0.5, dur) * env_perc(dur, 0.002, 0.14))
    sparkle = timeline(dur)
    add(sparkle, sine(G6 * 1.5, 0.12) * env_perc(0.12, 0.002, 0.03), at=0.05, gain=0.25)
    return x + sparkle


def sfx_hurt():
    """Quick descending buzz, softened."""
    dur = 0.20
    f = pitch_sweep(440.0, 170.0, dur, "exp")
    x = square(f, dur, duty=0.3)
    t = np.arange(samples(dur)) / SR
    x *= 1.0 - 0.35 * (0.5 + 0.5 * np.sin(2.0 * np.pi * 42.0 * t))  # tremolo buzz
    x = bitcrush(x, bits=7)
    x = lowpass(x, 2000.0)
    return x * env_perc(dur, attack=0.004, tau=0.08)


def sfx_death():
    """Sad descending 3-note: E4 -> C4 -> A3, last note sagging."""
    dur = 0.60

    def voice(freq, ndur, droop=0.0):
        f = pitch_sweep(freq, freq * (1.0 - droop), ndur, "linear") if droop else freq
        x = 0.6 * triangle(f, ndur) + 0.4 * sine(f, ndur)
        return x * adsr(ndur, attack=0.01, decay=0.05, sustain=0.7, release=0.10)

    c = timeline(dur)
    add(c, voice(E4, 0.20), at=0.00)
    add(c, voice(C4, 0.20), at=0.18)
    add(c, voice(A3, 0.24, droop=0.06), at=0.36)
    return lowpass(c, 3000.0)


def sfx_checkpoint():
    """Warm 2-note confirmation: G4 -> C5."""
    dur = 0.40
    c = timeline(dur)
    add(c, chime(G4, 0.20, tau=0.09, bright=0.2), at=0.00)
    add(c, chime(C5, 0.24, tau=0.12, bright=0.3), at=0.14)
    return lowpass(c, 3500.0)


def sfx_ui_tap():
    """Tiny click."""
    dur = 0.06
    tick = sine(1250.0, dur) * env_perc(dur, attack=0.001, tau=0.012)
    click = lowpass(noise(dur, seed=71), 4000.0)
    click *= env_perc(dur, attack=0.0005, tau=0.004)
    return tick + 0.35 * click


def sfx_swap():
    """Fast two-tone flip whoosh (character swap)."""
    dur = 0.15
    c = timeline(dur)
    lo = triangle(pitch_sweep(520.0, 640.0, 0.06, "exp"), 0.06)
    lo *= env_perc(0.06, attack=0.003, tau=0.03)
    hi = triangle(pitch_sweep(780.0, 960.0, 0.08, "exp"), 0.08)
    hi *= env_perc(0.08, attack=0.003, tau=0.035)
    add(c, lo, at=0.00)
    add(c, hi, at=0.06, gain=0.9)
    whoosh = lowpass(noise(dur, seed=83), pitch_sweep(1500.0, 5000.0, dur, "exp"))
    whoosh *= env_swell(dur, rise=0.5, fall=0.45)
    add(c, whoosh, at=0.0, gain=0.35)
    return c


def sfx_level_complete():
    """Triumphant 5-note fanfare: C5 E5 G5 C6 -> held E6 with vibrato."""
    dur = 1.20

    def fanfare_note(freq, ndur, tau):
        body = 0.5 * triangle(freq, ndur) + 0.35 * sine(freq, ndur)
        gleam = 0.3 * lowpass(square(freq, ndur, duty=0.5), 3000.0)
        return (body + gleam) * env_perc(ndur, attack=0.006, tau=tau)

    c = timeline(dur)
    add(c, fanfare_note(C5, 0.18, 0.08), at=0.00)
    add(c, fanfare_note(E5, 0.18, 0.08), at=0.14)
    add(c, fanfare_note(G5, 0.18, 0.08), at=0.28)
    add(c, fanfare_note(C6, 0.22, 0.10), at=0.42)
    # held final note with vibrato + supporting octave below
    hold = 0.60
    ffin = vibrato(E6, hold, rate=5.5, depth=0.012)
    fin = 0.55 * triangle(ffin, hold) + 0.4 * sine(ffin, hold)
    fin += 0.35 * sine(E5, hold)
    fin *= adsr(hold, attack=0.01, decay=0.10, sustain=0.75, release=0.28)
    add(c, fin, at=0.58)
    return lowpass(c, 5500.0)


SOUNDS = {
    "jump.wav": sfx_jump,
    "land.wav": sfx_land,
    "shoot.wav": sfx_shoot,
    "fireball_impact.wav": sfx_fireball_impact,
    "enemy_poof.wav": sfx_enemy_poof,
    "powerup.wav": sfx_powerup,
    "lever.wav": sfx_lever,
    "bridge.wav": sfx_bridge,
    "crystal.wav": sfx_crystal,
    "hurt.wav": sfx_hurt,
    "death.wav": sfx_death,
    "checkpoint.wav": sfx_checkpoint,
    "ui_tap.wav": sfx_ui_tap,
    "swap.wav": sfx_swap,
    "level_complete.wav": sfx_level_complete,
}


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    rows = []
    for name, build in SOUNDS.items():
        x = finalize(build(), peak_db=-3.0, fade_ms=5.0)
        path = OUT_DIR / name
        write_wav(path, x)
        dur = len(x) / SR
        peak_db = 20.0 * np.log10(np.max(np.abs(x)))
        rms_db = 20.0 * np.log10(np.sqrt(np.mean(x ** 2)))
        size = path.stat().st_size
        assert size < 300_000, f"{name} too big: {size} bytes"
        rows.append((name, dur, peak_db, rms_db, size))

    print(f"{'file':<22}{'dur (s)':>8}{'peak dBFS':>11}{'RMS dBFS':>10}{'bytes':>9}")
    for name, dur, pk, rms, size in rows:
        print(f"{name:<22}{dur:>8.3f}{pk:>11.2f}{rms:>10.2f}{size:>9}")
    print(f"\nwrote {len(rows)} files to {OUT_DIR}")


if __name__ == "__main__":
    main()
