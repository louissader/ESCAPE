using System;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Runs the entire asset/scene pipeline in one editor session (no step
    /// below changes code, so no domain reloads are needed between them).
    /// Steps log-and-continue on failure unless a dependency makes
    /// continuing meaningless; the summary at the end is the contract.
    /// </summary>
    public static class MasterPipeline
    {
        [MenuItem("Escape/Run Master Pipeline")]
        public static void RunAll()
        {
            int failed = 0;

            // Diagnostic: GraphicsSettings' m_CustomRenderPipeline guid looks
            // dangling on disk — log what actually resolves so the mobile
            // render tuning (HDR, quality tiers) is known to be live or moot.
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Debug.Log($"[MasterPipeline] Active render pipeline: {(rp != null ? rp.name : "NULL (built-in)")}");

            failed += Step("RemoveMissingScripts", ProjectMaintenance.RemoveMissingScripts);
            failed += Step("SliceGeneratedSprites", SliceGeneratedSprites.Run);
            failed += Step("BuildHeroAnimators", AnimatorBuilder.BuildHeroAnimators);
            failed += Step("PrefabVisualOverhaul", PrefabVisualOverhaul.Run);
            failed += Step("BuildTouchControlsPrefab", TouchControlsBuilder.BuildTouchControlsPrefab);
            failed += Step("BuildGameplayUIPrefab", HUDBuilder.BuildGameplayUIPrefab);
            failed += Step("ConfigureAudio", AudioSetup.ConfigureAudio);
            failed += Step("ExtractExistingLevels", LevelExtractor.ExtractExistingLevels);
            failed += Step("CreateLevelDefinitions", LevelLibrary.CreateAll);
            failed += Step("BuildAllLevels", LevelBuilder.BuildAllLevels);
            // Powerup pass must FOLLOW level building: BuildAllLevels re-instantiates
            // generated-level pickups (and legacy scenes are re-saved by the augment pass).
            failed += Step("FixPowerupVisuals", PlaytestFixes.FixPowerupVisuals);
            failed += Step("RebuildLevelSelect", LevelMenuBuilder.Run);
            failed += Step("EnsureMoreLoreSafeArea", ProjectMaintenance.EnsureMoreLoreSafeArea);
            // After BuildGameplayUIPrefab (which rebuilds that prefab from
            // scratch) and after BuildAllLevels (whose GEN_ sweep clears the
            // Level-1 easing objects).
            failed += Step("BuildIntroCutscene", IntroCutsceneBuilder.Run);
            failed += Step("BuildHowToPlayPanels", HowToPlayBuilder.Run);
            // Title screen LAST of the menu passes: HowToPlayBuilder rebuilds
            // the menu panel object TitleMenu points at, so the wiring must
            // happen after it (running it earlier would leave a dead ref).
            failed += Step("BuildTitleScreen", TitleScreenBuilder.Run);
            failed += Step("EaseLevelOne", PlaytestFixes.EaseLevelOne);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MasterPipeline] COMPLETE — {failed} step(s) failed" +
                      (failed == 0 ? " 🎉" : " (see errors above)"));
            if (failed > 0)
                EditorApplication.Exit(2);
        }

        [MenuItem("Escape/Run Playtest-Fix Pipeline")]
        public static void RunPlaytestFixes()
        {
            int failed = 0;
            // Sprites first (doors, 6-frame lava, 32px orbs, cutscene plates).
            failed += Step("SliceGeneratedSprites", SliceGeneratedSprites.Run);
            failed += Step("PlayerPhysics+Speed", PlaytestFixes.FixPlayerPhysics);
            failed += Step("PrefabVisualOverhaul", PrefabVisualOverhaul.Run);
            failed += Step("BuildGameplayUIPrefab", HUDBuilder.BuildGameplayUIPrefab);
            failed += Step("BuildTouchControlsPrefab", TouchControlsBuilder.BuildTouchControlsPrefab);
            // Intro scene (incl. post-cutscene explainer); the how-to-play
            // pass must follow BuildGameplayUIPrefab — that step rebuilds the
            // prefab from scratch and would drop the panel. Title screen
            // follows how-to-play: it wires TitleMenu at the menu panel that
            // pass just rebuilt.
            failed += Step("BuildIntroCutscene", IntroCutsceneBuilder.Run);
            failed += Step("BuildHowToPlayPanels", HowToPlayBuilder.Run);
            failed += Step("BuildTitleScreen", TitleScreenBuilder.Run);
            // Doors + animated lava wire up inside BuildAllLevels (generated
            // + legacy augment paths), followed by the solvability gate.
            failed += Step("BuildAllLevels", LevelBuilder.BuildAllLevels);
            // Powerup pass must FOLLOW level building: BuildAllLevels re-instantiates
            // generated-level pickups (and legacy scenes are re-saved by the augment pass).
            failed += Step("FixPowerupVisuals", PlaytestFixes.FixPowerupVisuals);
            failed += Step("StripLegacyUI", PlaytestFixes.StripLegacyUI);
            // After BuildAllLevels: its GEN_ sweep clears the easing objects.
            failed += Step("EaseLevelOne", PlaytestFixes.EaseLevelOne);
            failed += Step("RebuildLevelSelect", LevelMenuBuilder.Run);
            failed += Step("FixNullMaterials", FixNullMaterials.Run);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MasterPipeline] PLAYTEST FIXES COMPLETE — {failed} step(s) failed" +
                      (failed == 0 ? " 🎉" : ""));
            if (failed > 0)
                EditorApplication.Exit(2);
        }

        static int Step(string name, Action action)
        {
            Debug.Log($"[MasterPipeline] ▶ {name}");
            try
            {
                action();
                Debug.Log($"[MasterPipeline] ✓ {name}");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MasterPipeline] ✗ {name}: {e}");
                return 1;
            }
        }
    }
}
