using UnityEngine;

namespace ProjectF.Combat
{
    public enum ProficiencyRank { Recruit, Trained, Veteran }

    /// <summary>Per-soldier practice from landed hostile strikes. Survives rounds, resets with the scene.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnitCombat))]
    public sealed class SoldierProficiency : MonoBehaviour
    {
        [SerializeField] private ProficiencySettings settings;
        private UnitCombat combat;
        public int Experience { get; private set; }
        public ProficiencyRank Rank => settings == null ? ProficiencyRank.Recruit
            : Experience >= settings.VeteranHits ? ProficiencyRank.Veteran
            : Experience >= settings.TrainedHits ? ProficiencyRank.Trained : ProficiencyRank.Recruit;
        public int NextThreshold => settings == null ? 0 : Rank == ProficiencyRank.Recruit ? settings.TrainedHits : settings.VeteranHits;
        public int DamageBonus => !isActiveAndEnabled || combat == null || !combat.IsPlayerControlled || settings == null ? 0
            : Rank == ProficiencyRank.Veteran ? settings.VeteranDamageBonus
            : Rank == ProficiencyRank.Trained ? settings.TrainedDamageBonus : 0;

        private void Awake()
        {
            combat = GetComponent<UnitCombat>();
            if (settings == null) { Debug.LogError("Soldier proficiency settings are missing.", this); enabled = false; }
        }
        private void OnEnable() => combat.Attacked += OnStrike;
        private void OnDisable() => combat.Attacked -= OnStrike;
        private void OnStrike(UnitCombat victim)
        {
            // Attacked is emitted only after a valid melee hit, including the killing blow.
            // This event does not include wall strikes, damage taken or cancelled orders.
            if (!combat.IsPlayerControlled || victim == null || victim.Faction != UnitFaction.Hostile || Time.timeScale <= 0) return;
            Experience = Mathf.Min(settings.VeteranHits, Experience + 1);
        }
    }
}
