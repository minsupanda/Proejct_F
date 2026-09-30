using System.Collections.Generic;
using ProjectF.Player;
using ProjectF.Combat;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectF.Input
{
    [RequireComponent(typeof(NavigationWorld2D))]
    public sealed class UnitCommandController : MonoBehaviour
    {
        [SerializeField] private CommandInput input;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform selectionBox;
        [SerializeField, Min(1)] private float dragThreshold = 8f;
        [SerializeField] private Rect movementBounds = new Rect(-23, -15, 46, 30);
        private readonly List<CommandableUnit> selected = new List<CommandableUnit>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<CommandableUnit> selectable = new List<CommandableUnit>();
        private NavigationWorld2D navigation;
        private Vector2 start;
        private bool selecting, dragging;
        public IReadOnlyList<CommandableUnit> Selected => selected;
        public bool IsDragging => dragging;
        private void Awake() => navigation = GetComponent<NavigationWorld2D>();

        public bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = input.Pointer };
            uiHits.Clear();
            EventSystem.current.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        private void Update()
        {
            for (int i = selected.Count - 1; i >= 0; i--)
                if (!CanSelect(selected[i]))
                {
                    if (selected[i] != null) selected[i].SetSelected(false);
                    selected.RemoveAt(i);
                }
            if (!input.Ready || input.PanHeld)
            {
                CancelDrag();
                return;
            }
            Vector2 pointer = input.Pointer;
            bool insideView = worldCamera.pixelRect.Contains(pointer);
            if (input.SelectPressed && insideView && !PointerOverUI())
            {
                start = pointer;
                selecting = true;
            }
            if (selecting)
            {
                dragging |= (pointer - start).sqrMagnitude >= dragThreshold * dragThreshold;
                if (dragging) DrawBox(pointer);
                if (input.SelectReleased)
                {
                    if (insideView && !PointerOverUI()) Select(pointer);
                    CancelDrag();
                }
                else if (!input.SelectHeld) CancelDrag();
            }
            if (input.CommandPressed && insideView && !selecting && !PointerOverUI())
                IssueCommand(worldCamera.ScreenToWorldPoint(pointer));
        }

        private void Select(Vector2 end)
        {
            foreach (var unit in selected) unit.SetSelected(false);
            selected.Clear();
            Rect rect = Rect.MinMaxRect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
            CommandableUnit closest = null;
            float bestDistance = float.PositiveInfinity;
            Vector2 world = worldCamera.ScreenToWorldPoint(end);
            navigation.CopyUnitsTo(selectable);
            foreach (var unit in selectable)
            {
                if (!CanSelect(unit)) continue;
                Vector3 screen = worldCamera.WorldToScreenPoint(unit.transform.position);
                if (screen.z <= 0) continue;
                if (dragging)
                {
                    if (rect.Contains(screen, true)) AddSelected(unit);
                }
                else if (unit.GetComponent<Collider2D>().OverlapPoint(world))
                {
                    float distance = ((Vector2)screen - end).sqrMagnitude;
                    if (distance < bestDistance) { closest = unit; bestDistance = distance; }
                }
            }
            if (!dragging && closest != null) AddSelected(closest);
        }

        private void AddSelected(CommandableUnit unit) { selected.Add(unit); unit.SetSelected(true); }

        private static bool CanSelect(CommandableUnit unit)
        {
            if (unit == null || !unit.isActiveAndEnabled) return false;
            return !unit.TryGetComponent<UnitCombat>(out var combat) || combat.IsPlayerControlled;
        }

        private void IssueCommand(Vector2 point)
        {
            if (selected.Count == 0) return;
            navigation.CopyUnitsTo(selectable);
            UnitCombat target = null;
            float closest = float.PositiveInfinity;
            foreach (var candidate in selectable)
            {
                if (candidate == null || !candidate.TryGetComponent<UnitCombat>(out var combat)
                    || !combat.IsAlive || combat.Faction == UnitFaction.Player
                    || !candidate.GetComponent<Collider2D>().OverlapPoint(point)) continue;
                float distance = (candidate.Position - point).sqrMagnitude;
                if (distance < closest) { closest = distance; target = combat; }
            }
            if (target == null) { IssueMove(point); return; }
            foreach (var unit in selected)
                if (unit.TryGetComponent<UnitCombat>(out var combat)) combat.Attack(target);
        }

        private void IssueMove(Vector2 target)
        {
            if (selected.Count == 0) return;
            target.x = Mathf.Clamp(target.x, movementBounds.xMin, movementBounds.xMax);
            target.y = Mathf.Clamp(target.y, movementBounds.yMin, movementBounds.yMax);
            // Everyone shares the clicked gathering point. Nearby units claim inner spaces
            // first; navigation allocates non-overlapping arrival positions outwards.
            selected.Sort((a, b) => (a.Position - target).sqrMagnitude.CompareTo((b.Position - target).sqrMagnitude));
            foreach (var unit in selected) unit.MoveTo(target);
        }

        private void DrawBox(Vector2 end)
        {
            if (selectionBox == null) return;
            selectionBox.gameObject.SetActive(true);
            var parent = (RectTransform)selectionBox.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, start, null, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, end, null, out var b);
            selectionBox.anchoredPosition = (a + b) * .5f;
            selectionBox.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        private void CancelDrag()
        {
            selecting = dragging = false;
            if (selectionBox != null) selectionBox.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            CancelDrag();
            foreach (var unit in selected) if (unit != null) unit.SetSelected(false);
            selected.Clear();
        }
    }
}
