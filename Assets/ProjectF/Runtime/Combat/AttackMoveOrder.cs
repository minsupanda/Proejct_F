using System.Collections.Generic;
using ProjectF.Economy;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Combat
{
    // Validate pursuit limits before UnitCombat's physics tick can strike again.
    [DefaultExecutionOrder(-40), DisallowMultipleComponent, RequireComponent(typeof(UnitCombat))]
    public sealed class AttackMoveOrder : MonoBehaviour
    {
        [SerializeField] private AttackMoveSettings settings;
        private UnitCombat combat;
        private CommandableUnit unit;
        private WoodGatherer gatherer;
        private readonly List<Collider2D> nearby = new List<Collider2D>(32);
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private UnitCombat target;
        private Vector2 engagementOrigin, progressPosition;
        private float nextScan, lastProgress;
        private bool issuingAttack;
        public bool IsActive { get; private set; }
        public Vector2 Destination { get; private set; }
        public int DetectionScanCount { get; private set; }
        private void Awake()
        {
            combat = GetComponent<UnitCombat>(); unit = GetComponent<CommandableUnit>(); gatherer = GetComponent<WoodGatherer>();
            if (settings == null) { Debug.LogError("Attack move settings are missing.", this); enabled = false; }
        }
        private void OnEnable()
        {
            unit.MoveCommandIssued += ClearOrder; unit.StopCommandIssued += ClearOrder;
            combat.AttackCommandIssued += OnAttackCommand; combat.Attacked += OnStrike;
            if (gatherer != null) gatherer.Changed += OnGatherChanged;
        }
        private void OnDisable()
        {
            unit.MoveCommandIssued -= ClearOrder; unit.StopCommandIssued -= ClearOrder;
            combat.AttackCommandIssued -= OnAttackCommand; combat.Attacked -= OnStrike;
            if (gatherer != null) gatherer.Changed -= OnGatherChanged;
            if (IsActive) { combat.CancelAttack(); unit.Stop(); }
            ClearOrder(); nearby.Clear();
        }
        public bool Begin(Vector2 destination)
        {
            if (!isActiveAndEnabled || !combat.IsPlayerControlled || Time.timeScale <= 0
                || !float.IsFinite(destination.x) || !float.IsFinite(destination.y)) return false;
            // Existing move notifications cancel gathering, explicit attacks and any old advance.
            unit.MoveTo(destination);
            Destination = destination; IsActive = true; target = null; DetectionScanCount = 0;
            uint phase = unchecked((uint)GetEntityId().GetHashCode() * 2654435761u);
            nextScan = Time.time + (phase % 997) / 997f * settings.ScanInterval;
            return true;
        }
        private void ClearOrder() { IsActive = false; target = null; }
        private void OnAttackCommand() { if (!issuingAttack) ClearOrder(); }
        private void OnGatherChanged() { if (gatherer.Target != null) ClearOrder(); }
        private void OnStrike(UnitCombat _) => lastProgress = Time.time;

        private void FixedUpdate()
        {
            if (!IsActive) return;
            if (!combat.IsPlayerControlled) { ClearOrder(); return; }
            if (!ReferenceEquals(target, null))
            {
                if (!combat.CanAttack(target) || combat.Target != target) { Continue(); return; }
                if ((unit.Position - progressPosition).sqrMagnitude >= .04f)
                { progressPosition = unit.Position; lastProgress = Time.time; }
                float leash = settings.PursuitRadius;
                if ((target.Unit.Position - engagementOrigin).sqrMagnitude > leash * leash
                    || (unit.Position - engagementOrigin).sqrMagnitude > leash * leash
                    || Time.time - lastProgress >= settings.StalledSeconds) Continue();
                return;
            }
            // Arrival and an unreachable destination both finish this finite order.
            if (!unit.HasDestination) { ClearOrder(); return; }
            if (Time.time < nextScan) return;
            nextScan = Time.time + settings.ScanInterval; DetectionScanCount++;
            nearby.Clear(); Physics2D.OverlapCircle(unit.Position, settings.DetectionRadius, filter, nearby);
            UnitCombat nearest = null; float distance = settings.DetectionRadius * settings.DetectionRadius;
            foreach (var shape in nearby)
            {
                var candidate = shape.GetComponentInParent<UnitCombat>();
                if (!combat.CanAttack(candidate)) continue;
                float squared = (candidate.Unit.Position - unit.Position).sqrMagnitude;
                if (squared >= distance || !combat.HasLineOfSight(candidate)) continue;
                nearest = candidate; distance = squared;
            }
            if (nearest == null) return;
            issuingAttack = true;
            try
            {
                if (!combat.Attack(nearest)) return;
                target = nearest; engagementOrigin = progressPosition = unit.Position; lastProgress = Time.time;
            }
            finally { issuingAttack = false; }
        }
        private void Continue()
        {
            combat.CancelAttack(); target = null;
            unit.NavigateTo(Destination);
            // Give the original route time to make progress before probing another engagement.
            nextScan = Time.time + settings.RecoverySeconds;
        }
    }
}
