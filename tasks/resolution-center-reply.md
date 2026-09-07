# Resolution Center reply — draft (submission abcf9bc1-7a4a-4eb4-959f-a8777f8c0450)

RULES (from Apple docs, verified):
- Reply BEFORE resubmitting; correspondence closes at resubmission.
- 4000-char limit; attachments allowed. One edit per rejected item.
- Send from Account Holder (Louis) in ASC.

PRE-SEND CHECKLIST — every claim below must be TRUE before sending:
- [ ] Build 5 (1.2.0) uploaded and attached to the submission
- [ ] New store_* screenshots uploaded for BOTH 6.9" iPhone and 13" iPad
- [ ] On-device iPad check done (or wording softened to remove the claim)
- [ ] Optional but strong: screen recording on an 11" iPad attached

---

Hello,

Thank you for the detailed review. We have addressed both issues in a new
build (1.2.0, build 5) and updated metadata.

Guideline 4 (Design) — root cause found and fixed. On iPad, version 1.1.0
defaulted to a two-player co-op control scheme: two mirrored control
clusters (d-pad plus buttons on both edges) covering roughly 94% of the
screen width on the 11-inch iPad Air. For a single player this was
crowded and difficult to use, exactly as your reviewer described. In 1.2.0:

1. Every touch device now defaults to the single-player control layout
   (one d-pad, left side only). The two-player co-op layout is a
   deliberate opt-in via Settings, intended for two people sharing one
   device.
2. Changing the control mode in Settings now applies immediately, during
   play. In 1.1.0 the change silently required a level restart, which
   made the setting appear broken.
3. All interactive controls now meet or exceed the 44pt minimum tap
   target on every supported device, and modal layouts were re-spaced so
   controls cannot crowd or overlap at iPad aspect ratios.
4. Settings controls that do not apply to the current device (e.g. the
   haptics toggle on iPad, which has no Taptic Engine) are now hidden
   rather than shown inert.
5. The full 8-level campaign with its tutorial progression is included.

We verified the new layout on the iPad Air 11-inch form factor, including
iPadOS 26 resizable windowing. [ATTACH: short screen recording]

Guideline 2.3.3 (Screenshots) — you were right: our previous screenshots
were world renders that did not include the game's interface. We have
replaced every screenshot in both the 6.9-inch iPhone and 13-inch iPad
sets with direct captures of actual gameplay showing the full in-game UI:
touch controls, health bars, hero portraits, objective banner, and pause
control — one screenshot per campaign level.

Both fixes are in build 5 (1.2.0), attached to this submission along with
the updated screenshot sets.

Thank you again for the specific, actionable feedback.

Louis Sader

---
(char count target: comfortably under 4000 — current body ~1900)
