# ESCAPE — Release Checklist (v1.2.0, build 5)

State of the build-out as of 2026-07-11 on branch `mobile-release`.
Everything below the "Human actions" line requires accounts/credentials only
a human holds; everything above it was automated and verified in-repo.

## What shipped in this build-out

- Unity upgraded 2022.3.45f1 → **2022.3.62f3** (required: 16 KB page-size
  support for Google Play API 35+ targets, target API 36 platform module).
- Touch controls: Swap mode (single pilot, auto-swap on exit/death, parked
  character invulnerable + untargeted) and 2-player Co-op mode (mirrored
  edge clusters). Safe-area aware, editor keyboard input still works.
- All audited bugs fixed (B1-B14), including: progress wipe on every launch,
  replay over-unlock, fake restart, hardcoded scene indices, the 0.1s bat
  fire-rate flood, and the fireball-ownership rewrite that makes bats
  killable and enemy shots damaging.
- Death redesign: HP 100 / 30 contact / 20 fireball / 1s i-frames; single
  death respawns at the checkpoint nearest the surviving character; level
  fails only when both are dead at once (except during the L8 lava chase).
- Consistent 16px pixel art: two DISTINCT generated hero sprite sheets
  (EMBER and WISP — silhouettes, not recolors), re-skinned lever/bridge/
  lava/platform-form/exit-portal props, 2-zone parallax backgrounds, URP 2D
  lights, app icons. Deterministic pipelines: `tools/spritegen`,
  `tools/sfxgen` (regenerate: `tools/.venv/bin/python tools/<x>/generate_all.py`).
- 5 shipped levels (legacy 1-3 disabled via SceneRouter.FirstLevel — set
  back to 1 and rebake to restore; displayed to players as 1-5) via LevelDefinition assets +
  LevelBuilder, each gated by an automated solvability check. Crystals
  (3/level) drive star ratings; torch checkpoints; hearts.
- Audio: menu + 2 zone themes (GLYNMOOG, Streaming/Vorbis 0.7) and 15
  synthesized SFX wired to every gameplay event. Music/SFX sliders.
  NOTE: no AudioMixer asset (not creatable from editor code) — two gain
  stages in AudioManager give the same user-facing result.
- Store config: `com.louissader.escape` v1.2.0 (build 5), IL2CPP,
  **ARM64-only** (the old project shipped ARMv7+X86 with no 64-bit slice at
  all!), minSdk 24 / target **36**, AAB, TV-compat off, predictive-back
  disabled via custom AndroidManifest, iOS 15.0+, landscape-locked,
  bottom-edge deferred system gestures, adaptive + flat icons, splash color
  matched.

## Verified automatically (2026-07-11)

- [x] `Builds/Android/ESCAPE.aab` built: **28 MB**, ARM64-only, IL2CPP,
      target API 36. All four .so files LOAD-aligned **0x4000 (16 KB)**
      (lib_burst_generated, libil2cpp, libmain, libunity) — Play compliant.
- [x] `Builds/iOS/Unity-iPhone.xcodeproj` exported;
      `UnityFramework/PrivacyInfo.xcprivacy` present; Info.plist confirms
      UIRequiresFullScreen + landscape-only orientations.
- [x] Tests: EditMode 7/7, PlayMode 4/4 (incl. B1/B2 regressions, all-8-
      levels smoke, virtual-input drive, auto-swap handoff, B14 bat-kill).
- [x] Levels 4-8 pass automated solvability; L1-3 keep original
      human-verified geometry (augmented in place).
