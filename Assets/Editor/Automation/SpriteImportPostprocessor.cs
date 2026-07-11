using UnityEditor;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Enforces pixel-art import settings for generated art. Slicing is NOT
    /// done here (reimport-loop risk) — SliceGeneratedSprites handles rects.
    /// </summary>
    public class SpriteImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/Generated/"))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;

            bool isIcon = assetPath.Contains("/Icons/");
            bool isBackground = assetPath.Contains("/Backgrounds/");
            bool hasManifest = System.IO.File.Exists(
                System.IO.Path.ChangeExtension(assetPath, ".json"));

            importer.spritePixelsPerUnit = isIcon || isBackground ? 100 : 16;
            importer.spriteImportMode = hasManifest && !isIcon && !isBackground
                ? SpriteImportMode.Multiple
                : SpriteImportMode.Single;

            if (isBackground)
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
