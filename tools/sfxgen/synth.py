"""Deterministic numpy synthesis primitives for ESCAPE's procedural SFX pipeline.

Everything here is pure-function and seed-driven: given the same inputs the
output arrays (and therefore the WAV bytes) are identical on every run.

Conventions:
  * Sample rate is fixed at 44100 Hz, mono, float64 in [-1, 1] internally.
  * Oscillators accept a frequency that is either a scalar (Hz) or a
    per-sample array (for sweeps/vibrato) and a duration in seconds.
  * write_wav() emits 16-bit PCM and asserts against clipping.
"""

import wave
from pathlib import Path

import numpy as np

SR = 44100  # samples per second


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def samples(dur):
    """Number of samples for a duration in seconds."""
    return int(round(dur * SR))


def _freq_array(freq, n):
    """Broadcast a scalar or array frequency to exactly n samples."""
    f = np.asarray(freq, dtype=np.float64)
    if f.ndim == 0:
        return np.full(n, float(f))
    if len(f) != n:
        raise ValueError(f"frequency array length {len(f)} != {n}")
    return f


def _phase(freq, n):
    """Integrated phase in cycles (not radians) for a possibly-varying freq."""
    f = _freq_array(freq, n)
    return np.cumsum(f) / SR


# ---------------------------------------------------------------------------
# oscillators
# ---------------------------------------------------------------------------

def sine(freq, dur):
    n = samples(dur)
    return np.sin(2.0 * np.pi * _phase(freq, n))


def square(freq, dur, duty=0.5):
    n = samples(dur)
    ph = _phase(freq, n) % 1.0
    return np.where(ph < duty, 1.0, -1.0)


def triangle(freq, dur):
    n = samples(dur)
    # phase offset 0.25 so the wave starts at 0 (click-free onset)
    ph = (_phase(freq, n) + 0.25) % 1.0
    return 4.0 * np.abs(ph - 0.5) - 1.0


def noise(dur, seed):
    """White noise; seed is mandatory to keep output deterministic."""
    rng = np.random.default_rng(seed)
    return rng.uniform(-1.0, 1.0, samples(dur))


# ---------------------------------------------------------------------------
# pitch utilities
# ---------------------------------------------------------------------------

def pitch_sweep(f0, f1, dur, curve="exp"):
    """Per-sample frequency array sweeping f0 -> f1 over dur seconds."""
    n = samples(dur)
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    if curve == "exp":
        return f0 * (f1 / f0) ** t
    if curve == "linear":
        return f0 + (f1 - f0) * t
    raise ValueError(f"unknown curve {curve!r}")


def vibrato(freq, dur, rate=6.0, depth=0.02):
    """Wrap a scalar/array frequency with sinusoidal vibrato.

    depth is fractional (0.02 = +/-2% pitch wobble).
    """
    n = samples(dur)
    f = _freq_array(freq, n)
    t = np.arange(n) / SR
    return f * (1.0 + depth * np.sin(2.0 * np.pi * rate * t))


# ---------------------------------------------------------------------------
# envelopes
# ---------------------------------------------------------------------------

def adsr(dur, attack=0.01, decay=0.05, sustain=0.6, release=0.05):
    """Classic linear ADSR over dur seconds (release ends exactly at dur)."""
    n = samples(dur)
    na = min(samples(attack), n)
    nd = min(samples(decay), n - na)
    nr = min(samples(release), n - na - nd)
    ns = n - na - nd - nr
    parts = [
        np.linspace(0.0, 1.0, na, endpoint=False),
        np.linspace(1.0, sustain, nd, endpoint=False),
        np.full(ns, sustain),
        np.linspace(sustain, 0.0, nr, endpoint=True) if nr else np.empty(0),
    ]
    return np.concatenate(parts)


def env_perc(dur, attack=0.004, tau=0.06):
    """Percussive envelope: fast linear attack, exponential decay (tau sec)."""
    n = samples(dur)
    t = np.arange(n) / SR
    env = np.exp(-np.maximum(t - attack, 0.0) / tau)
    ramp = t < attack
    env[ramp] *= t[ramp] / attack
    return env


def env_swell(dur, rise=0.4, fall=0.4):
    """Raised-cosine swell: rise to 1 over `rise` fraction, fall over `fall`."""
    n = samples(dur)
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    env = np.ones(n)
    up = t < rise
    env[up] = 0.5 - 0.5 * np.cos(np.pi * t[up] / rise)
    down = t > (1.0 - fall)
    env[down] = 0.5 - 0.5 * np.cos(np.pi * (1.0 - t[down]) / fall)
    return env


# ---------------------------------------------------------------------------
# effects
# ---------------------------------------------------------------------------

def lowpass(x, cutoff):
    """One-pole lowpass. cutoff may be scalar Hz or a per-sample array
    (sweepable). Soft-edged by design -- perfect for the cozy aesthetic."""
    n = len(x)
    fc = _freq_array(cutoff, n)
    alpha = 1.0 - np.exp(-2.0 * np.pi * fc / SR)
    y = np.empty(n)
    acc = 0.0
    for i in range(n):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def bitcrush(x, bits=8, rate_div=1):
    """Retro flavor: amplitude quantization + sample-and-hold rate reduction.

    Use high bit depths (8-10) and small rate_div for gentle grit; this game
    is cozy, not arcade-harsh.
    """
    y = x
    if rate_div > 1:
        idx = (np.arange(len(x)) // rate_div) * rate_div
        y = y[idx]
    if bits is not None:
        q = float(2 ** (bits - 1))
        y = np.round(y * q) / q
    return y


# ---------------------------------------------------------------------------
# arrangement
# ---------------------------------------------------------------------------

def timeline(dur):
    """Silent canvas of dur seconds to place events onto."""
    return np.zeros(samples(dur))


def add(canvas, x, at=0.0, gain=1.0):
    """Mix signal x into canvas starting at `at` seconds (clipped to fit)."""
    i = int(round(at * SR))
    end = min(len(canvas), i + len(x))
    if end > i:
        canvas[i:end] += gain * x[: end - i]
    return canvas


# ---------------------------------------------------------------------------
# mastering / output
# ---------------------------------------------------------------------------

def fade(x, ms=5.0):
    """Raised-cosine fade-in and fade-out to kill onset/offset clicks."""
    n = min(samples(ms / 1000.0), len(x) // 2)
    if n <= 0:
        return x
    ramp = 0.5 - 0.5 * np.cos(np.pi * np.linspace(0.0, 1.0, n, endpoint=False))
    y = x.copy()
    y[:n] *= ramp
    y[-n:] *= ramp[::-1]
    return y


def normalize(x, peak_db=-3.0):
    """Peak-normalize to peak_db dBFS."""
    peak = np.max(np.abs(x))
    if peak == 0.0:
        raise ValueError("cannot normalize silence")
    return x * (10.0 ** (peak_db / 20.0) / peak)


def finalize(x, peak_db=-3.0, fade_ms=5.0):
    """Standard mastering chain: fade edges, normalize, assert headroom."""
    y = normalize(fade(x, fade_ms), peak_db)
    peak = np.max(np.abs(y))
    assert peak <= 10.0 ** (peak_db / 20.0) + 1e-9, f"peak {peak} over target"
    return y


def write_wav(path, x):
    """Write mono 16-bit 44.1 kHz PCM. Asserts against clipping."""
    peak = np.max(np.abs(x))
    assert peak <= 1.0, f"clipping: peak {peak} > 1.0 in {path}"
    pcm = np.round(x * 32767.0).astype(np.int16)
    assert np.max(np.abs(pcm.astype(np.int64))) <= 32767, "int16 overflow"
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
