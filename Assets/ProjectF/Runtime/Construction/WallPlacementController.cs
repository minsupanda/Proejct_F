using System;
using ProjectF.Input;
using UnityEngine;

namespace ProjectF.Construction
{
    public enum ConstructionMode { None, Build, Demolish }
    [DefaultExecutionOrder(-10)]
    public sealed class WallPlacementController : MonoBehaviour
    {
        [SerializeField] private WallConstruction construction;
        [SerializeField] private CommandInput input;
        [SerializeField] private UnitCommandController commands;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private MeshRenderer preview;
        [SerializeField] private LineRenderer boundary;
        private MaterialPropertyBlock properties;
        private static readonly int colorId = Shader.PropertyToID("_BaseColor");
        private float nextPreviewCheck;
        private Vector2 lastCell;
        private Vector2 lastDemolitionPoint;
        public event Action Changed;
        public ConstructionMode Mode { get; private set; }
        public bool IsPlacing => Mode == ConstructionMode.Build;
        public bool IsDemolishing => Mode == ConstructionMode.Demolish;
        public bool IsEditing => Mode != ConstructionMode.None;
        public WallStructure DemolitionTarget { get; private set; }
        public DemolitionResult DemolitionStatus { get; private set; } = DemolitionResult.NoWall;
        public PlacementResult Result { get; private set; } = PlacementResult.Available;
        public bool PreviewVisible => preview.enabled;

        private void Awake() { properties = new MaterialPropertyBlock(); preview.enabled = false; boundary.enabled = false; }
        private void OnEnable() => construction.Changed += OnConstructionChanged;
        private void OnDisable() { construction.Changed -= OnConstructionChanged; Cancel(); }
        private void OnConstructionChanged()
        {
            nextPreviewCheck = 0;
            if (IsEditing && !construction.CanConstruct) Cancel();
            Changed?.Invoke();
        }
        public void Toggle()
        {
            if (IsPlacing) { Cancel(); return; }
            if (!isActiveAndEnabled || !construction.CanConstruct || !construction.CanAfford) return;
            BeginMode(ConstructionMode.Build);
        }
        public void ToggleDemolition()
        {
            if (IsDemolishing) { Cancel(); return; }
            if (!isActiveAndEnabled || !construction.CanConstruct) return;
            BeginMode(ConstructionMode.Demolish);
        }
        private void BeginMode(ConstructionMode mode)
        {
            Mode = mode;
            preview.enabled = false;
            DemolitionTarget = null;
            DemolitionStatus = DemolitionResult.NoWall;
            Result = PlacementResult.Available;
            commands.SetPointerCommandsBlocked(this, true);
            nextPreviewCheck = 0;
            Rect area = construction.Settings.Territory;
            boundary.positionCount = 4;
            boundary.SetPosition(0, new Vector3(area.xMin, area.yMin, -.3f));
            boundary.SetPosition(1, new Vector3(area.xMax, area.yMin, -.3f));
            boundary.SetPosition(2, new Vector3(area.xMax, area.yMax, -.3f));
            boundary.SetPosition(3, new Vector3(area.xMin, area.yMax, -.3f));
            boundary.enabled = IsPlacing;
            Changed?.Invoke();
        }
        public void Cancel()
        {
            if (!IsEditing) return;
            Mode = ConstructionMode.None;
            DemolitionTarget = null;
            DemolitionStatus = DemolitionResult.NoWall;
            preview.enabled = boundary.enabled = false;
            commands.SetPointerCommandsBlocked(this, false);
            Changed?.Invoke();
        }
        private void Update()
        {
            if (!IsEditing) return;
            if (!input.Ready || !construction.CanConstruct || input.CancelPressed || input.CommandPressed) { Cancel(); return; }
            if (input.PanHeld || !worldCamera.pixelRect.Contains(input.Pointer) || commands.PointerOverUI())
            {
                preview.enabled = false;
                nextPreviewCheck = 0;
                if (IsDemolishing) SetDemolitionTarget(null);
                return;
            }
            if (IsDemolishing) { UpdateDemolition(); return; }
            Vector2 cell = construction.Settings.Snap(worldCamera.ScreenToWorldPoint(input.Pointer));
            if (cell != lastCell || Time.time >= nextPreviewCheck || !preview.enabled)
            {
                lastCell = cell;
                nextPreviewCheck = Time.time + .1f;
                SetResult(construction.Validate(cell, out _));
                preview.transform.position = new Vector3(cell.x, cell.y, -.4f);
                preview.transform.localScale = new Vector3(construction.Footprint.x, construction.Footprint.y, 1);
                preview.enabled = true;
            }
            if (input.SelectPressed)
            {
                // Revalidate at the click, even when the preview is still green from a prior tick.
                var result = construction.TryBuild(cell);
                SetResult(result == PlacementResult.Available ? construction.Validate(cell, out _) : result);
            }
        }
        private void UpdateDemolition()
        {
            // Point targeting avoids deleting an adjacent wall when clicking the gap between cells.
            Vector2 point = worldCamera.ScreenToWorldPoint(input.Pointer);
            if (point == lastDemolitionPoint && Time.time < nextPreviewCheck && !input.SelectPressed) return;
            lastDemolitionPoint = point;
            nextPreviewCheck = Time.time + .1f;
            var target = construction.FindDemolitionTarget(point);
            SetDemolitionTarget(target);
            if (input.SelectPressed && target != null)
            {
                construction.TryDemolish(target);
                SetDemolitionTarget(construction.FindDemolitionTarget(point));
            }
        }
        private void SetDemolitionTarget(WallStructure target)
        {
            var state = construction.ValidateDemolition(target);
            bool changed = DemolitionTarget != target || DemolitionStatus != state;
            DemolitionTarget = target;
            DemolitionStatus = state;
            preview.enabled = target != null;
            if (target != null)
            {
                preview.transform.position = target.transform.position + new Vector3(0, 0, -.4f);
                preview.transform.localScale = new Vector3(construction.Footprint.x, construction.Footprint.y, 1);
                properties.SetColor(colorId, state == DemolitionResult.Available ? new Color(1f, .65f, .15f) : new Color(.95f, .25f, .25f));
                preview.SetPropertyBlock(properties);
            }
            if (changed) Changed?.Invoke();
        }
        private void SetResult(PlacementResult value)
        {
            bool changed = Result != value;
            Result = value;
            properties.SetColor(colorId, value == PlacementResult.Available ? new Color(.3f, .9f, .6f) : new Color(.95f, .25f, .25f));
            preview.SetPropertyBlock(properties);
            if (changed) Changed?.Invoke();
        }
    }
}