- [x] Android graphics: Vulkan + GLES3 (legacy GLES2 entry removed —
      incompatible with the project's Linear color space).

## Verify on device (human)

- [ ] Touch controls on a phone (Swap) and tablet (Co-op).
- [ ] Android 15/16 back-gesture pauses the game (predictive back disabled).
- [ ] iPad windowed aspect ratios (~4:3 and ~2.4:1) keep HUD/camera sane
      (UIRequiresFullScreen is deprecated as of iPadOS 26 — see TN3192).
      The App Store Guideline 4 rejection proved this was a real problem;
      fixes since landed: Swap mode is now the default on ALL touch devices,
      control mode can be switched live, tap targets enlarged to 136u, and
      the duplicate cameras in Levels 1-3 were removed. Still needs
      on-device verification on an iPad in windowed/Stage Manager sizes.

## Human actions — Apple

1. Xcode: open `Builds/iOS/Unity-iPhone.xcodeproj`, set your Team, archive.
   **Upload requires Xcode 26+ / iOS 26 SDK (Apple hard rule since
   2026-04-28, TN3192).** Xcode 26.6 is installed on this machine.
2. App Store Connect: create the app. **The name "ESCAPE" is almost
   certainly taken** — fallbacks: "ESCAPE: Cave Duo", "ESCAPE Together",
   "Grotto ESCAPE". Bundle id stays `com.louissader.escape` either way.
3. Screenshots (from `StoreAssets/` or fresh captures): REQUIRED sets are
   6.9" iPhone 2868x1320 landscape and 13" iPad 2752x2064 landscape
   (PNG/JPEG, RGB, no alpha, max 10 each; smaller sizes auto-scale).
4. App Privacy: **"Data Not Collected"** (no ads/analytics/IAP/network).
5. Age rating questionnaire: mild fantasy violence. Category: Games > Puzzle.
   Primary language: English (U.S.). TestFlight a build before release.

## Human actions — Google Play

1. Generate the release keystore (KEEP IT SAFE — losing it means losing the
   app identity):
   `keytool -genkeypair -v -keystore escape-release.keystore -alias escape \
      -keyalg RSA -keysize 2048 -validity 10000`
2. Rebuild signed:
   `export ESCAPE_KEYSTORE_PATH=... ESCAPE_KEYSTORE_PASS=... \
      ESCAPE_KEYSTORE_ALIAS=escape ESCAPE_KEYSTORE_ALIAS_PASS=...`
   then run BuildScript.BuildAndroid (Escape menu or -executeMethod).
   Unsigned/debug artifacts are watermarked with a loud console WARNING.
3. Play Console: create app; upload AAB to internal testing first.
   Data safety: **"No data collected or shared."** Content rating
   questionnaire: mild fantasy violence. Target API 36 confirmation is
   automatic from the manifest.
4. Store listing: phone screenshots (16:9, min 1080px), 7" + 10" tablet
   sets, feature graphic 1024x500, short + full description.

## Licensing — VERIFIED (audit completed 2026-08-04, updated 2026-09-07)

Full details and archived evidence live in `Licenses/THIRD-PARTY-LICENSES.md`
(Wayback snapshots committed alongside it).

- **Grotto Escape packs (ansimuz)**: CC0 — itch.io metadata reads "Creative
  Commons Zero v1.0 Universal"; Wayback capture of the Grotto Escape II page
  committed in `Licenses/`. Commercial use OK, no attribution required.
- **GLYNMOOG "Free Music Pack: Melodic Themes vol.1"** (music pack): page
  description grants "Full Unrestricted License to use as you please for
  personal or commercial use - crediting is appreciated." Wayback capture
  committed in `Licenses/`. Three tracks ship (01 Mysterious Force Loop,
  02 Desolation Loop, 03 Solitude Wonder).
- **IndieMarc "Simple 2D Platformer Template"**: Unity Asset Store EULA —
  fine to ship embedded in a game. Code scaffolding plus the lever sprites
  still ship (player art was replaced by generated EMBER/WISP sheets).
- **LiberationSans** (TMP default font): SIL Open Font License 1.1; license
  text committed at `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.
- **EmojiOne/JoyPixels sprite sheet** (TMP sample; free tier requires
  visible attribution): NOT used — no sprite tags or references anywhere,
  and the TMP Settings default-sprite-asset reference was cleared
  2026-09-07. It still sits under a `Resources/` folder, so DELETE
  `Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset` inside the
  Unity editor before the next build to keep its bytes out entirely.
- Unity Personal splash stays (mandatory on Personal license, 2022.3).

## Known deferred items

- Human playtest QA: feel/difficulty per level in BOTH control modes
  (automated solvability ≠ fun); Profiler pass on a mid-tier device.
- Screen-shake (Cinemachine impulse), squash/stretch, and particle FX are
  wired where cheap; a dedicated juice pass would deepen them.
- SpriteAtlas packing + texture-memory audit (Phase 9 target < 50 MB).
- The abandoned `ESCAPE-Mobile` (SpriteKit) and `Desktop/Swift/ESCAPE`
  folders are superseded by this project and safe to delete.
