using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Escape.EditorAutomation
{
    public static class ProjectMaintenance
    {
        /// <summary>
        /// Batchmode no-op: reaching this method proves scripts compile.
        ///   Unity -batchmode -quit -executeMethod Escape.EditorAutomation.ProjectMaintenance.CompileCheck
        /// </summary>
        public static void CompileCheck()
        {
            Debug.Log("[ProjectMaintenance] Compile check OK.");
        }

        /// <summary>
        /// MoreLore is the one hand-made UI scene no builder owns; give its
        /// canvas the same SafeArea wrapper the built scenes get so the
        /// ending content clears the notch / home-indicator bands.
        /// Idempotent (guarded find-or-create, mirrors LevelMenuBuilder).
        /// </summary>
        [MenuItem("Escape/Ensure MoreLore Safe Area")]
        public static void EnsureMoreLoreSafeArea()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MoreLore.unity");
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ProjectMaintenance] MoreLore has no Canvas");
                return;
            }

            Transform canvasT = canvas.transform;
            Transform safeT = canvasT.Find("SafeArea");
            if (safeT == null)
            {
                var go = new GameObject("SafeArea", typeof(RectTransform));
                go.transform.SetParent(canvasT, false);
                safeT = go.transform;
            }
            var rect = (RectTransform)safeT;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (safeT.GetComponent<SafeAreaFitter>() == null)
                safeT.gameObject.AddComponent<SafeAreaFitter>();

            // Reparent every other canvas child under the panel, preserving
            // order (reverse walk + SetAsFirstSibling keeps it stable).
            int moved = 0;
            for (int i = canvasT.childCount - 1; i >= 0; i--)
            {
                Transform child = canvasT.GetChild(i);
                if (child == safeT)
                    continue;
                child.SetParent(safeT, false);
                child.SetAsFirstSibling();
                moved++;
            }

            // Same touch-UI rule the builders apply: no focus concept, so
            // taps must not latch the "selected" tint (Continue button).
            foreach (var sel in canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                sel.navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.None
                };

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ProjectMaintenance] MoreLore SafeArea ensured ({moved} child(ren) moved)");
        }

        /// <summary>
        /// Strips missing-script components left by the Phase 0 dead-code
        /// purge from every prefab and scene (CharacterHoldItem on the player
        /// prefabs, SkeletonBehavior on Skeleton.prefab, etc.).
        /// </summary>
        [MenuItem("Escape/Remove Missing Scripts Project-Wide")]
        public static void RemoveMissingScripts()
        {
            int total = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int removed = RemoveFromHierarchy(root);
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[ProjectMaintenance] {path}: removed {removed} missing script(s)");
                    total += removed;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int removed = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    removed += RemoveFromHierarchy(root);
                if (removed > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[ProjectMaintenance] {path}: removed {removed} missing script(s)");
                    total += removed;
                }
            }

            Debug.Log($"[ProjectMaintenance] RemoveMissingScripts done, {total} removed.");
        }

        static int RemoveFromHierarchy(GameObject root)
        {
            int removed = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            return removed;
        }
    }
}
