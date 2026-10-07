using System;
using ProjectF.Combat;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Economy
{
    public enum GatheringState { Idle, Moving, Gathering, Blocked, StorageFull }

    /// <summary>One gathering order per ally; uses the existing planner and cancels on other orders.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CommandableUnit), typeof(UnitCombat))]
    public sealed class WoodGatherer : MonoBehaviour
    {
        [SerializeField] private WoodGatheringSettings settings;
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private PortalInvasion invasion;
        private CommandableUnit unit;
        private UnitCombat combat;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private float workSeconds, nextApproach, lastProgress;
        private Vector2 progressPosition;
        public event Action Changed;
        public WoodResourceNode Target { get; private set; }
        public GatheringState State { get; private set; }
        public bool CanGather => isActiveAndEnabled && settings != null && stockpile != null && stockpile.isActiveAndEnabled
            && invasion != null && invasion.isActiveAndEnabled && invasion.State == InvasionState.Ready && combat.IsPlayerControlled;

        public void InitializeEstate(EstateStockpile estate, PortalInvasion director)
        {
            if (gameObject.activeInHierarchy) throw new System.InvalidOperationException("Bind estate before activating a spawned gatherer.");
            stockpile = estate != null ? estate : throw new System.ArgumentNullException(nameof(estate));
            invasion = director != null ? director : throw new System.ArgumentNullException(nameof(director));
        }
        private void Awake() { unit = GetComponent<CommandableUnit>(); combat = GetComponent<UnitCombat>(); }
        private void OnEnable()
        {
            unit.MoveCommandIssued += Cancel;
            combat.AttackCommandIssued += Cancel;
            combat.Died += Cancel;
            if (invasion != null) invasion.Changed += OnInvasionChanged;
        }
        private void OnDisable()
        {
            unit.MoveCommandIssued -= Cancel;
            combat.AttackCommandIssued -= Cancel;
            combat.Died -= Cancel;
            if (invasion != null) invasion.Changed -= OnInvasionChanged;
            Cancel();
        }
        private void OnInvasionChanged() { if (!CanGather) Cancel(); }

        public bool Gather(WoodResourceNode node)
        {
            if (!CanGather || Time.timeScale <= 0 || node == null || !node.IsAvailable) return false;
            if (Target == node) return true;
            Cancel();
            combat.CancelAttack(); unit.Stop();
            Target = node;
            workSeconds = 0; nextApproach = 0; lastProgress = Time.time; progressPosition = unit.Position;
            SetState(GatheringState.Moving);
            return true;
        }
        public void Cancel()
        {
            if (Target != null || State == GatheringState.Moving || State == GatheringState.Gathering || State == GatheringState.StorageFull)
                unit.Stop();
            Target = null; workSeconds = 0;
            SetState(GatheringState.Idle);
        }
        private void SetState(GatheringState state)
        {
            if (State == state) return;
            State = state; Changed?.Invoke();
        }
        private void FixedUpdate()
        {
            if (ReferenceEquals(Target, null)) return;
            if (!CanGather || Target == null || !Target.IsAvailable) { Cancel(); return; }
            Vector2 surface = Target.ClosestPoint(unit.Position);
            float range = unit.Radius + settings.GatheringReach;
            bool inReach = (surface - unit.Position).sqrMagnitude <= range * range;
            // Work only accumulates while both distance and solid-obstacle visibility are valid.
            if (inReach && HasClearReach(surface))
            {
                unit.Stop(); lastProgress = Time.time;
                int amount = Mathf.Min(settings.WoodPerCycle, Target.Remaining);
                if (!stockpile.CanAddWood(amount)) { workSeconds = 0; SetState(GatheringState.StorageFull); return; }
                SetState(GatheringState.Gathering);
                workSeconds += Time.fixedDeltaTime;
                if (workSeconds >= settings.CycleSeconds)
                {
                    workSeconds = 0;
                    Target.TransferWood(stockpile, amount);
                    // A stockpile listener may cancel the order (for example, starting invasion).
                    if (Target != null && !Target.IsAvailable) Cancel();
                }
                return;
            }
            workSeconds = 0;
            SetState(GatheringState.Moving);
            if ((unit.Position - progressPosition).sqrMagnitude >= .04f)
            { progressPosition = unit.Position; lastProgress = Time.time; }
            if (Time.time - lastProgress >= settings.StalledSeconds)
            { Cancel(); SetState(GatheringState.Blocked); return; }
            if (Time.time < nextApproach) return;
            nextApproach = Time.time + settings.ApproachRetrySeconds;
            if (!unit.HasDestination)
            {
                Vector2 away = (unit.Position - surface).normalized;
                unit.NavigateTo(surface + away * (unit.Radius + .15f));
            }
        }
        private bool HasClearReach(Vector2 surface)
        {
            int count = Physics2D.Linecast(unit.Position, surface, filter, hits);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].collider != Target.Shape
                    && hits[i].collider.GetComponentInParent<CommandableUnit>() == null) return false;
            return true;
        }
    }
}
