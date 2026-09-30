using System;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Combat
{
    public enum UnitFaction { Player, Hostile, Neutral }

    /// <summary>Owns health and one explicit melee target; navigation still owns all motion.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CommandableUnit))]
    public sealed class UnitCombat : MonoBehaviour
    {
        [SerializeField] private CombatSettings settings;
        [SerializeField] private UnitFaction faction;
        [SerializeField] private bool retaliateWhenIdle;
        private CommandableUnit unit;
        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[32];
        private readonly ContactFilter2D sightFilter = new ContactFilter2D { useTriggers = false };
        private float nextAttackTime, nextPursuitTime, nextStrikeCheckTime;
        private Vector2 lastTargetPosition;
        private bool issuedPursuit;
        public event Action HealthChanged;
        public event Action Died;
        public event Action<UnitCombat> Attacked;
        public UnitFaction Faction => faction;
        public int Health { get; private set; }
        public int MaxHealth => settings != null ? settings.MaxHealth : 0;
        public bool IsAlive => Health > 0 && isActiveAndEnabled && unit != null && unit.isActiveAndEnabled;
        public bool IsPlayerControlled => faction == UnitFaction.Player && IsAlive;
        public UnitCombat Target { get; private set; }
        public CommandableUnit Unit => unit;

        private void Awake()
        {
            unit = GetComponent<CommandableUnit>();
            if (settings == null)
            {
                Debug.LogError("Unit combat settings are missing.", this);
                enabled = false;
                return;
            }
            Health = settings.MaxHealth;
        }

        private void OnEnable()
        {
            unit.MoveCommandIssued += ClearTarget;
        }

        private void OnDisable()
        {
            unit.MoveCommandIssued -= ClearTarget;
            // Disabling combat alone must also release its movement order.
            if (Target != null) unit.Stop();
            ClearTarget();
        }

        public bool CanAttack(UnitCombat other) => IsAlive && other != null && other != this && other.IsAlive
            && faction != UnitFaction.Neutral && other.faction != UnitFaction.Neutral && faction != other.faction;

        public bool Attack(UnitCombat other)
        {
            if (!CanAttack(other)) return false;
            if (Target == other) return true;
            unit.Stop();
            Target = other;
            issuedPursuit = false;
            nextPursuitTime = 0;
            nextStrikeCheckTime = 0;
            // Keep the cooldown across orders so repeated clicks cannot accelerate damage.
            return true;
        }

        public void CancelAttack()
        {
            if (Target != null || issuedPursuit) unit.Stop();
            ClearTarget();
        }

        private void ClearTarget()
        {
            Target = null;
            issuedPursuit = false;
        }

        private void FixedUpdate()
        {
            if (Target == null)
            {
                // A destroyed Unity object compares equal to null before we can inspect it.
                if (issuedPursuit) CancelAttack();
                return;
            }
            if (!CanAttack(Target)) { CancelAttack(); return; }
            Vector2 delta = Target.unit.Position - unit.Position;
            float range = unit.Radius + Target.unit.Radius + settings.AttackReach;
            bool inRange = delta.sqrMagnitude <= range * range;
            if (inRange && Time.time < nextAttackTime)
            {
                unit.Stop();
                issuedPursuit = false;
                return;
            }
            // No physics query while the weapon is cooling down. Blocked strikes are
            // probed at most ten times per second, with a fresh query before every hit.
            if (inRange && Time.time >= nextStrikeCheckTime)
            {
                nextStrikeCheckTime = Time.time + .1f;
                if (HasClearStrike(Target))
                {
                    unit.Stop();
                    issuedPursuit = false;
                    nextAttackTime = Time.time + settings.AttackInterval;
                    var victim = Target;
                    victim.ReceiveDamage(settings.AttackDamage, this);
                    Attacked?.Invoke(victim);
                    if (!victim.IsAlive) CancelAttack();
                    return;
                }
            }

            if (Time.time < nextPursuitTime) return;
            nextPursuitTime = Time.time + settings.PursuitInterval;
            // Stationary targets retain their existing path/priority. Moving targets update
            // at a bounded cadence, never once per frame or per attack click.
            if (!issuedPursuit || (Target.unit.Position - lastTargetPosition).sqrMagnitude >= .25f
                || !unit.HasDestination)
            {
                lastTargetPosition = Target.unit.Position;
                unit.NavigateTo(lastTargetPosition);
                issuedPursuit = true;
            }
        }

        private bool HasClearStrike(UnitCombat other)
        {
            int count = Physics2D.Linecast(unit.Position, other.unit.Position, sightFilter, sightHits);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (sightHits[i].collider != null && sightHits[i].collider.GetComponentInParent<CommandableUnit>() == null)
                    return false;
            return true;
        }

        /// <summary>Positive damage only. A null source represents environmental damage.</summary>
        public void ReceiveDamage(int damage, UnitCombat source = null)
        {
            if (!IsAlive || damage <= 0 || (source != null && !source.CanAttack(this))) return;
            Health = Mathf.Max(0, Health - damage);
            HealthChanged?.Invoke();
            if (Health == 0)
            {
                CancelAttack();
                unit.Stop();
                Died?.Invoke();
                // Removes selection, physics and navigation occupancy through their lifecycles.
                // Health stays zero on re-enable; scene reload is the prototype's reset.
                gameObject.SetActive(false);
            }
            else if (retaliateWhenIdle && Target == null && !unit.HasDestination && CanAttack(source))
                Attack(source);
        }
    }
}
