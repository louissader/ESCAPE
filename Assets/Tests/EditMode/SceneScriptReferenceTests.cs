using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Playtest 9.5 crash guard: a component whose class shares another class's
/// .cs file has no MonoScript asset, so the scene saves a script reference
/// with no guid. The editor tolerates it; the iOS player crashes loading the
/// scene (CachedReader::OutOfBoundsError). Every shipped scene must reference
/// its scripts by guid.
/// </summary>
public class SceneScriptReferenceTests
{
    static readonly Regex GuidlessScript =
        new Regex(@"^\s*m_Script: \{fileID: -?\d+\}\s*$", RegexOptions.Multiline);

    [Test]
    public void BuildScenes_HaveNoGuidlessScriptReferences()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
                continue;
            string text = File.ReadAllText(scene.path);
            Match m = GuidlessScript.Match(text);
            Assert.IsFalse(m.Success,
                $"{scene.path} has a guid-less script reference ('{m.Value.Trim()}'): " +
                "a MonoBehaviour is declared in a file not named after it");
        }
    }
}
