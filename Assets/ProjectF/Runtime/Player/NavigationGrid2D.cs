using UnityEngine;

namespace ProjectF.Player
{
    /// <summary>Shared static geometry cache. Unit colliders and triggers are not terrain.</summary>
    internal sealed class NavigationGrid2D
    {
        private readonly Rect bounds;
        private readonly float cellSize, radius;
        private readonly Collider2D[] overlaps = new Collider2D[64];
        private readonly RaycastHit2D[] hits = new RaycastHit2D[64];
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private readonly bool[] free;
        private readonly byte[] checkedEdges, openEdges;
        public int Width { get; }
        public int Height { get; }
        public int Count => Width * Height;
        public float CellSize => cellSize;

        public NavigationGrid2D(Rect bounds, float cellSize, float radius)
        {
            this.bounds = bounds; this.cellSize = cellSize; this.radius = radius;
            Width = Mathf.FloorToInt(bounds.width / cellSize) + 1;
            Height = Mathf.FloorToInt(bounds.height / cellSize) + 1;
            free = new bool[Count]; checkedEdges = new byte[Count]; openEdges = new byte[Count];
        }

        public Vector2 Point(int node) => bounds.min + new Vector2(node % Width, node / Width) * cellSize;
        public int Nearest(Vector2 point)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((point.x - bounds.xMin) / cellSize), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((point.y - bounds.yMin) / cellSize), 0, Height - 1);
            return y * Width + x;
        }
        public bool IsFree(int node) => node >= 0 && node < Count && free[node];
        public Vector2 Clamp(Vector2 point) => new Vector2(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax), Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));
        private static bool Terrain(Collider2D collider) => collider != null && collider.GetComponentInParent<CommandableUnit>() == null;

        public bool IsClear(Vector2 point)
        {
            if (point != Clamp(point)) return false;
            int count = Physics2D.OverlapCircle(point, radius, filter, overlaps);
            if (count == overlaps.Length) return false; // Saturation must never turn into a false clear result.
            for (int i = 0; i < count; i++) if (Terrain(overlaps[i])) return false;
            return true;
        }

        public bool CanTravel(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return IsClear(to);
            int count = Physics2D.CircleCast(from, radius, delta.normalized, filter, hits, delta.magnitude);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (Terrain(hits[i].collider)) return false;
            return true;
        }

        public void Refresh()
        {
            Physics2D.SyncTransforms();
            for (int i = 0; i < Count; i++) { free[i] = IsClear(Point(i)); checkedEdges[i] = openEdges[i] = 0; }
        }

        public int Neighbour(int node, int direction)
        {
            int dx = direction == 0 || direction == 4 || direction == 6 ? -1 : direction == 1 || direction == 5 || direction == 7 ? 1 : 0;
            int dy = direction == 2 || direction == 4 || direction == 5 ? -1 : direction == 3 || direction == 6 || direction == 7 ? 1 : 0;
            int x = node % Width + dx, y = node / Width + dy;
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            int next = y * Width + x;
            if (!IsFree(next)) return -1;
            byte bit = (byte)(1 << direction);
            if ((checkedEdges[node] & bit) == 0)
            {
                checkedEdges[node] |= bit;
                if (CanTravel(Point(node), Point(next))) openEdges[node] |= bit;
            }
            return (openEdges[node] & bit) != 0 ? next : -1;
        }
    }
}
