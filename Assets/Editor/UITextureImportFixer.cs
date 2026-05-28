using UnityEditor;
using UnityEngine;

namespace TransportManager.Editor
{
    public static class UITextureImportFixer
    {
        [MenuItem("Tools/Transport Manager/Fix UI Sprite Imports")]
        public static void FixAll()
        {
            string[] folders =
            {
                "Assets/Resources/UI/Icons/icons",
                "Assets/Resources/UI/Icons/Infos",
                "Assets/Resources/UI/Logo",
                "Assets/Resources/UI/Tutorial",
                "Assets/Resources/UI/WareHouse",
                "Assets/Resources/UI/offers",
            };

            int count = 0;
            foreach (var folder in folders)
            {
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }

                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        changed = true;
                    }

                    if (importer.filterMode != FilterMode.Bilinear)
                    {
                        importer.filterMode = FilterMode.Bilinear;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        count++;
                        Debug.Log($"[UITextureImportFixer] Fixed: {path}");
                    }
                }
            }

            Debug.Log($"[UITextureImportFixer] Done — {count} texture(s) reimported as Sprite.");
            EditorUtility.DisplayDialog("Fix UI Sprite Imports", $"{count} texture(s) reimportées en Sprite.", "OK");
        }
    }
}
