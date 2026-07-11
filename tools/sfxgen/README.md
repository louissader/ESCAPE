# sfxgen — procedural SFX pipeline for ESCAPE

Deterministic, numpy-only synthesis of all game sound effects. Every run
produces byte-identical WAVs (fixed seeds, pure math, plain PCM output), so
the generated files in `Assets/Audio/SFX/` are fully reproducible from code.

## Output

15 sounds, written to `Assets/Audio/SFX/`:

44.1 kHz, 16-bit, mono WAV. Peak-normalized to -3 dBFS, 5 ms raised-cosine
fade-in/out on every file, clipping asserted against at write time.

## Regenerating

From the repo root:

```sh
# one-time setup (skip if tools/.venv already exists)
python3 -m venv tools/.venv
tools/.venv/bin/pip install numpy

# generate (overwrites Assets/Audio/SFX/*.wav in place)
tools/.venv/bin/python tools/sfxgen/generate_all.py
```

The script prints a table of duration / peak / RMS / size per file and fails
loudly (assert) on clipping or oversized output.

Because output is byte-identical, regenerating never dirties git unless you
actually changed the synthesis code.

## Files

- `synth.py` — reusable primitives: sine/square/triangle/noise oscillators,
  ADSR and percussive envelopes, pitch sweeps, vibrato, one-pole lowpass,
  bitcrush, timeline mixing, mastering (`finalize`) and WAV writing.
- `generate_all.py` — one builder function per sound effect plus the
  output/verification loop. Edit a builder, rerun, done.

## Style guide for new sounds

Bright and retro-chiptune-adjacent, but soft-edged — ESCAPE is a cozy cave
puzzler, not a harsh arcade cab:

- Prefer triangle/sine cores; if you use `square()`, always `lowpass()` it.
- Keep `bitcrush()` gentle (8-10 bits, small `rate_div`); 6 bits is reserved
  for deliberately crunchy impacts.
- Always pass `seed=` to `noise()` (it is mandatory) — pick a new fixed seed
  per sound so determinism holds.
- End every builder's output through `finalize()` via the main loop; do not
  hand-normalize.
