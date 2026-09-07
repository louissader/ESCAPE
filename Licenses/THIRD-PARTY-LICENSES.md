# Third-Party Asset Licenses — ESCAPE: Cave Duo

License verification completed 2026-08-04, prior to first App Store release.
Snapshots of delisted pages are stored alongside this file as evidence.

## Grotto Escape art packs (ansimuz) — COMMERCIAL OK, no attribution required

- **License:** Creative Commons Zero v1.0 Universal (CC0)
- **Live page:** https://ansimuz.itch.io/grotto-escape-game-art-pack — itch.io
  metadata table reads "Asset license: Creative Commons Zero v1.0 Universal".
  A Sep 2023 devlog confirms the Grotto Escape II expansion content was folded
  into this pack's downloads.
- **Grotto Escape II** (original page now 404): archived at
  https://web.archive.org/web/20220529062609/https://ansimuz.itch.io/grotto-escape-ii-art-pack
  (local copy: `grotto-escape-ii-itch-page-wayback-2022-05-29.html`).
  Description states: "You may use these assets in personal or commercial
  projects. You may modify these assets to suit your needs. Credit is not
  required but appreciated it."

## Melodic Themes vol.1 (GLYNMOOG) — COMMERCIAL OK, credit appreciated but not required

- **License:** Custom grant in the page description (no CC license row).
- **Original page** https://glynmoog.itch.io/faceless-themes-vol1 (display
  title "Free Music Pack: Melodic Themes vol.1") was delisted sometime after
  2025-07-27 and now returns 404. Delisting does not revoke the license
  granted at distribution time.
- **Evidence:** Wayback snapshot 2025-07-27,
  https://web.archive.org/web/20250727125329/https://glynmoog.itch.io/faceless-themes-vol1
  (local copy: `glynmoog-melodic-themes-vol1-itch-page-wayback-2025-07-27.html`).
  Description states: "You are granted Full Unrestricted License to use as
  you please for personal or commercial use - crediting is appreciated."
- Tracks used: 3 of the pack's tracks are in the repo and shipped — "01
  Mysterious Force Loop", "02 Desolation Loop", "03 Solitude Wonder" — wired
  name-based via AudioSetup (menu + 2 zone themes). "Sad Lullaby" is not in
  the repo and is not used.

## IndieMarc "Simple 2D Platformer Template"

- Unity Asset Store EULA — permitted to ship embedded in a game build.
- What actually ships (verified 2026-09-07): the code scaffolding (character
  controllers, Lever/Bridge scripts) AND some template art. The player
  character art was replaced — Player1/Player2 prefabs reference the
  generated EMBER/WISP sheets and Hero1/Hero2 animator controllers — but
  `Prefabs/Lever.prefab` (instanced in all 8 level scenes) still references
  the template's `Sprites/Lever/Lever_position01/02/03.png`, so those lever
  sprites ship and render in-game. The remaining template art
  (Character sprites/animations, Lever_position05, Circle/CircleBlur) sits
  unreferenced in `Assets/IndieMarc/PlatformerDemo/Sprites/` and is excluded
  from builds by Unity's dependency pruning. The Asset Store EULA covers
  shipping the art either way; no attribution required.

## LiberationSans (TextMesh Pro default font) — SHIPS, OFL

- **License:** SIL Open Font License 1.1. Full license text is committed at
  `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.
- `LiberationSans SDF.asset` (under `Assets/TextMesh Pro/Resources/`) is the
  project's default TMP font asset, so the font data compiles into every
  build. The OFL permits bundling/embedding in commercial software; the
  reserved-font-name and license-text conditions are satisfied by shipping
  the font unrenamed and keeping the OFL text in-repo.

## EmojiOne / JoyPixels sprite sheet (TextMesh Pro sample) — NOT USED

- TMP's bundled `EmojiOne.asset` sprite sheet (free-tier license requires
  visible attribution) is present in the project source at
  `Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset` but is NOT
  used: no scene, prefab, or script contains a `<sprite>` tag or a sprite
  asset reference, and the `m_defaultSpriteAsset` reference in TMP Settings
  was cleared on 2026-09-07. The game displays no EmojiOne artwork, so no
  attribution is owed for what players see.
- CAVEAT: because the file lives under a `Resources/` folder, Unity still
  compiles its bytes into builds regardless of references. It is flagged for
  manual deletion inside the Unity editor (delete
  `Sprite Assets/EmojiOne.asset` via the Project window so the .meta stays
  in sync) before cutting the next build.

## First-party generated assets

- Hero sprites (EMBER/WISP), SFX, and level art variants were generated
  in-repo via `tools/spritegen` and `tools/sfxgen` — no external license.
