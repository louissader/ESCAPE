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
        // SceneRouter.Story ("Story Details") is deliberately absent: the
        // scene was removed from Build Settings in build 3 (keyboard-only
        // copy, zero live callers — TitleMenu.OnStory replays the Intro
        // instead). LoadStory survives as dead code only because the legacy
        // MainMenu type is still referenced by TitleScreenBuilder's cleanup.
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

    [Test]
    public void IntroScene_ExistsInBuildSettings()
    {
        // TitleMenu.OnPlay / OnStory both route through SceneRouter.LoadIntro
        // — a build without the Intro scene would dead-end the PLAY button
        // on first launch.
        Assert.Contains(SceneRouter.IntroScene, BuildSceneNames());
    }
}
