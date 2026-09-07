# Build 5 (v1.2.0) — App Store rejection remediation

STATUS 2026-09-07: code fixes landed, cold compile clean so far (0 error CS).
DECISIONS: reverted to 8 levels (FirstLevel=1); HIG scope = iPad + egregious
sub-44pt targets. Both ASC account blockers CLEARED (PLA accepted, DSA trader
status = non-trader, Active). Signing fixed at the Unity source.

Rejection: submission abcf9bc1-7a4a-4eb4-959f-a8777f8c0450, v1.1.0 (2),
reviewed 2026-08-19 on **iPad Air 11-inch (M3) / iPadOS 26.6.1**.
Cited: **Guideline 2.3.3** (screenshots not "app in use") and **Guideline 4** (UI crowded on iPad).
No 4.3, no 4.2, no account hold.

## A. Guideline 4 — iPad UI (root cause)
Mechanism: all canvases use ScaleWithScreenSize ref 1920x1080 match 0.5, so
canvasW = 1440*sqrt(aspect). Co-op controls occupy 94.5% of the iPad Air's width
(vs 77% on iPhone); the same d-pad button is 72pt on iPhone and 123pt on iPad.

- [x] A1  ControlMode.cs:32-33 — drop the `deviceModel.StartsWith("iPad")` -> Coop
          branch. Swap everywhere on mobile; Coop becomes explicit opt-in.
- [x] A2  Live control-mode switch. SaveSystem.cs:135-139 raise a change event;
          TouchControlsUI.cs:41 and SwapModeController.cs:41 stop caching in Start().
          (Today the Settings toggle silently does nothing until a scene reload.)
- [ ] A3  TouchControls CanvasScaler m_MatchWidthOrHeight 0.5 -> 0 (match width),
          pinning canvasW at 1920 across aspects. Requires A4.
- [ ] A4  Clamp HowToPlayPanel + SettingsPanel to the visible rect. Btn_Close on
          HOW TO PLAY goes off-screen above aspect 2.41; BasicsControls clips
          horizontally below aspect 1.297 (9pt margin at 4:3).
- [ ] A5  Re-evaluate layout on resize (iPadOS 26 ignores UIRequiresFullScreen,
          TN3192). Reuse the SafeAreaFitter.cs:20-26 watcher pattern.
- [x] A6  Tap targets: MoreLore.unity:557 and Story Details.unity:562 Continue
          buttons are 160x30 units (12pt tall on iPhone); Menu Scene
          Btn_CloseSubmenu is 60x60. Raise to >= 88 units.

## B. Guideline 2.3.3 — screenshots
Root cause: BOTH capture harnesses render through a camera render-texture, and
screen-space overlay canvases do not render that way. Every store image is a bare
world render with no HUD, no touch controls, no pause button, no health bars.

- [ ] B1  Do C1 first — do not capture with broken glyphs on screen.
- [ ] B2  Port the RenderMode flip from LevelSelectCapture.cs:44-51 into
          ScreenshotTests.cs so the HUD actually renders.
- [ ] B3  Capture at 2868x1320 (iPhone 6.9") and 2752x2064 (iPad 13"), landscape.
          Current correct-content set is 1920x1080 = no App Store size class.
- [x] B4  ScreenshotCapture.cs:41 still loops 1..LevelCount and hardcodes levels
          1/5/8. Align to SceneRouter.FirstLevel / DisplayNumber, or retire it.
- [ ] B5  Reframe: orthographicSize 8f with +8/+4 offset leaves heroes at 2-4% of
          frame height in an empty frame.
- [ ] B6  Retire all qa_*.png and the six stale iphone69_*/ipad13_* files.
          iphone69_level1 / ipad13_level1 show a DISABLED level.

## C. New P0 defects (would draw a fresh rejection)
- [x] C1  Missing glyphs. TMP atlas lacks U+25C0 U+25B6 U+21C4 U+2605 U+2606 and
          m_missingGlyphCharacter is 0, so they render as nothing.
          Level 4.unity:4669 is the FIRST instruction a new player sees and
          currently reads "Hold   to move, tap A to jump".
          Also Level 4.unity:9036, LevelEndController.cs:67-68, Level 1.unity:4933/5094.
- [x] C2  HOW TO PLAY tells a touch user to use a KEYBOARD:
          "keyboard: ARROW KEYS + SPACE" / "keyboard: A / D + W + E".
          Source HowToPlayBuilder.cs:152,154; baked into Menu Scene.unity:3970/4477,
          Intro.unity:2183/7521, GameplayUI.prefab:7444/885.
          Introduced in build 3 (e7528d7) — NOT in the declined build. New exposure.
- [x] C3  HAPTICS/VIBRATION toggle is inert on iPad Air (no Taptic Engine) —
          a dead control on the exact device that drew the Guideline 4 citation.
          HUDBuilder.cs:136-137; hide when unsupported.
