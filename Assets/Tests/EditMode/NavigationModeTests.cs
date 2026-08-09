using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Build-4 regression lock: touch UI has no focus concept, so every
/// Selectable the builders bake must have Navigation.Mode.None — default
/// (Automatic) navigation latches the "selected" tint after each tap.
/// Runs against the baked artifacts on disk, so it also catches a builder
/// regression that only shows up after a rebake.
/// </summary>
public class NavigationModeTests
{
    static void AssertAllNavigationNone(GameObject root, string context)
    {
        foreach (Selectable s in root.GetComponentsInChildren<Selectable>(true))
            Assert.AreEqual(Navigation.Mode.None, s.navigation.mode,
                $"{context}: '{s.gameObject.name}' ({s.GetType().Name}) " +
                "has non-None navigation — it will hold the selected tint after a tap");
    }

    [Test]
    public void GameplayUIPrefab_AllSelectables_NavigationNone()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameplayUI.prefab");
        Assert.IsNotNull(prefab, "GameplayUI.prefab missing");
        AssertAllNavigationNone(prefab, "GameplayUI.prefab");
    }

    [Test]
    public void UiScenes_AllSelectables_NavigationNone()
    {
        string[] scenePaths =
        {
            "Assets/Scenes/Menu Scene.unity",
            "Assets/Scenes/Intro.unity",
            "Assets/Scenes/Level Scene.unity",
            "Assets/Scenes/MoreLore.unity",
        };
        foreach (string path in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (GameObject root in scene.GetRootGameObjects())
                AssertAllNavigationNone(root, path);
        }
    }
}
