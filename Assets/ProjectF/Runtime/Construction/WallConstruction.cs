using System;
using ProjectF.Economy;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Construction
{
    public enum PlacementResult { Available, Unavailable, OutsideTerritory, Occupied, InsufficientWood }
    public enum DemolitionResult { Available, Unavailable, NoWall, RefundOverflow }
    public enum RepairResult { Available, Unavailable, NoWall, AlreadyHealthy, InsufficientWood }

    /// <summary>Owns placement rules and spending. Input and preview are separate.</summary>
    public sealed class WallConstruction : MonoBehaviour
    {
        [SerializeField] private ConstructionSettings settings;
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private PortalInvasion invasion;
        [SerializeField] private NavigationWorld2D navigation;
        [SerializeField] private WallStructure wallPrefab;
        [SerializeField] private WallStructure farmPrefab;
        [SerializeField] private FarmSettings farmSettings;
        [SerializeField] private Transform structures;
        private readonly Collider2D[] overlaps = new Collider2D[16];
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private Vector2 footprint;
        private Vector2 farmFootprint;
        private bool initialized, placing;
        public event Action Changed;
        public ConstructionSettings Settings => settings;
        public Vector2 Footprint => footprint;
        public FarmSettings FarmSettings => farmSettings;
        public Vector2 FarmFootprint => farmFootprint;
        public bool HasFarmConfiguration => initialized && farmPrefab != null && farmSettings != null && farmFootprint.x > 0;
        public bool CanAffordFarm => HasFarmConfiguration && stockpile != null && stockpile.Wood >= farmSettings.WoodCost;
        public bool CanConstruct => initialized && isActiveAndEnabled && stockpile != null && stockpile.isActiveAndEnabled
            && Time.timeScale > 0 && invasion != null && invasion.isActiveAndEnabled && invasion.State == InvasionState.Ready;
        public bool CanAfford => stockpile != null && settings != null && stockpile.Wood >= settings.WallWoodCost;

        private void OnEnable() { if (invasion != null) invasion.Changed += OnInvasionChanged; }
        private void OnDisable()
        {
            if (invasion != null) invasion.Changed -= OnInvasionChanged;
            Changed?.Invoke();
        }
        private void OnInvasionChanged() => Changed?.Invoke();
        private void Start()
        {
            if (settings == null || stockpile == null || invasion == null || navigation == null || structures == null
                || wallPrefab == null || wallPrefab.gameObject.activeSelf)
            {
                Debug.LogError("Wall construction requires settings, resources, invasion, navigation and an inactive wall prefab.", this);
                enabled = false;
                return;
            }
            var shape = wallPrefab.GetComponent<BoxCollider2D>();
            footprint = Vector2.Scale(shape.size, wallPrefab.transform.localScale);
            if (shape.isTrigger || shape.offset != Vector2.zero || footprint.x <= 0 || footprint.y <= 0)
            {
                Debug.LogError("Wall prefab needs a centered, solid BoxCollider2D with positive scale.", this);
                enabled = false;
                return;
            }
            // Farms opt in; older scenes keep the wall-only setup without migration requirements.
            if (farmPrefab != null || farmSettings != null)
            {
                var farmShape = farmPrefab != null ? farmPrefab.GetComponent<BoxCollider2D>() : null;
                if (farmSettings == null || farmShape == null || farmPrefab.gameObject.activeSelf
                    || farmPrefab.GetComponent<FarmProduction>() == null || farmShape.isTrigger || farmShape.offset != Vector2.zero)
                {
                    Debug.LogError("Farm construction requires settings and an inactive farm prefab with a centered solid collider.", this);
                    enabled = false; return;
                }
                farmFootprint = Vector2.Scale(farmShape.size, farmPrefab.transform.localScale);
                if (farmFootprint.x <= 0 || farmFootprint.y <= 0)
                { Debug.LogError("Farm footprint must be positive.", this); enabled = false; return; }
            }
            initialized = true;
            Changed?.Invoke();
        }

        public PlacementResult Validate(Vector2 requested, out Vector2 snapped)
            => ValidateStructure(requested, footprint, settings != null ? settings.WallWoodCost : 0, out snapped);

        public PlacementResult ValidateFarm(Vector2 requested, out Vector2 snapped)
        {
            if (!HasFarmConfiguration) { snapped = requested; return PlacementResult.Unavailable; }
            return ValidateStructure(requested, farmFootprint, farmSettings.WoodCost, out snapped);
        }
        private PlacementResult ValidateStructure(Vector2 requested, Vector2 size, int cost, out Vector2 snapped)
        {
            snapped = settings != null ? settings.Snap(requested) : requested;
            if (!CanConstruct || placing) return PlacementResult.Unavailable;
            if (!float.IsFinite(snapped.x) || !float.IsFinite(snapped.y)) return PlacementResult.OutsideTerritory;
            Vector2 half = size * .5f;
            Rect area = settings.Territory;
            if (snapped.x - half.x < area.xMin || snapped.x + half.x > area.xMax
                || snapped.y - half.y < area.yMin || snapped.y + half.y > area.yMax) return PlacementResult.OutsideTerritory;
            if (stockpile.Wood < cost) return PlacementResult.InsufficientWood;
            Physics2D.SyncTransforms();
            // Every solid collider blocks placement, including moving units. Saturation is blocked.
            if (Physics2D.OverlapBox(snapped, size + Vector2.one * .06f, 0, filter, overlaps) > 0)
                return PlacementResult.Occupied;
            return PlacementResult.Available;
        }

        public PlacementResult TryBuild(Vector2 requested)
            => TryBuildStructure(requested, false);
        public PlacementResult TryBuildFarm(Vector2 requested)
            => TryBuildStructure(requested, true);
        private PlacementResult TryBuildStructure(Vector2 requested, bool farm)
        {
            Vector2 position;
            var result = farm ? ValidateFarm(requested, out position) : Validate(requested, out position);
            if (result != PlacementResult.Available) return result;
            placing = true;
            try
            {
                var wall = Instantiate(farm ? farmPrefab : wallPrefab, new Vector3(position.x, position.y, 0), Quaternion.identity, structures);
                int cost = farm ? farmSettings.WoodCost : settings.WallWoodCost;
                // Keep the refund agreed at purchase, even if designers later change the settings.
                wall.Initialize(navigation, this, settings.GetDemolitionRefund(cost), cost);
                if (farm) wall.GetComponent<FarmProduction>().Initialize(stockpile, invasion, farmSettings);
                if (!stockpile.TrySpendWood(cost))
                {
                    Destroy(wall.gameObject);
                    return PlacementResult.InsufficientWood;
                }
                wall.gameObject.SetActive(true);
                Physics2D.SyncTransforms();
                return PlacementResult.Available;
            }
            finally { placing = false; Changed?.Invoke(); }
        }

        public RepairResult ValidateRepair(WallStructure wall, out int woodCost)
        {
            woodCost = 0;
            if (!CanConstruct || placing) return RepairResult.Unavailable;
            if (wall == null || wall.Owner != this || !wall.IsAlive) return RepairResult.NoWall;
            if (wall.Health == wall.MaxHealth) return RepairResult.AlreadyHealthy;
            woodCost = wall.RepairWoodCost;
            return stockpile.Wood >= woodCost ? RepairResult.Available : RepairResult.InsufficientWood;
        }

        public RepairResult TryRepair(WallStructure wall)
        {
            var result = ValidateRepair(wall, out int cost);
            if (result != RepairResult.Available) return result;
            placing = true;
            try
            {
                // Both balance and health are committed before resource-change callbacks run.
                return stockpile.TrySpendWood(cost, wall.RestoreFullHealth) ? RepairResult.Available : RepairResult.InsufficientWood;
            }
            finally { placing = false; Changed?.Invoke(); }
        }

        // Shared point targeting for repair and demolition; keep the existing public entry point.
        public WallStructure FindDemolitionTarget(Vector2 point)
        {
            if (!CanConstruct || !float.IsFinite(point.x) || !float.IsFinite(point.y)) return null;
            Physics2D.SyncTransforms();
            int count = Physics2D.OverlapPoint(point, filter, overlaps);
            for (int i = 0; i < count; i++)
            {
                var wall = overlaps[i].GetComponent<WallStructure>();
                if (wall != null && wall.Owner == this && wall.IsAlive) return wall;
            }
            return null;
        }

        public DemolitionResult ValidateDemolition(WallStructure wall)
        {
            if (!CanConstruct || placing) return DemolitionResult.Unavailable;
            if (wall == null || wall.Owner != this || !wall.IsAlive) return DemolitionResult.NoWall;
            if (wall.RefundWood > 0 && !stockpile.CanAddWood(wall.RefundWood)) return DemolitionResult.RefundOverflow;
            return DemolitionResult.Available;
        }

        public DemolitionResult TryDemolish(WallStructure wall)
        {
            var result = ValidateDemolition(wall);
            if (result != DemolitionResult.Available) return result;
            placing = true;
            try
            {
                if (wall.RefundWood > 0 && !stockpile.TryAddWood(wall.RefundWood)) return DemolitionResult.RefundOverflow;
                wall.Retire();
                Destroy(wall.gameObject);
                Physics2D.SyncTransforms();
                return DemolitionResult.Available;
            }
            finally { placing = false; Changed?.Invoke(); }
        }
    }
}
