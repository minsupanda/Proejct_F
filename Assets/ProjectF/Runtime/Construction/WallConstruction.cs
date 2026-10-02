using System;
using ProjectF.Economy;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Construction
{
    public enum PlacementResult { Available, Unavailable, OutsideTerritory, Occupied, InsufficientWood }
    public enum DemolitionResult { Available, Unavailable, NoWall, RefundOverflow }

    /// <summary>Owns placement rules and spending. Input and preview are separate.</summary>
    public sealed class WallConstruction : MonoBehaviour
    {
        [SerializeField] private ConstructionSettings settings;
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private PortalInvasion invasion;
        [SerializeField] private NavigationWorld2D navigation;
        [SerializeField] private WallStructure wallPrefab;
        [SerializeField] private Transform structures;
        private readonly Collider2D[] overlaps = new Collider2D[16];
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private Vector2 footprint;
        private bool initialized, placing;
        public event Action Changed;
        public ConstructionSettings Settings => settings;
        public Vector2 Footprint => footprint;
        public bool CanConstruct => initialized && isActiveAndEnabled && stockpile.isActiveAndEnabled
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
            initialized = true;
            Changed?.Invoke();
        }

        public PlacementResult Validate(Vector2 requested, out Vector2 snapped)
        {
            snapped = settings != null ? settings.Snap(requested) : requested;
            if (!CanConstruct || placing) return PlacementResult.Unavailable;
            if (!float.IsFinite(snapped.x) || !float.IsFinite(snapped.y)) return PlacementResult.OutsideTerritory;
            Vector2 half = footprint * .5f;
            Rect area = settings.Territory;
            if (snapped.x - half.x < area.xMin || snapped.x + half.x > area.xMax
                || snapped.y - half.y < area.yMin || snapped.y + half.y > area.yMax) return PlacementResult.OutsideTerritory;
            if (!CanAfford) return PlacementResult.InsufficientWood;
            Physics2D.SyncTransforms();
            // Every solid collider blocks placement, including moving units. Saturation is blocked.
            if (Physics2D.OverlapBox(snapped, footprint + Vector2.one * .06f, 0, filter, overlaps) > 0)
                return PlacementResult.Occupied;
            return PlacementResult.Available;
        }

        public PlacementResult TryBuild(Vector2 requested)
        {
            var result = Validate(requested, out var position);
            if (result != PlacementResult.Available) return result;
            placing = true;
            try
            {
                var wall = Instantiate(wallPrefab, new Vector3(position.x, position.y, 0), Quaternion.identity, structures);
                int cost = settings.WallWoodCost;
                // Keep the refund agreed at purchase, even if designers later change the settings.
                wall.Initialize(navigation, this, settings.GetDemolitionRefund(cost));
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
