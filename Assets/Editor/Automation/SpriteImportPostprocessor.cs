using System.IO;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Enforces import settings for generated art. Slicing is NOT done here
    /// (reimport-loop risk) — SliceGeneratedSprites handles rects.
    ///
    /// Two regimes:
    ///  - Pixel art (everything else): Point filter, uncompressed, PPU 16
    ///    (100 for Icons/Backgrounds), Multiple when a manifest exists.
    ///  - Smooth plates (Backgrounds/cutscene_* + Backgrounds/title_bg):
    ///    native 960x540 anti-aliased illustrations stretched full-screen by
    ///    UI, so they import Bilinear, Clamp, uncrunched, maxTextureSize
    ///    kept >= 1024 so the 960px width never downscales. Scoped by exact
    ///    path + filename so no other generated sheet loses Point filtering.
    /// </summary>
    public class SpriteImportPostprocessor : AssetPostprocessor
    {
        const string GeneratedRoot = "Assets/Art/Generated/";
        const string BackgroundsRoot = "Assets/Art/Generated/Backgrounds/";

        /// <summary>Backgrounds/cutscene_*.png + Backgrounds/title_bg.png:
        /// the full-bleed UI plates drawn with anti-aliasing (not pixel art).</summary>
        static bool IsSmoothPlate(string path)
        {
            if (!path.StartsWith(BackgroundsRoot))
                return false;
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("cutscene_") || name == "title_bg";
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GeneratedRoot))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;

            bool isIcon = assetPath.Contains("/Icons/");
            bool isBackground = assetPath.Contains("/Backgrounds/");
            bool isPlate = IsSmoothPlate(assetPath);
            bool hasManifest = File.Exists(Path.ChangeExtension(assetPath, ".json"));

            importer.filterMode = isPlate ? FilterMode.Bilinear : FilterMode.Point;
            importer.spritePixelsPerUnit = isIcon || isBackground ? 100 : 16;
            importer.spriteImportMode = hasManifest && !isIcon && !isBackground
                ? SpriteImportMode.Multiple
                : SpriteImportMode.Single;

            if (isPlate)
            {
                // UI plates: never let a size cap resample the 960px width,
                // and clamp so Bilinear can't bleed the opposite edge in.
                if (importer.maxTextureSize < 1024)
                    importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Clamp;
                var plateSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(plateSettings);
                plateSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(plateSettings);
                Debug.Log($"[SpriteImportPostprocessor] smooth plate import: {assetPath} " +
                          "(Bilinear, Clamp, uncrunched, maxSize>=1024)");
            }
            else if (isBackground)
            {
                // Seamless SpriteDrawMode.Tiled needs Full Rect mesh + Repeat wrap.
                importer.wrapMode = TextureWrapMode.Repeat;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
        }
    }
}