- [x] C4  MoreLore.unity:778 ending copy — broken mid-sentence wrapping, four
          trailing blank lines, "Press the Restart button" on a touch device.

## D. Version / build 5
- [x] D1  ConfigureMobileSettings.cs:15 "1.1.2" -> "1.2.0"; :31 and :33
          Mathf.Max(4,..) -> Mathf.Max(5,..); refresh the stale comment at :26.
- [x] D2  ProjectSettings.asset:144 -> 1.2.0, :174 iPhone -> 5, :177 -> 5.
- [x] D3  Menu Scene.unity:2279 m_text -> v1.2.0 (cosmetic; TitleMenu overwrites
          from Application.version at runtime).
- [x] D4  DECISION PENDING: ConfigureMobileSettings.cs:58 targetOSVersionString
          "13.0" -> "15.0". Clears Apple's Spring 2027 warning; drops iOS 13/14.

## E. Signing — blocks upload today
- [x] E1  ProjectSettings.asset: appleEnableAutomaticSigning 0 -> 1,
          appleDeveloperTeamID -> JNRU2NQ284. Every fresh export currently
          regenerates CODE_SIGN_IDENTITY="iPhone Developer" with empty
          DEVELOPMENT_TEAM, which cannot produce an App Store archive.

## F. Hygiene
- [ ] F1  rm -rf Builds/iOS before re-export. It holds a COMPLETE stale duplicate
          Xcode project ("Unity-iPhone 2.xcodeproj") from an iCloud sync conflict.
          Archiving the wrong one would silently ship a stale build.
- [ ] F2  EmojiOne ships via TMP Settings m_defaultSpriteAsset under Resources/
          and its license requires visible attribution; the game shows none.
          Either drop the asset or add a credit.
- [ ] F3  Licenses/THIRD-PARTY-LICENSES.md: add LiberationSans (SIL OFL) and
          EmojiOne; remove the "Sad Lullaby" track (not in repo); correct the
          "IndieMarc art fully replaced" claim.

## G. Blocked on Louis (cannot be done for him)
- [ ] G1  Accept the updated Apple Developer Program License Agreement as
          Account Holder. Blocks submitting app updates.
- [ ] G2  Provide EU trader status (Digital Services Act).

## H. Submission strategy
- Reply in Resolution Center BEFORE resubmitting — correspondence closes the
  moment you resubmit, and you get ONE edit on a submission with Unresolved Issues.
- Resubmitting starts a fresh full review; 2026 queue times are days-to-weeks.
- Do NOT request expedited review — neither stated criterion applies.


## Landed since the plan was written (not in the original list)
- [x] Swap-mode camera: SwapModeController now owns the Cinemachine target-group
      weights, blends them, and guards the all-zero-weight NaN case. Fixes the
      active hero walking off-screen past ~49 tiles of hero separation.
- [x] Levels 1-3 duplicate camera + duplicate AudioListener removed (surgical
      YAML). All 6 scenes validated: 0 dangling fileIDs, headers == objects.
      This became live the moment FirstLevel went back to 1.
- [x] MoreLore Continue button 160x30 -> 360x136, AND the `Lore` label's
      raycastTarget cleared: it had been eating the top 37% of that button.
- [x] Tap targets to the 136-unit floor across pause / settings / win / fail /
      ResetConfirm / GOT IT / Btn_Skip / submenu, re-pitched 120 -> 144 so no
      two hit rects can claim one tap. Slider + toggle got invisible hit
      children so the visual pips did not move.
- [x] ToggleGroup on SWAP/COOP (HAPTICS deliberately excluded) — both had
      rendered checked at once.
- [x] Haptics.IsSupported; HAPTICS row hidden on iPad (no Taptic Engine) —
      a dead control on the exact device that drew the Guideline 4 citation.
- [x] ScreenFader no longer leaves an invisible full-screen blended quad
      submitting every frame in every scene for the whole session.
- [x] App icon recentred at the generator (tools/spritegen/icons.py): the whole
      composition was keyed off cy=37 in a 64px canvas whose centre is 32.
      Disc centre y 599 -> 519; margins 192/18 -> 112/98.
- [x] Store screenshot rig rewritten (StoreScreenshotRig.cs) to render the HUD
      and capture at 2868x1320 + 2752x2064. NEEDS VERIFICATION.

## Next
- [ ] REBAKE (required — builder edits are inert until baked):
      Escape/Run Playtest-Fix Pipeline, order BuildGameplayUIPrefab ->
      BuildIntroCutscene -> BuildHowToPlayPanels -> BuildTitleScreen.
      Menu Scene + Intro are baked and DO need it; level scenes hold
      GameplayUI as a prefab instance so they do not.
- [ ] Full test suite (EditMode then PlayMode; no -quit with -runTests,
      no -nographics for PlayMode).
- [ ] Build 5, verify Info.plist reads 1.2.0 / 5 and deployment target 15.0.
- [ ] Capture store screenshots from the FIXED build.
- [ ] Resolution Center reply, then Resubmit.
