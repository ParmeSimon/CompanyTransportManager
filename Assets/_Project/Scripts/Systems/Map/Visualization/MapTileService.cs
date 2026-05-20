using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using TransportManager.Entities.Map;

namespace TransportManager.Systems.Map.Visualization
{
    public class MapTileService
    {
        private readonly TileMapConfig _config;
        private readonly Dictionary<TileKey, Texture2D> _memoryCache = new Dictionary<TileKey, Texture2D>();
        private readonly LinkedList<TileKey> _lruOrder = new LinkedList<TileKey>();
        private readonly Dictionary<TileKey, Task<Texture2D>> _inFlight = new Dictionary<TileKey, Task<Texture2D>>();

        public MapTileService(TileMapConfig config)
        {
            _config = config;
        }

        public Task<Texture2D> GetTileAsync(TileKey key)
        {
            if (_memoryCache.TryGetValue(key, out var cached))
            {
                Touch(key);
                return Task.FromResult(cached);
            }
            if (_inFlight.TryGetValue(key, out var pending)) return pending;

            var task = FetchAsync(key);
            _inFlight[key] = task;
            return task;
        }

        private async Task<Texture2D> FetchAsync(TileKey key)
        {
            try
            {
                string diskPath = DiskPathFor(key);
                if (File.Exists(diskPath))
                {
                    var bytes = await Task.Run(() => File.ReadAllBytes(diskPath));
                    var tex = BuildTexture(bytes);
                    if (tex != null) StoreMemory(key, tex);
                    return tex;
                }

                string url = BuildUrl(key);
                using var req = UnityWebRequestTexture.GetTexture(url);
                if (!string.IsNullOrEmpty(_config.userAgent))
                    req.SetRequestHeader("User-Agent", _config.userAgent);
                req.timeout = _config.requestTimeoutSeconds;

                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[MapTileService] {key} fetch failed: {req.error} ({req.responseCode})");
                    return null;
                }

                var data = req.downloadHandler.data;
                _ = SaveToDiskAsync(diskPath, data);

                var texture = ((DownloadHandlerTexture)req.downloadHandler).texture;
                if (texture != null)
                {
                    texture.wrapMode = TextureWrapMode.Clamp;
                    StoreMemory(key, texture);
                }
                return texture;
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        private static Task SaveToDiskAsync(string path, byte[] bytes)
        {
            return Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MapTileService] disk write failed: {e.Message}");
                }
            });
        }

        private static Texture2D BuildTexture(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(bytes) ? tex : null;
        }

        private string BuildUrl(TileKey k)
        {
            return _config.urlTemplate
                .Replace("{z}", k.zoom.ToString())
                .Replace("{x}", k.x.ToString())
                .Replace("{y}", k.y.ToString());
        }

        private string DiskPathFor(TileKey k)
        {
            return Path.Combine(
                Application.persistentDataPath,
                _config.diskCacheFolder,
                k.zoom.ToString(),
                k.x.ToString(),
                $"{k.y}.png");
        }

        private void StoreMemory(TileKey k, Texture2D tex)
        {
            if (_memoryCache.ContainsKey(k))
            {
                _memoryCache[k] = tex;
                Touch(k);
                return;
            }
            _memoryCache[k] = tex;
            _lruOrder.AddLast(k);
            EvictIfNeeded();
        }

        private void Touch(TileKey k)
        {
            _lruOrder.Remove(k);
            _lruOrder.AddLast(k);
        }

        private void EvictIfNeeded()
        {
            int cap = Mathf.Max(16, _config.memoryCacheCapacity);
            while (_memoryCache.Count > cap && _lruOrder.First != null)
            {
                var oldest = _lruOrder.First.Value;
                _lruOrder.RemoveFirst();
                if (_memoryCache.TryGetValue(oldest, out var tex))
                {
                    _memoryCache.Remove(oldest);
                    if (tex != null) UnityEngine.Object.Destroy(tex);
                }
            }
        }
    }
}
