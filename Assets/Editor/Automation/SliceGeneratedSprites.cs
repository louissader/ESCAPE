using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Applies sprite rects to every generated strip from its JSON manifest
    /// ({frameWidth, frameHeight, frames:[{name,index}]}). Idempotent: skips
    /// sheets whose rects already match. Run after (re)generating art.
    /// </summary>
    public static class SliceGeneratedSprites
    {
        [System.Serializable]
        class Manifest
        {
            public int frameWidth;
            public int frameHeight;
            public Frame[] frames;
        }

        [System.Serializable]
        class Frame
        {
            public string name;
            public int index;
        }

        [MenuItem("Escape/Slice Generated Sprites")]
        public static void Run()
        {
            int sliced = 0, skipped = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Generated" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string manifestPath = Path.ChangeExtension(path, ".json");
                if (!File.Exists(manifestPath))
                    continue;
                if (path.Contains("/Icons/") || path.Contains("/Backgrounds/"))
                    continue;

                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.frames == null || manifest.frames.Length == 0)
                    continue;

                if (Apply(path, manifest))
                    sliced++;
                else
                    skipped++;
            }
            Debug.Log($"[SliceGeneratedSprites] sliced {sliced}, already-correct {skipped}");
        }

        static bool Apply(string path, Manifest manifest)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                return false;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            SpriteRect[] existing = provider.GetSpriteRects();
            SpriteRect[] wanted = BuildRects(path, manifest);

            if (Matches(existing, wanted))
                return false;

            provider.SetSpriteRects(wanted);

            // 2022.3 name/fileId pairs must be registered for stable refs.
            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>();
                foreach (SpriteRect rect in wanted)
                    pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            provider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        static SpriteRect[] BuildRects(string path, Manifest manifest)
        {
            var rects = new SpriteRect[manifest.frames.Length];
            string baseName = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < manifest.frames.Length; i++)
            {
                Frame frame = manifest.frames[i];
                rects[i] = new SpriteRect
                {
                    name = $"{baseName}_{frame.name}",
                    rect = new Rect(frame.index * manifest.frameWidth, 0,
                                    manifest.frameWidth, manifest.frameHeight),
                    pivot = new Vector2(0.5f, 0.5f), // center: preserves existing scene placements
                    alignment = (int)SpriteAlignment.Center,
                    spriteID = DeterministicGuid(baseName, frame.name),
                };
            }
            return rects;
        }

        /// <summary>Stable per-frame GUIDs so re-slicing never breaks references.</summary>
        static GUID DeterministicGuid(string sheet, string frame)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sheet + "/" + frame));
            var sb = new System.Text.StringBuilder(32);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            GUID.TryParse(sb.ToString(), out GUID guid);
            return guid;
        }

        static bool Matches(SpriteRect[] a, SpriteRect[] b)
        {
            if (a == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].name != b[i].name || a[i].rect != b[i].rect)
                    return false;
            return true;
        }
    }
}
