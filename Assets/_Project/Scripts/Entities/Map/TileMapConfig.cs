using UnityEngine;

namespace TransportManager.Entities.Map
{
    [CreateAssetMenu(fileName = "TileMapConfig", menuName = "TransportManager/Tile Map Config")]
    public class TileMapConfig : ScriptableObject
    {
        [Tooltip("URL template. Placeholders: {z}, {x}, {y}.")]
        public string urlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

        [Tooltip("Required by OSM tile usage policy. Identify your app.")]
        public string userAgent = "TransportManagerMobile/0.1 (contact@example.com)";

        [Tooltip("Displayed in-game as required by OSM ToS.")]
        public string attribution = "© OpenStreetMap contributors";

        public int minZoom = 1;
        public int maxZoom = 18;
        public int tilePixelSize = 256;

        [Tooltip("Max tiles kept in memory before LRU eviction.")]
        public int memoryCacheCapacity = 200;

        [Tooltip("Folder under persistentDataPath where tiles are cached on disk.")]
        public string diskCacheFolder = "osm_tiles";

        [Tooltip("Timeout per tile HTTP request in seconds.")]
        public int requestTimeoutSeconds = 10;
    }
}
