using NUnit.Framework;
using UnityEditor;
using System.Linq;

public class SceneRouterTests
{
    static string[] BuildSceneNames()
    {
        return EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
            .ToArray();
    }

    [Test]
    public void AllRouterScenes_ExistInBuildSettings()
    {
        string[] names = BuildSceneNames();
        Assert.Contains(SceneRouter.MainMenu, names);
        Assert.Contains(SceneRouter.Story, names);
        Assert.Contains(SceneRouter.LevelSelect, names);
        Assert.Contains(SceneRouter.Lore, names);
        for (int i = 1; i <= SceneRouter.LevelCount; i++)
            Assert.Contains(SceneRouter.LevelSceneName(i), names,
                $"Level {i} missing from Build Settings");
    }

    [Test]
    public void IntroScene_ConstantMatchesBuilderSceneName()
    {
        // The editor pass creates the intro scene literally named "Intro" —
        // SceneRouter.LoadIntro loads by this constant, so they must agree.
        Assert.AreEqual("Intro", SceneRouter.IntroScene);
    }
}
