using System.Collections.Generic;
using UnityEngine;

namespace ProjectF.Player
{
    public enum UnitTravelState { Idle, Moving, Waiting, Arrived, NoPath }

    /// <summary>Scene-owned planner: all movement is committed together after reservation checks.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class NavigationWorld2D : MonoBehaviour
    {
        internal const float Clearance = .10f;
        private const float TimeStep = .1f;
        private const int Horizon = 60;
        [SerializeField] private Rect movementBounds = new Rect(-23, -15, 46, 30);
        [SerializeField, Min(.5f)] private float cellSize = 1f;
        [SerializeField, Range(.2f, 2f)] private float replanInterval = .5f;
        [SerializeField, Min(100)] private int searchBudget = 6000;
        [SerializeField, Min(1000)] private int groupSearchBudget = 32000;
        private readonly List<AgentPlan> agents = new List<AgentPlan>();
        private readonly Dictionary<int, NavigationGrid2D> grids = new Dictionary<int, NavigationGrid2D>();
        private readonly Dictionary<Vector2, int[]> goalCandidates = new Dictionary<Vector2, int[]>();
        private CooperativePathfinder finder;
        private ReservationTable2D reservations;
        private long nextOrder;
        private float elapsed, terrainElapsed;
        private bool dirty = true, terrainDirty = true, membershipDirty = true;
        private readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        public double LastPlanMilliseconds { get; private set; }
        public double PeakPlanMilliseconds { get; private set; }
        public double TotalPlanMilliseconds { get; private set; }
        public double PeakTerrainMilliseconds { get; private set; }
        public int LastExpandedNodes { get; private set; }
        public int ReplanCount { get; private set; }
        public int SafetyStops { get; private set; }
        public Rect MovementBounds => movementBounds;

        internal sealed class AgentPlan
        {
            public CommandableUnit unit;
            public readonly Vector2[] path = new Vector2[Horizon + 1];
            public bool planned, pinned, blocked;
            public Vector2 next, current, start;
            public float radius, speed;
            public NavigationGrid2D grid;
        }

        internal void Register(CommandableUnit unit)
        {
            foreach (var agent in agents) if (agent.unit == unit) return;
            agents.Add(new AgentPlan { unit = unit }); dirty = membershipDirty = true;
        }
        internal void Unregister(CommandableUnit unit)
        {
            for (int i = agents.Count - 1; i >= 0; i--) if (agents[i].unit == unit) agents.RemoveAt(i);
            dirty = membershipDirty = true;
        }
        internal long NewOrder() { dirty = true; return ++nextOrder; }
        public void MarkObstaclesDirty() { terrainDirty = true; dirty = true; }
        public void CopyUnitsTo(List<CommandableUnit> result)
        {
            result.Clear();
            foreach (var agent in agents) result.Add(agent.unit);
        }

        private void FixedUpdate()
        {
            if (agents.Count == 0) return;
            elapsed += Time.fixedDeltaTime; terrainElapsed += Time.fixedDeltaTime;
            bool moving = false;
            foreach (var agent in agents) if (agent.unit.HasDestination) { moving = true; break; }
            if (!moving)
            {
                foreach (var agent in agents) agent.unit.ApplyNavigationVelocity(Vector2.zero);
                return;
            }
            if (terrainElapsed >= 1f) { terrainDirty = true; dirty = true; }
            if (finder == null || membershipDirty || (dirty && elapsed >= TimeStep) || elapsed >= replanInterval) Replan();
            float nextTime = elapsed + Time.fixedDeltaTime;
            int sample = Mathf.Clamp(Mathf.FloorToInt(nextTime / TimeStep), 0, Horizon - 1);
            float fraction = Mathf.Clamp01(nextTime / TimeStep - sample);
            foreach (var agent in agents)
            {
                agent.current = agent.unit.Position;
                agent.blocked = false;
                agent.next = agent.unit.HasDestination ? Vector2.Lerp(agent.path[sample], agent.path[sample + 1], fraction) : agent.current;
                agent.next = agent.current + Vector2.ClampMagnitude(agent.next - agent.current, agent.unit.MoveSpeed * Time.fixedDeltaTime);
                if (!agent.grid.CanTravel(agent.current, agent.next))
                {
                    agent.next = agent.current; terrainDirty = dirty = true;
                }
            }
            // Validate actual next-step motion as well as planned trajectories. External physics
            // or new terrain must never cause agents to blindly push into one another.
            bool unsafeStep = false;
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < agents.Count; i++)
                    for (int j = i + 1; j < agents.Count; j++)
                    {
                        if (agents[i].blocked && agents[j].blocked) continue;
                        float separation = agents[i].radius + agents[j].radius + Clearance * .3f;
                        // Reject distant pairs before the continuous collision calculation.
                        if (Mathf.Abs(agents[i].current.x - agents[j].current.x) > separation + (agents[i].next - agents[i].current).magnitude + (agents[j].next - agents[j].current).magnitude) continue;
                        float required = Mathf.Min(separation * separation, (agents[i].current - agents[j].current).sqrMagnitude - .00001f);
                        if (CooperativePathfinder.SweptDistanceSquared(agents[i].current, agents[i].next, agents[j].current, agents[j].next) >= required) continue;
                        agents[i].blocked = agents[j].blocked = true;
                        agents[i].next = agents[i].current; agents[j].next = agents[j].current;
                        changed = unsafeStep = true;
                    }
            } while (changed);
            if (unsafeStep) { dirty = true; SafetyStops++; }
            foreach (var agent in agents)
            {
                var unit = agent.unit;
                Vector2 velocity = agent.blocked ? Vector2.zero : Vector2.ClampMagnitude((agent.next - agent.current) / Time.fixedDeltaTime, unit.MoveSpeed);
                unit.ApplyNavigationVelocity(velocity);
                if (!unit.HasDestination) continue;
                if (Vector2.Distance(agent.current, unit.Destination) < .045f) unit.Arrive();
                else unit.SetTravelState(velocity.sqrMagnitude > .001f ? UnitTravelState.Moving : UnitTravelState.Waiting);
                if (Vector2.Distance(agent.current, Sample(agent.path, elapsed)) > .2f) dirty = true;
            }
        }

        private static Vector2 Sample(Vector2[] path, float time)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(time / TimeStep), 0, Horizon - 1);
            return Vector2.Lerp(path[index], path[index + 1], Mathf.Clamp01(time / TimeStep - index));
        }

        private void Replan()
        {
            watch.Restart(); LastExpandedNodes = 0;
            foreach (var agent in agents)
            {
                agent.start = agent.unit.Position;
                agent.radius = agent.unit.Radius;
                agent.speed = agent.unit.MoveSpeed;
                // Share terrain by conservative size class, not by the largest unit in the army.
                int sizeClass = Mathf.CeilToInt((agent.radius + Clearance * .5f) * 20);
                if (!grids.TryGetValue(sizeClass, out var terrain))
                {
                    terrain = new NavigationGrid2D(movementBounds, cellSize, sizeClass / 20f);
                    grids.Add(sizeClass, terrain); terrainDirty = true;
                    if (finder == null) finder = new CooperativePathfinder(terrain, Horizon, TimeStep, searchBudget);
                }
                agent.grid = terrain;
            }
            if (terrainDirty)
            {
                double before = watch.Elapsed.TotalMilliseconds;
                foreach (var terrain in grids.Values) terrain.Refresh();
                terrainDirty = false; terrainElapsed = 0;
                PeakTerrainMilliseconds = System.Math.Max(PeakTerrainMilliseconds, watch.Elapsed.TotalMilliseconds - before);
            }
            agents.Sort(ComparePriority);
            if (reservations == null) reservations = new ReservationTable2D(movementBounds, Horizon);
            foreach (var agent in agents) agent.pinned = false;
            bool complete = false;
            // Arrived soldiers still occupy space. Keep their cost in the crowd budget rather
            // than letting the final few arrivals perform large searches through a dense group.
            int unitBudget = Mathf.Min(searchBudget, Mathf.Max(256, groupSearchBudget / Mathf.Max(1, agents.Count)));
            // A failed lower-priority escape is retried as a stationary obstacle. Higher
            // priority is a preference, never permission to occupy an unsafe trajectory.
            for (int attempt = 0; attempt < 3 && !complete; attempt++)
            {
                foreach (var agent in agents)
                {
                    agent.planned = !agent.unit.HasDestination || agent.unit.MoveSpeed <= .001f || agent.pinned;
                    if (agent.planned) Hold(agent);
                }
                reservations.Reset(agents);
                for (int i = 0; i < agents.Count; i++) if (agents[i].planned) reservations.Reserve(i);
                complete = true;
                for (int i = 0; i < agents.Count; i++)
                {
                    var agent = agents[i];
                    if (agent.planned) continue;
                    if (!ResolveDestination(i, out var destination))
                    {
                        agent.unit.RejectDestination(); agent.planned = true; Hold(agent);
                        complete = false; break;
                    }
                    agent.unit.SetResolvedDestination(destination);
                    if (!finder.Find(i, agents, reservations, destination, agent.path, unitBudget))
                    {
                        agent.pinned = true; complete = false; break;
                    }
                    LastExpandedNodes += finder.Expanded;
                    agent.planned = true;
                    reservations.Reserve(i);
                }
            }
            if (!complete) foreach (var agent in agents) Hold(agent);
            elapsed = 0; dirty = membershipDirty = false; ReplanCount++;
            watch.Stop(); LastPlanMilliseconds = watch.Elapsed.TotalMilliseconds;
            PeakPlanMilliseconds = System.Math.Max(PeakPlanMilliseconds, LastPlanMilliseconds);
            TotalPlanMilliseconds += LastPlanMilliseconds;
        }

        private static int ComparePriority(AgentPlan a, AgentPlan b) => a.unit.CommandOrder.CompareTo(b.unit.CommandOrder);
        private static void Hold(AgentPlan agent)
        {
            for (int t = 0; t <= Horizon; t++) agent.path[t] = agent.start;
        }

        private bool ResolveDestination(int index, out Vector2 result)
        {
            var grid = agents[index].grid;
            Vector2 requested = grid.Clamp(agents[index].unit.RequestedDestination);
            result = requested;
            if (grid.IsClear(requested) && GoalAvailable(index, requested)) return true;
            if (!goalCandidates.TryGetValue(requested, out var candidates))
            {
                // Cache one distance-ordered candidate list for the entire group. Search may
                // grow beyond four units of radius, so a large army does not silently lose orders.
                if (goalCandidates.Count >= 16) goalCandidates.Clear();
                candidates = new int[grid.Count];
                for (int n = 0; n < candidates.Length; n++) candidates[n] = n;
                System.Array.Sort(candidates, (a, b) =>
                {
                    int compared = (grid.Point(a) - requested).sqrMagnitude.CompareTo((grid.Point(b) - requested).sqrMagnitude);
                    return compared != 0 ? compared : a.CompareTo(b);
                });
                goalCandidates.Add(requested, candidates);
            }
            foreach (int node in candidates)
            {
                if (!grid.IsFree(node)) continue;
                Vector2 point = grid.Point(node);
                if (!GoalAvailable(index, point)) continue;
                result = point; return true;
            }
            return false;
        }

        private bool GoalAvailable(int index, Vector2 point)
        {
            for (int i = 0; i < agents.Count; i++)
            {
                if (i == index) continue;
                var other = agents[i].unit;
                if (i > index && other.HasDestination) continue;
                Vector2 occupied = other.HasDestination ? other.Destination : agents[i].start;
                float separation = agents[index].radius + agents[i].radius + Clearance;
                if ((point - occupied).sqrMagnitude < separation * separation) return false;
            }
            return true;
        }

        private void OnDisable()
        {
            foreach (var agent in agents) if (agent.unit != null) agent.unit.ApplyNavigationVelocity(Vector2.zero);
            dirty = true;
        }
    }
}
