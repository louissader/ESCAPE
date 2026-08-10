using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Locks the 6-beat narrated intro contract against builder churn: the
/// baked Intro scene must carry a plate, caption, voice clip, and non-empty
/// overlay root for every beat, plus the scene-local narration source.
/// Runs against the artifact on disk — i.e., after a rebake, exactly like
/// NavigationModeTests.
/// </summary>
public class IntroCutsceneTests
{
    const int Beats = 6;

    [Test]
    public void IntroScene_CarriesTheFull6BeatNarratedContract()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Intro.unity");
        IntroCutscene cutscene = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            cutscene = root.GetComponentInChildren<IntroCutscene>(true);
            if (cutscene != null)
                break;
        }
        Assert.IsNotNull(cutscene, "Intro scene has no IntroCutscene");

        Assert.AreEqual(Beats, cutscene.plates.Length, "plate count");
        for (int i = 0; i < Beats; i++)
            Assert.IsNotNull(cutscene.plates[i], $"plate {i} missing");

        Assert.AreEqual(Beats, cutscene.captions.Length, "caption count");
        for (int i = 0; i < Beats; i++)
            Assert.IsFalse(string.IsNullOrEmpty(cutscene.captions[i]), $"caption {i} empty");

        Assert.IsNotNull(cutscene.voiceSource, "voiceSource not wired");
        Assert.IsFalse(cutscene.voiceSource.playOnAwake, "voiceSource must not playOnAwake");
        Assert.AreEqual(Beats, cutscene.beatVoiceOvers.Length, "voice-over count");
        for (int i = 0; i < Beats; i++)
            Assert.IsNotNull(cutscene.beatVoiceOvers[i],
                $"vo_beat{i} missing — run tools/vogen/generate_vo.sh and rebuild the intro");

        Assert.AreEqual(Beats, cutscene.beatOverlayRoots.Length, "overlay root count");
        for (int i = 0; i < Beats; i++)
        {
            Assert.IsNotNull(cutscene.beatOverlayRoots[i], $"overlay root {i} missing");
            Assert.Greater(cutscene.beatOverlayRoots[i].childCount, 0,
                $"beat {i} overlay root is empty — every beat carries motion now");
        }
    }
}
