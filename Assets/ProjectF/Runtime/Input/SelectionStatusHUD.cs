using ProjectF.Combat;
using ProjectF.Economy;
using UnityEngine;

namespace ProjectF.Input
{
    /// <summary>Read-only selection summary. Never issues orders or owns unit state.</summary>
    public sealed class SelectionStatusHUD : MonoBehaviour
    {
        [SerializeField] private UnitCommandController commands;
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text status;
        private const float RefreshInterval = .1f;
        private float nextRefresh;
        private Summary previous;
        private bool hasPrevious;

        private struct Summary
        {
            public int Count, Combatants, Moving, Attacking, Gathering, Idle, Advancing;
            public long Health, Maximum;
            public bool SameAs(Summary other) => Count == other.Count && Combatants == other.Combatants
                && Moving == other.Moving && Attacking == other.Attacking && Gathering == other.Gathering
                && Idle == other.Idle && Advancing == other.Advancing && Health == other.Health && Maximum == other.Maximum;
        }

        private void OnEnable() { hasPrevious = false; nextRefresh = 0; }
        private void OnDisable() { if (panel != null) panel.SetActive(false); }

        private void LateUpdate()
        {
            // Unscaled time keeps selection/health feedback current while simulation is paused.
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshInterval;
            if (panel == null || status == null) return;
            var current = ReadSelection();
            if (hasPrevious && previous.SameAs(current)) return;
            previous = current; hasPrevious = true;
            panel.SetActive(current.Count > 0);
            if (current.Count == 0) { status.text = string.Empty; return; }
            string health = current.Combatants == 0 ? "N/A" : $"{current.Health} / {current.Maximum}";
            status.text = $"SELECTED {current.Count}\nHEALTH {health}\nMOVE {current.Moving}  ATTACK {current.Attacking}\nGATHER {current.Gathering}  IDLE {current.Idle}\nADVANCE {current.Advancing}";
        }

        private Summary ReadSelection()
        {
            var result = new Summary();
            if (commands == null || !commands.isActiveAndEnabled) return result;
            var selected = commands.Selected;
            for (int i = 0; i < selected.Count; i++)
            {
                var unit = selected[i];
                if (unit == null || !unit.isActiveAndEnabled) continue;
                unit.TryGetComponent<UnitCombat>(out var combat);
                if (combat != null && !combat.IsPlayerControlled) continue;
                result.Count++;
                if (combat != null) { result.Combatants++; result.Health += combat.Health; result.Maximum += combat.MaxHealth; }
                // Classify the active order, including approach/pursuit, once per unit.
                if (unit.TryGetComponent<AttackMoveOrder>(out var advance) && advance.IsActive) result.Advancing++;
                else if (combat != null && (combat.Target != null || combat.WallTarget != null)) result.Attacking++;
                else if (unit.TryGetComponent<WoodGatherer>(out var gatherer) && gatherer.isActiveAndEnabled && gatherer.Target != null) result.Gathering++;
                else if (unit.HasDestination) result.Moving++;
                else result.Idle++;
            }
            return result;
        }
    }
}
