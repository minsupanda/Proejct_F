using System.Collections.Generic;
using UnityEngine;

namespace ProjectF.Player
{
    /// <summary>A* over (position, time), including waits and swept-disc reservations.</summary>
    internal sealed class CooperativePathfinder
    {
        private NavigationGrid2D grid;
        private readonly int horizon, stride, budget;
        private readonly float step;
        private readonly int[] visited, parents;
        private readonly List<HeapEntry> heap = new List<HeapEntry>(4096);
        private readonly List<int> reverse = new List<int>(128);
        private int stamp;
        private Vector2 start, goal;
        private float speed;
        private int self;
        private IReadOnlyList<NavigationWorld2D.AgentPlan> plans;
        private ReservationTable2D table;
        public int Expanded { get; private set; }
        private struct HeapEntry { public int state; public float score; }

        public CooperativePathfinder(NavigationGrid2D grid, int horizon, float step, int budget)
        {
            this.grid = grid; this.horizon = horizon; this.step = step; this.budget = budget;
            stride = grid.Count + 2;
            visited = new int[stride * (horizon + 1)]; parents = new int[visited.Length];
        }
        private Vector2 Point(int node) => node == grid.Count ? start : node == grid.Count + 1 ? goal : grid.Point(node);
        private float Estimate(Vector2 point) => Vector2.Distance(point, goal) / (speed * step);

        public bool Find(int index, IReadOnlyList<NavigationWorld2D.AgentPlan> reservations, ReservationTable2D table, Vector2 target, Vector2[] output, int expansionBudget)
        {
            this.table = table;
            self = index; plans = reservations; start = plans[index].start; goal = target;
            grid = plans[index].grid;
            speed = plans[index].speed;
            if (speed <= .001f) return false;
            if (++stamp == int.MaxValue) { System.Array.Clear(visited, 0, visited.Length); stamp = 1; }
            heap.Clear(); reverse.Clear(); Expanded = 0;
            int initial = grid.Count;
            visited[initial] = stamp; parents[initial] = -1;
            Push(initial, Estimate(start));
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            int limit = Mathf.Min(budget, expansionBudget);
            while (heap.Count > 0 && Expanded++ < limit)
            {
                int state = Pop();
                int time = state / stride, node = state % stride;
                Vector2 point = Point(node);
                float remaining = (point - goal).sqrMagnitude;
                if (remaining < bestDistance - .0001f && SafeHold(point, time))
                {
                    best = state; bestDistance = remaining;
                    if (remaining < .0001f) break;
                }
                if (time >= horizon) continue;
                TryEdge(state, node, 1, false); // Waiting is a real action, never a physics collision.
                if (node == grid.Count || remaining <= grid.CellSize * grid.CellSize * 2.1f)
                    TryEdge(state, grid.Count + 1, 0, true);
                if (node == grid.Count)
                {
                    int nearest = grid.Nearest(point);
                    TryEdge(state, nearest, 0, true);
                    for (int d = 0; d < 8; d++)
                    {
                        int next = grid.Neighbour(nearest, d);
                        if (next >= 0) TryEdge(state, next, 0, true);
                    }
                }
                else if (node < grid.Count)
                    for (int d = 0; d < 8; d++)
                    {
                        int next = grid.Neighbour(node, d);
                        if (next >= 0) TryEdge(state, next, 0, false);
                    }
            }
            if (best < 0) return false;
            for (int s = best; s >= 0; s = parents[s]) reverse.Add(s);
            output[0] = start;
            for (int i = reverse.Count - 1; i > 0; i--)
            {
                int a = reverse[i], b = reverse[i - 1];
                int ta = a / stride, tb = b / stride;
                Vector2 pa = Point(a % stride), pb = Point(b % stride);
                for (int t = ta + 1; t <= tb; t++) output[t] = Vector2.Lerp(pa, pb, (float)(t - ta) / (tb - ta));
            }
            int finish = best / stride;
            for (int t = finish + 1; t <= horizon; t++) output[t] = output[finish];
            return true;
        }

        private void TryEdge(int state, int node, int duration, bool checkTerrain)
        {
            if (node < 0 || (node < grid.Count && !grid.IsFree(node))) return;
            Vector2 a = Point(state % stride), b = Point(node);
            int time = state / stride;
            if (duration == 0) duration = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (speed * step) - .00001f));
            int end = time + duration;
            if (end > horizon) return;
            int next = end * stride + node;
            if (visited[next] == stamp) return;
            if (checkTerrain && !grid.CanTravel(a, b)) return;
            for (int t = 1; t <= duration; t++)
                if (!SafeSegment(Vector2.Lerp(a, b, (float)(t - 1) / duration), Vector2.Lerp(a, b, (float)t / duration), time + t)) return;
            visited[next] = stamp; parents[next] = state;
            Push(next, end + Estimate(b) * 1.0001f);
        }

        private bool SafeHold(Vector2 point, int time)
        {
            for (int t = time + 1; t <= horizon; t++) if (!SafeSegment(point, point, t)) return false;
            return true;
        }

        private bool SafeSegment(Vector2 a, Vector2 b, int endTime)
        {
            return table.IsSafe(self, a, b, endTime);
        }

        internal static float SweptDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 relative = a - c, velocity = (b - a) - (d - c);
            float t = velocity.sqrMagnitude < .0000001f ? 0 : Mathf.Clamp01(-Vector2.Dot(relative, velocity) / velocity.sqrMagnitude);
            return (relative + velocity * t).sqrMagnitude;
        }

        private void Push(int state, float score)
        {
            var entry = new HeapEntry { state = state, score = score };
            int at = heap.Count; heap.Add(entry);
            while (at > 0)
            {
                int parent = (at - 1) / 2;
                if (heap[parent].score <= score) break;
                heap[at] = heap[parent]; at = parent;
            }
            heap[at] = entry;
        }
        private int Pop()
        {
            int result = heap[0].state;
            var last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return result;
            int at = 0;
            while (at * 2 + 1 < heap.Count)
            {
                int child = at * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].score < heap[child].score) child++;
                if (last.score <= heap[child].score) break;
                heap[at] = heap[child]; at = child;
            }
            heap[at] = last;
            return result;
        }
    }
}
