using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectF.Player
{
    /// <summary>Space/time broad phase. Exact swept-disc checks are retained for nearby reservations.</summary>
    internal sealed class ReservationTable2D
    {
        private const float BucketSize = 2f;
        private readonly Vector2 origin;
        private readonly int width, height, cells, horizon;
        private readonly int[] heads;
        private readonly List<Link> links = new List<Link>(32768);
        private int[] seen = Array.Empty<int>();
        private int stamp;
        private IReadOnlyList<NavigationWorld2D.AgentPlan> plans;
        private struct Link { public int agent, next; }

        public ReservationTable2D(Rect bounds, int horizon)
        {
            origin = bounds.min - Vector2.one * BucketSize;
            width = Mathf.CeilToInt(bounds.width / BucketSize) + 3;
            height = Mathf.CeilToInt(bounds.height / BucketSize) + 3;
            cells = width * height; this.horizon = horizon;
            heads = new int[cells * (horizon + 1)];
        }

        public void Reset(IReadOnlyList<NavigationWorld2D.AgentPlan> source)
        {
            plans = source;
            Array.Fill(heads, -1); links.Clear();
            if (seen.Length < plans.Count) Array.Resize(ref seen, plans.Count);
            // Reserve every starting footprint during launch, including not-yet-planned units.
            for (int i = 0; i < plans.Count; i++) Insert(i, plans[i].start, plans[i].start, 1);
        }

        public void Reserve(int index)
        {
            var path = plans[index].path;
            for (int t = 1; t <= horizon; t++) Insert(index, path[t - 1], path[t], t);
        }

        private void Range(Vector2 a, Vector2 b, float radius, out int left, out int right, out int bottom, out int top)
        {
            left = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - origin.x) / BucketSize), 0, width - 1);
            right = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x, b.x) + radius - origin.x) / BucketSize), 0, width - 1);
            bottom = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y) - radius - origin.y) / BucketSize), 0, height - 1);
            top = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.y, b.y) + radius - origin.y) / BucketSize), 0, height - 1);
        }

        private void Insert(int agent, Vector2 a, Vector2 b, int time)
        {
            Range(a, b, plans[agent].radius, out int left, out int right, out int bottom, out int top);
            for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                {
                    int cell = time * cells + y * width + x;
                    links.Add(new Link { agent = agent, next = heads[cell] });
                    heads[cell] = links.Count - 1;
                }
        }

        public bool IsSafe(int self, Vector2 a, Vector2 b, int time)
        {
            if (++stamp == int.MaxValue) { Array.Clear(seen, 0, seen.Length); stamp = 1; }
            float radius = plans[self].radius;
            Range(a, b, radius + NavigationWorld2D.Clearance, out int left, out int right, out int bottom, out int top);
            for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                    for (int link = heads[time * cells + y * width + x]; link >= 0; link = links[link].next)
                    {
                        int i = links[link].agent;
                        if (i == self || seen[i] == stamp) continue;
                        seen[i] = stamp;
                        var other = plans[i];
                        Vector2 c = other.planned ? other.path[time - 1] : other.start;
                        Vector2 d = other.planned ? other.path[time] : other.start;
                        float separation = radius + other.radius + NavigationWorld2D.Clearance;
                        float required = Mathf.Min(separation * separation, (a - c).sqrMagnitude - .00001f);
                        if (CooperativePathfinder.SweptDistanceSquared(a, b, c, d) < required) return false;
                    }
            return true;
        }
    }
}
