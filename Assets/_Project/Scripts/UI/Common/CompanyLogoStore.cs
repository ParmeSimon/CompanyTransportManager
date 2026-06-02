using System;
using System.IO;
using UnityEngine;

namespace TransportManager.UI.Common
{
    /// <summary>
    /// Stocke/charge le logo photo personnalisé de l'entreprise.
    /// L'image est ré-échantillonnée puis écrite en PNG dans persistentDataPath.
    /// </summary>
    public static class CompanyLogoStore
    {
        private const int MaxSize = 256;          // côté max stocké (logo compact)
        private const string FileName = "company_logo.png";

        private static Sprite _cached;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasCustomLogo => File.Exists(FilePath);

        /// Enregistre une texture importée comme logo (carré, max 256 px).
        public static bool Save(Texture2D source)
        {
            if (source == null) return false;
            try
            {
                var square = MakeSquare(source, MaxSize);
                File.WriteAllBytes(FilePath, square.EncodeToPNG());
                _cached = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CompanyLogoStore] Échec de l'enregistrement : {e.Message}");
                return false;
            }
        }

        /// Charge (et met en cache) le sprite du logo personnalisé, ou null s'il n'existe pas.
        public static Sprite LoadSprite()
        {
            if (_cached != null) return _cached;
            if (!File.Exists(FilePath)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                if (!tex.LoadImage(File.ReadAllBytes(FilePath))) return null;
                tex.Apply();
                _cached = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                return _cached;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CompanyLogoStore] Échec du chargement : {e.Message}");
                return null;
            }
        }

        public static void Clear()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            _cached = null;
        }

        // Recadre au centre en carré puis redimensionne à `size` via une RenderTexture (rapide et propre).
        private static Texture2D MakeSquare(Texture2D src, int size)
        {
            int side = Mathf.Min(src.width, src.height);
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            // Blit avec recadrage centré : scale = side/dim, offset = (1-scale)/2
            var scale  = new Vector2(side / (float)src.width, side / (float)src.height);
            var offset = new Vector2((1f - scale.x) * 0.5f, (1f - scale.y) * 0.5f);
            Graphics.Blit(src, rt, scale, offset);
            RenderTexture.active = rt;
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            result.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }
    }
}
