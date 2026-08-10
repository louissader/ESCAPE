#!/bin/bash
# Generate the 6 intro-cutscene voice-over lines into Assets/Audio/VO/.
#
# Narration is the SOURCE OF TRUTH here (the on-screen captions in
# IntroCutscene.cs are deliberately shorter — the voice says more than
# the text). Keep beat order in sync with IntroCutsceneBuilder.Captions.
#
# Usage:
#   tools/vogen/generate_vo.sh                 # uses $VOICE below
#   VOICE="Evan (Enhanced)" tools/vogen/generate_vo.sh
#
# Louis: after downloading an Enhanced/Premium voice (System Settings >
# Accessibility > Spoken Content > System Voice > Manage Voices), rerun
# with that voice name. `say -v '?'` lists installed voices.
set -euo pipefail

VOICE="${VOICE:-Daniel}"   # TEMP basic voice until an Enhanced voice is installed
RATE=165                   # words/min — a touch slower than default for gravity

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$REPO/Assets/Audio/VO"
mkdir -p "$OUT"

LINES=(
  "Two friends chased a legend. A cave beyond the mountains, where the fallen stars still burned."
  "They found the way in. The dark opened up before them, and it was already glittering."
  "But the mountain had other plans. The way home vanished behind a wall of stone."
  "The stars scattered into the deep. Every last one, falling farther than light could follow."
  "Deeper than any map had ever dared. Whatever waits down there is holding all the light."
  "There is no way back. The only way out is down. And they will face it together."
)

for i in "${!LINES[@]}"; do
  aiff="$OUT/vo_beat$i.aiff"
  wav="$OUT/vo_beat$i.wav"
  say -v "$VOICE" -r "$RATE" -o "$aiff" "${LINES[$i]}"
  afconvert -f WAVE -d LEI16@44100 -c 1 "$aiff" "$wav"
  rm -f "$aiff"
  dur=$(afinfo "$wav" | awk '/estimated duration/ {print $3}')
  echo "vo_beat$i.wav  ${dur}s  ($VOICE)"
done
echo "Done -> $OUT (voice: $VOICE)"
