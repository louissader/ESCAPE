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
