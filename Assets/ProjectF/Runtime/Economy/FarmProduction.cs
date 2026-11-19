using System;
using ProjectF.Construction;
using ProjectF.Invasion;
using UnityEngine;

namespace ProjectF.Economy
{
    /// <summary>A harvest belongs to its farm. Pause, invasion and full storage retain its progress.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(WallStructure))]
    public sealed class FarmProduction : MonoBehaviour
    {
        private FarmSettings settings;
        private EstateStockpile stockpile;
        private PortalInvasion invasion;
        private WallStructure structure;
        private Action commitHarvest;
        public FarmSettings Settings => settings;
        public float RemainingSeconds { get; private set; }
        public bool CanProduce => isActiveAndEnabled && settings != null && structure != null && structure.IsAlive
            && stockpile != null && stockpile.isActiveAndEnabled && invasion != null && invasion.isActiveAndEnabled
            && invasion.State == InvasionState.Ready && Time.timeScale > 0;

        internal void Initialize(EstateStockpile estate, PortalInvasion portal, FarmSettings configuration)
        {
            if (gameObject.activeInHierarchy) throw new InvalidOperationException("Initialize farms before activation.");
            stockpile = estate != null ? estate : throw new ArgumentNullException(nameof(estate));
            invasion = portal != null ? portal : throw new ArgumentNullException(nameof(portal));
            settings = configuration != null ? configuration : throw new ArgumentNullException(nameof(configuration));
            RemainingSeconds = settings.HarvestSeconds;
        }
        private void Awake()
        {
            structure = GetComponent<WallStructure>();
            commitHarvest = CompleteHarvest;
        }
        private void Update()
        {
            if (!CanProduce) return;
            RemainingSeconds = Mathf.Max(0, RemainingSeconds - Time.deltaTime);
            if (RemainingSeconds == 0) stockpile.TryAddFood(settings.FoodPerHarvest, commitHarvest);
        }
        // Reset before resource observers run, including observers which disable this farm.
        private void CompleteHarvest() => RemainingSeconds = settings.HarvestSeconds;
    }
}
