using System;
using ProjectF.Input;
using UnityEngine;

namespace ProjectF.Construction
{
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
        public event Action Changed;
        public bool IsPlacing { get; private set; }
        public PlacementResult Result { get; private set; } = PlacementResult.Available;
        public bool PreviewVisible => preview.enabled;

        private void Awake() { properties = new MaterialPropertyBlock(); preview.enabled = false; boundary.enabled = false; }
        private void OnEnable() => construction.Changed += OnConstructionChanged;
        private void OnDisable() { construction.Changed -= OnConstructionChanged; Cancel(); }
        private void OnConstructionChanged()
        {
            nextPreviewCheck = 0;
            if (IsPlacing && !construction.CanConstruct) Cancel();
            Changed?.Invoke();
        }
        public void Toggle()
        {
            if (IsPlacing) { Cancel(); return; }
            if (!isActiveAndEnabled || !construction.CanConstruct || !construction.CanAfford) return;
            IsPlacing = true;
            Result = PlacementResult.Available;
            commands.SetPointerCommandsBlocked(this, true);
            nextPreviewCheck = 0;
            Rect area = construction.Settings.Territory;
            boundary.positionCount = 4;
            boundary.SetPosition(0, new Vector3(area.xMin, area.yMin, -.3f));
            boundary.SetPosition(1, new Vector3(area.xMax, area.yMin, -.3f));
            boundary.SetPosition(2, new Vector3(area.xMax, area.yMax, -.3f));
            boundary.SetPosition(3, new Vector3(area.xMin, area.yMax, -.3f));
            boundary.enabled = true;
            Changed?.Invoke();
        }
        public void Cancel()
        {
            if (!IsPlacing) return;
            IsPlacing = false;
            preview.enabled = boundary.enabled = false;
            commands.SetPointerCommandsBlocked(this, false);
            Changed?.Invoke();
        }
        private void Update()
        {
            if (!IsPlacing) return;
            if (!input.Ready || !construction.CanConstruct || input.CancelPressed || input.CommandPressed) { Cancel(); return; }
            if (input.PanHeld || !worldCamera.pixelRect.Contains(input.Pointer) || commands.PointerOverUI()) { preview.enabled = false; return; }
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
