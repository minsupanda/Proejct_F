using System;
using ProjectF.Invasion;
using UnityEngine;

namespace ProjectF.Economy
{
    /// <summary>One ration issue per defense. Shortage weakens the army; it never prevents an enemy invasion.</summary>
    public sealed class ArmySupply : MonoBehaviour
    {
        [SerializeField] private ArmySupplySettings settings;
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private PortalInvasion invasion;
        private int suppliedWave;
        public event Action Changed;
        public ArmySupplySettings Settings => settings;
        public int RequiredFood => settings != null && invasion != null ? settings.RequiredFood(invasion.DefenderCount) : 0;
        public int AvailableFood => stockpile != null && stockpile.isActiveAndEnabled ? stockpile.Food : 0;
        public int PreviewDamagePercent => settings != null ? settings.DamagePercent(RequiredFood, AvailableFood) : 100;
        public int LastRequiredFood { get; private set; }
        public int LastConsumedFood { get; private set; }
        public int LastDamagePercent { get; private set; } = 100;
        public bool IsDefenseActive => invasion != null && (invasion.State == InvasionState.Preparing
            || invasion.State == InvasionState.Spawning || invasion.State == InvasionState.Fighting);
        public int CurrentDamagePercent => isActiveAndEnabled && IsDefenseActive && suppliedWave == invasion.WaveNumber
            ? LastDamagePercent : 100;

        private void Awake()
        {
            if (settings != null && stockpile != null && invasion != null) return;
            Debug.LogError("Army supply requires settings, stockpile and invasion references.", this);
            enabled = false;
        }
        private void OnEnable()
        {
            if (stockpile != null) stockpile.Changed += NotifyChanged;
            if (invasion != null) invasion.Changed += NotifyChanged;
            NotifyChanged();
        }
        private void OnDisable()
        {
            if (stockpile != null) stockpile.Changed -= NotifyChanged;
            if (invasion != null) invasion.Changed -= NotifyChanged;
            NotifyChanged();
        }
        private void NotifyChanged() => Changed?.Invoke();

        // PortalInvasion commits Preparing before calling this, then broadcasts its transition.
        internal void PrepareForWave(int wave, int defenders)
        {
            if (!isActiveAndEnabled || settings == null || suppliedWave == wave) return;
            suppliedWave = wave;
            LastRequiredFood = settings.RequiredFood(defenders);
            LastConsumedFood = Mathf.Min(LastRequiredFood, AvailableFood);
            LastDamagePercent = settings.DamagePercent(LastRequiredFood, LastConsumedFood);
            // Final ration state is visible before resource-change observers run.
            if (LastConsumedFood > 0 && !stockpile.TrySpendFood(LastConsumedFood))
            {
                LastConsumedFood = 0;
                LastDamagePercent = settings.DamagePercent(LastRequiredFood, 0);
            }
        }
    }
}
