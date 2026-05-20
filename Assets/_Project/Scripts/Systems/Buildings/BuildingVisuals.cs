using System.Collections.Generic;
using UnityEngine;

namespace TransportManager.Systems.Buildings
{
    public static class BuildingVisuals
    {
        public const string Hangar = "Hangar";
        public const string Office = "Office";
        public const string FuelTank = "FuelTank";

        private static readonly Dictionary<string, string> _spriteNameAliases = new Dictionary<string, string>
        {
            { Hangar, "hub" },
        };

        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite GetSprite(string building, int level)
        {
            string key = $"{building}_{level}";
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var s = TryLoad(building, level);
            if (s == null)
            {
                // Fall back to highest available below requested level
                for (int lvl = level - 1; lvl >= 0; lvl--)
                {
                    s = TryLoad(building, lvl);
                    if (s != null) break;
                }
            }

            _cache[key] = s;
            return s;
        }

        private static readonly string[] PathTemplates =
        {
            "Buildings/{0}/level_{1}",
            "Buildings/{0}/lvl_{1}",
            "UI/{0}_lvl{1}",
            "UI/{0}_level_{1}",
        };

        private static Sprite TryLoad(string building, int level)
        {
            var names = new System.Collections.Generic.List<string> { building, building.ToLowerInvariant() };
            if (_spriteNameAliases.TryGetValue(building, out var alias)) names.Add(alias);

            foreach (var name in names)
            {
                foreach (var tpl in PathTemplates)
                {
                    string path = string.Format(tpl, name, level);
                    var s = Resources.Load<Sprite>(path);
                    if (s != null) return s;
                    var t = Resources.Load<Texture2D>(path);
                    if (t != null) return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                }
            }
            return null;
        }
    }
}
