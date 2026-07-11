using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Repairs renderers whose material asset was deleted (they render
    /// magenta): assigns the built-in Sprites-Default material, which the
    /// URP 2D renderer supports (97 healthy renderers already use it).
    /// Sweeps every scene and prefab.
    /// </summary>
    public static class FixNullMaterials
    {
        [MenuItem("Escape/Fix Null Materials")]
        public static void Run()
        {
            Material spritesDefault = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (spritesDefault == null)
            {
                Debug.LogError("[FixNullMaterials] built-in Sprites-Default not found");
                return;
            }

            int fixedCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int n = FixHierarchy(root, spritesDefault);
                if (n > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[FixNullMaterials] {path}: {n} fixed");
                    fixedCount += n;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    n += FixHierarchy(root, spritesDefault);
                if (n > 0)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[FixNullMaterials] {path}: {n} fixed");
                    fixedCount += n;
                }
            }

            Debug.Log($"[FixNullMaterials] done — {fixedCount} renderer(s) repaired");
        }

        static int FixHierarchy(GameObject root, Material fallback)
        {
            int n = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                bool broken = m == null || m.shader == null || !m.shader.isSupported;
                // Also normalize the one legacy Sprites/Diffuse (lit under
                // built-in RP, pointless under URP 2D) for uniformity.
                bool legacyDiffuse = m != null && m.shader != null && m.shader.name == "Sprites/Diffuse";
                if (broken || legacyDiffuse)
                {
                    r.sharedMaterial = fallback;
                    n++;
                }
            }
            return n;
        }
    }
}
