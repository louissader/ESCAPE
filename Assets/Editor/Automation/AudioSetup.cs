using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Wires the audio stack: builds SfxLibrary.asset from Assets/Audio/SFX,
    /// replaces the Menu Scene's legacy "Audio Source" object with the new
    /// AudioManager (menu/grotto/depths music assigned). Idempotent.
    /// </summary>
    public static class AudioSetup
    {
        const string LibraryPath = "Assets/Audio/SfxLibrary.asset";
        const string MusicFolder = "Assets/GLYNMOOG/Melodic Themes vol.1";

        [MenuItem("Escape/Configure Audio")]
        public static void ConfigureAudio()
        {
            // 1. SfxLibrary from every wav in Assets/Audio/SFX.
            SfxLibrary library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var entries = new List<SfxLibrary.Entry>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/SFX" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                    entries.Add(new SfxLibrary.Entry
                    {
                        id = Path.GetFileNameWithoutExtension(path),
                        clip = clip,
                    });
            }
            library.entries = entries.ToArray();
            EditorUtility.SetDirty(library);
            Debug.Log($"[AudioSetup] SfxLibrary: {entries.Count} clips");

            // 2. Music clips.
            AudioClip menu = FindClip("03 Solitude Wonder");
            AudioClip grotto = FindClip("01 Mysterious Force Loop");
            AudioClip depths = FindClip("02 Desolation Loop");
            if (menu == null || grotto == null || depths == null)
                Debug.LogError("[AudioSetup] Missing music clip(s) — check GLYNMOOG folder");

            // 3. AudioManager in the Menu Scene (replaces legacy Audio Source object).
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu Scene.unity", OpenSceneMode.Single);

            GameObject legacy = GameObject.Find("Audio Source");
            if (legacy != null)
                Object.DestroyImmediate(legacy);

            AudioManager manager = Object.FindObjectOfType<AudioManager>();
            if (manager == null)
                manager = new GameObject("AudioManager").AddComponent<AudioManager>();
            manager.menuMusic = menu;
            manager.grottoMusic = grotto;
            manager.depthsMusic = depths;
            manager.sfxLibrary = library;
            EditorUtility.SetDirty(manager);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AudioSetup] Menu Scene AudioManager configured.");
        }

        static AudioClip FindClip(string namePrefix)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).StartsWith(namePrefix))
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return null;
        }
    }
}
