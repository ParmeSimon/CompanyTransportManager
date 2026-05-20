using System;

namespace TransportManager.Systems.Map.Visualization
{
    public readonly struct TileKey : IEquatable<TileKey>
    {
        public readonly int zoom;
        public readonly int x;
        public readonly int y;

        public TileKey(int zoom, int x, int y)
        {
            this.zoom = zoom;
            this.x = x;
            this.y = y;
        }

        public bool Equals(TileKey other) => zoom == other.zoom && x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is TileKey o && Equals(o);
        public override int GetHashCode() => (zoom * 397 ^ x) * 397 ^ y;
        public override string ToString() => $"{zoom}/{x}/{y}";
    }
}
