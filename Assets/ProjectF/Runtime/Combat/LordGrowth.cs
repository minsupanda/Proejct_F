using System;
using ProjectF.Invasion;
using UnityEngine;

namespace ProjectF.Combat
{
    public enum LordDiscipline { Martial, Leadership }

    /// <summary>Victory milestones and permanent choices for this scene's lord. No combat XP.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnitCombat))]
    public sealed class LordGrowth : MonoBehaviour
    {
        [SerializeField] private LordGrowthSettings settings;
        [SerializeField] private PortalInvasion invasion;
        private UnitCombat combat;
        private int lastRewardedWave;
        private bool spending;
        public event Action Changed;
        public LordGrowthSettings Settings => settings;
        public int Points { get; private set; }
        public int MartialRank { get; private set; }
        public int LeadershipRank { get; private set; }
        // Purchased improvements persist through temporary disable and the lord's death.
        public int DamageBonus => settings != null ? MartialRank * settings.DamagePerRank : 0;
        public int CapacityBonus => settings != null ? LeadershipRank * settings.CapacityPerRank : 0;
        public bool CanInvest => isActiveAndEnabled && !spending && settings != null && combat != null
            && combat.IsPlayerControlled && invasion != null && invasion.isActiveAndEnabled && Time.timeScale > 0
            && (invasion.State == InvasionState.Ready || invasion.State == InvasionState.Repelled);

        private void Awake()
        {
            combat = GetComponent<UnitCombat>();
            if (settings == null || invasion == null || combat.Faction != UnitFaction.Player)
            { Debug.LogError("Lord growth requires settings, invasion and a player lord.", this); enabled = false; }
        }
        private void OnEnable()
        {
            if (invasion != null) invasion.Changed += OnInvasionChanged;
            OnInvasionChanged();
        }
        private void OnDisable()
        {
            if (invasion != null) invasion.Changed -= OnInvasionChanged;
            // No value changed. In particular, do not wake scene consumers during teardown.
        }
        private void OnInvasionChanged()
        {
            if (isActiveAndEnabled && settings != null && combat != null && combat.IsPlayerControlled
                && invasion != null && invasion.isActiveAndEnabled && invasion.State == InvasionState.Repelled
                && invasion.WaveNumber > lastRewardedWave)
            {
                // Record the milestone before notifying: repeated state events cannot award it twice.
                lastRewardedWave = invasion.WaveNumber;
                Points += settings.PointsPerVictory;
                Changed?.Invoke();
            }
        }
        public bool CanUpgrade(LordDiscipline discipline)
        {
            if (!CanInvest || Points < 1) return false;
            return discipline == LordDiscipline.Martial ? MartialRank < settings.MaximumRank
                : discipline == LordDiscipline.Leadership && LeadershipRank < settings.MaximumRank;
        }
        public bool TryUpgrade(LordDiscipline discipline)
        {
            if (!CanUpgrade(discipline)) return false;
            spending = true;
            Points--;
            if (discipline == LordDiscipline.Martial) MartialRank++;
            else LeadershipRank++;
            try { Changed?.Invoke(); }
            finally { spending = false; }
            return true;
        }
    }
}
