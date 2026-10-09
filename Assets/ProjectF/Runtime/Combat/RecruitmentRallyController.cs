using System;
using ProjectF.Construction;
using ProjectF.Input;
using UnityEngine;

namespace ProjectF.Combat
{
    /// <summary>Owns rally editing input and markers. Recruitment owns the session point.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RecruitmentRallyController : MonoBehaviour
    {
        [SerializeField] private UnitRecruitment recruitment;
        [SerializeField] private CommandInput input;
        [SerializeField] private UnitCommandController commands;
        [SerializeField] private WallPlacementController placement;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LineRenderer marker;
        [SerializeField] private LineRenderer preview;
        private float nextCheck;
        private Vector2 lastPoint;
        private MaterialPropertyBlock properties;
        private static readonly int colorId = Shader.PropertyToID("_BaseColor");
        public event Action Changed;
        public bool IsChoosing { get; private set; }
        public bool PreviewVisible => preview.enabled;
        public RallyPointResult Result { get; private set; } = RallyPointResult.Unavailable;
        private void Awake() => properties = new MaterialPropertyBlock();
        private void OnEnable()
        {
            recruitment.Changed += Refresh; placement.Changed += OnPlacementChanged;
            preview.enabled = false; Refresh();
        }
        private void OnDisable()
        {
            recruitment.Changed -= Refresh; placement.Changed -= OnPlacementChanged;
            Cancel(); marker.enabled = preview.enabled = false;
            Changed?.Invoke();
        }
        public void Toggle()
        {
            if (IsChoosing) { Cancel(); return; }
            if (!isActiveAndEnabled || !recruitment.PreparationAvailable) return;
            placement.Cancel(); IsChoosing = true; nextCheck = 0; Result = RallyPointResult.Unavailable;
            commands.SetPointerCommandsBlocked(this, true); Changed?.Invoke();
        }
        public void Cancel()
        {
            if (!IsChoosing) return;
            IsChoosing = false; preview.enabled = false;
            commands.SetPointerCommandsBlocked(this, false); Changed?.Invoke();
        }
        public void Clear()
        {
            if (!isActiveAndEnabled || !recruitment.PreparationAvailable) return;
            Cancel(); recruitment.ClearRallyPoint();
        }
        private void OnPlacementChanged() { if (placement.IsEditing) Cancel(); }
        private void Refresh()
        {
            if (IsChoosing && !recruitment.PreparationAvailable) Cancel();
            marker.enabled = recruitment.HasRallyPoint && recruitment.isActiveAndEnabled;
            if (marker.enabled) marker.transform.position = new Vector3(recruitment.RallyPoint.x, recruitment.RallyPoint.y, -.5f);
            nextCheck = 0; Changed?.Invoke();
        }
        private void Update()
        {
            if (!IsChoosing) return;
            if (!input.Ready || !recruitment.PreparationAvailable || input.CancelPressed || input.CommandPressed) { Cancel(); return; }
            if (input.PanHeld || !worldCamera.pixelRect.Contains(input.Pointer) || commands.PointerOverUI())
            {
                if (preview.enabled) { preview.enabled = false; Changed?.Invoke(); }
                nextCheck = 0; return;
            }
            Vector2 point = worldCamera.ScreenToWorldPoint(input.Pointer);
            if (point != lastPoint || Time.unscaledTime >= nextCheck || input.SelectPressed)
            {
                lastPoint = point; nextCheck = Time.unscaledTime + .1f;
                var result = recruitment.ValidateRallyPoint(point);
                bool changed = result != Result || !preview.enabled;
                Result = result; preview.enabled = true;
                preview.transform.position = new Vector3(point.x, point.y, -.5f);
                var color = Result == RallyPointResult.Available ? new Color(.25f, 1f, .8f) : new Color(1f, .25f, .25f);
                preview.startColor = preview.endColor = color;
                properties.SetColor(colorId, color); preview.SetPropertyBlock(properties);
                if (changed) Changed?.Invoke();
            }
            if (input.SelectPressed && recruitment.TrySetRallyPoint(point) == RallyPointResult.Available) Cancel();
        }
    }
}
