using System;
using System.Collections.Generic;
using ProjectF.Player;
using Unity.Profiling;
using UnityEngine;

namespace ProjectF.Combat
{
    public enum EnemyAIState { Guarding, Engaging, Returning, Advancing }

    /// <summary>Owns hostile target decisions. Combat and navigation retain damage and motion.</summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent, RequireComponent(typeof(UnitCombat))]
    public sealed class EnemyCombatAI : MonoBehaviour
    {
        [SerializeField] private EnemyAISettings settings;
        private UnitCombat combat;
        private CommandableUnit unit;
        private readonly List<Collider2D> nearby = new List<Collider2D>(32);
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private static readonly ProfilerMarker detectMarker = new ProfilerMarker("ProjectF.EnemyAI.Detect");
        private float nextThink, lastSeen, lastProgress, nextReturnRetry, recoverUntil;
        private Vector2 progressPosition;
        private Vector2 advanceDestination, engagementOrigin;
        private bool advancing;
        public event Action<EnemyAIState> StateChanged;
        public EnemyAIState State { get; private set; }
        public Vector2 HomePosition { get; private set; }
        public int DetectionScanCount { get; private set; }
        public int LastCandidateCount { get; private set; }

        private void Awake()
        {
            combat = GetComponent<UnitCombat>();
            unit = GetComponent<CommandableUnit>();
            if (settings == null || combat.Faction != UnitFaction.Hostile)
            {
                Debug.LogError("Enemy AI requires settings and a Hostile faction.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            HomePosition = transform.position;
            State = EnemyAIState.Guarding;
            DetectionScanCount = LastCandidateCount = 0;
            recoverUntil = 0;
            advancing = false;
            // Spread spawned guards across their first interval without a static registry or RNG.
            uint phase = unchecked((uint)GetEntityId().GetHashCode() * 2654435761u);
            nextThink = Time.time + (phase % 997) / 997f * settings.ThinkInterval;
            combat.SuppressIdleRetaliation = true;
            combat.Attacked += OnAttack;
            StateChanged?.Invoke(State);
        }

        private void OnDisable()
        {
            combat.Attacked -= OnAttack;
            combat.SuppressIdleRetaliation = false;
            combat.CancelAttack();
            if (unit != null) unit.Stop();
            nearby.Clear();
        }

        private void OnAttack(UnitCombat _) => lastProgress = Time.time;

        /// <summary>Advance using normal navigation, engaging visible opponents along the route.</summary>
        public bool AdvanceTo(Vector2 destination)
        {
            if (!isActiveAndEnabled || !combat.IsAlive || !float.IsFinite(destination.x) || !float.IsFinite(destination.y)) return false;
            advancing = true;
            advanceDestination = destination;
            ResumeAdvance();
            return true;
        }

        private void ResumeAdvance()
        {
            combat.CancelAttack();
            ChangeState(EnemyAIState.Advancing);
            unit.NavigateTo(advanceDestination);
            nextReturnRetry = Time.time + settings.ReturnRetryInterval;
            recoverUntil = Time.time + settings.RecoveryDelay;
        }

        private void FixedUpdate()
        {
            if (!combat.IsAlive) return;
            if (State == EnemyAIState.Engaging)
            {
                var target = combat.Target;
                float leash = settings.LeashRadius;
                Vector2 origin = advancing ? engagementOrigin : HomePosition;
                // Cheap lifetime and leash checks run before UnitCombat's physics tick, so an
                // invalid target cannot receive one more hit while we wait for a thinking tick.
                if (!combat.CanAttack(target) || (target.Unit.Position - origin).sqrMagnitude > leash * leash
                    || (unit.Position - origin).sqrMagnitude > leash * leash)
                {
                    BeginReturn();
                    return;
                }
            }
            if (Time.time < nextThink) return;
            nextThink = Time.time + settings.ThinkInterval;
            switch (State)
            {
                case EnemyAIState.Guarding:
                case EnemyAIState.Advancing:
                    if (State == EnemyAIState.Advancing)
                    {
                        // The cooperative planner may assign a nearby free arrival slot when
                        // several raiders share a destination. Respect its completed arrival.
                        if (unit.TravelState == UnitTravelState.Arrived
                            || (unit.Position - advanceDestination).sqrMagnitude <= settings.ReturnTolerance * settings.ReturnTolerance)
                        {
                            unit.Stop();
                            HomePosition = unit.Position;
                            advancing = false;
                            ChangeState(EnemyAIState.Guarding);
                        }
                        else if (!unit.HasDestination && Time.time >= nextReturnRetry)
                        {
                            unit.NavigateTo(advanceDestination);
                            nextReturnRetry = Time.time + settings.ReturnRetryInterval;
                        }
                    }
                    if (Time.time < recoverUntil) return;
                    var target = FindTarget();
                    if (target != null && combat.Attack(target))
                    {
                        engagementOrigin = unit.Position;
                        lastSeen = lastProgress = Time.time;
                        progressPosition = unit.Position;
                        ChangeState(EnemyAIState.Engaging);
                    }
                    break;
                case EnemyAIState.Engaging:
                    if (combat.HasLineOfSight(combat.Target)) lastSeen = Time.time;
                    if ((unit.Position - progressPosition).sqrMagnitude >= .04f)
                    {
                        progressPosition = unit.Position;
                        lastProgress = Time.time;
                    }
                    if (Time.time - lastSeen >= settings.LostSightDelay
                        || Time.time - lastProgress >= settings.StalledPursuitTimeout) BeginReturn();
                    break;
                case EnemyAIState.Returning:
                    // No target acquisition/retaliation until physically back at the guard post.
                    combat.CancelAttack();
                    if ((unit.Position - HomePosition).sqrMagnitude <= settings.ReturnTolerance * settings.ReturnTolerance)
                    {
                        unit.Stop();
                        recoverUntil = Time.time + settings.RecoveryDelay;
                        ChangeState(EnemyAIState.Guarding);
                    }
                    else if (!unit.HasDestination && Time.time >= nextReturnRetry) IssueReturn();
                    break;
            }
        }

        private UnitCombat FindTarget()
        {
            using (detectMarker.Auto())
            {
                DetectionScanCount++;
                nearby.Clear();
                Physics2D.OverlapCircle(unit.Position, settings.DetectionRadius, filter, nearby);
                LastCandidateCount = nearby.Count;
                UnitCombat best = null;
                float bestDistance = settings.DetectionRadius * settings.DetectionRadius;
                float leashSquared = settings.LeashRadius * settings.LeashRadius;
                foreach (var collider in nearby)
                {
                    var body = collider.attachedRigidbody;
                    if (body == null || !body.TryGetComponent<UnitCombat>(out var candidate) || !combat.CanAttack(candidate)) continue;
                    Vector2 position = candidate.Unit.Position;
                    float distance = (position - unit.Position).sqrMagnitude;
                    if (distance > bestDistance || (!advancing && (position - HomePosition).sqrMagnitude > leashSquared)) continue;
                    // Stable tie-break avoids switching between equally distant units on scans.
                    if (distance == bestDistance && best != null && candidate.GetEntityId().CompareTo(best.GetEntityId()) >= 0) continue;
                    if (!combat.HasLineOfSight(candidate)) continue;
                    best = candidate;
                    bestDistance = distance;
                }
                return best;
            }
        }

        private void BeginReturn()
        {
            if (advancing) { ResumeAdvance(); return; }
            combat.CancelAttack();
            ChangeState(EnemyAIState.Returning);
            IssueReturn();
            nextThink = Time.time + settings.ThinkInterval;
        }

        private void IssueReturn()
        {
            unit.NavigateTo(HomePosition);
            nextReturnRetry = Time.time + settings.ReturnRetryInterval;
        }

        private void ChangeState(EnemyAIState value)
        {
            if (State == value) return;
            State = value;
            StateChanged?.Invoke(value);
        }

        private void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, settings.DetectionRadius);
            Gizmos.color = new Color(1, .6f, .15f);
            Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)HomePosition : transform.position, settings.LeashRadius);
        }
    }
}
