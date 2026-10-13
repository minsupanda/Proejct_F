using System.Collections.Generic;
using ProjectF.Player;
using ProjectF.Combat;
using ProjectF.Economy;
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
        private readonly HashSet<CommandableUnit> selectedSet = new HashSet<CommandableUnit>();
        // Scene-owned groups survive temporary input/controller disable; invalid members are pruned on recall.
        private readonly List<CommandableUnit>[] groups = new List<CommandableUnit>[CommandInput.ControlGroupCount];
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<CommandableUnit> selectable = new List<CommandableUnit>();
        private readonly Collider2D[] resourceHits = new Collider2D[16];
        private readonly ContactFilter2D resourceFilter = new ContactFilter2D { useTriggers = false };
        private NavigationWorld2D navigation;
        private PointerEventData pointerData;
        private EventSystem pointerEventSystem;
        private Vector2 start;
        private bool selecting, dragging, additiveSelection;
        private readonly HashSet<object> pointerOwners = new HashSet<object>();
        private bool waitForPointerRelease;
        public IReadOnlyList<CommandableUnit> Selected => selected;
        public bool IsDragging => dragging;
        private void Awake()
        {
            navigation = GetComponent<NavigationWorld2D>();
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<CommandableUnit>();
        }

        /// <summary>Modal tools own pointer clicks until released; cancellation never leaks a world order.</summary>
        public void SetPointerCommandsBlocked(object owner, bool blocked)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (blocked) { pointerOwners.Add(owner); CancelDrag(); }
            else if (pointerOwners.Remove(owner)) waitForPointerRelease = true;
        }

        public bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            if (pointerData == null || pointerEventSystem != EventSystem.current)
            {
                pointerEventSystem = EventSystem.current;
                pointerData = new PointerEventData(pointerEventSystem);
            }
            pointerData.Reset();
            pointerData.position = input.Pointer;
            uiHits.Clear();
            pointerEventSystem.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }

        private void Update()
        {
            for (int i = selected.Count - 1; i >= 0; i--)
                if (!CanSelect(selected[i]))
                {
                    if (selected[i] != null) selected[i].SetSelected(false);
                    selectedSet.Remove(selected[i]);
                    selected.RemoveAt(i);
                }
            if (pointerOwners.Count > 0 || waitForPointerRelease)
            {
                CancelDrag();
                if (pointerOwners.Count == 0 && !input.SelectHeld && !input.CommandHeld) waitForPointerRelease = false;
                return;
            }
            if (!input.Ready || input.PanHeld || input.CancelPressed)
            {
                CancelDrag();
                return;
            }
            Vector2 pointer = input.Pointer;
            bool insideView = worldCamera.pixelRect.Contains(pointer);
            if (input.StopPressed && insideView && !selecting && !input.SelectHeld
                && Time.timeScale > 0 && !PointerOverUI())
            {
                foreach (var unit in selected) unit.StopCommand();
                // Stop wins over a right-click received in the same frame.
                return;
            }
            int group = input.GroupPressed;
            if (group >= 0 && insideView && !selecting && !input.SelectHeld
                && Time.timeScale > 0 && !PointerOverUI())
            {
                if (input.StoreGroupHeld) StoreGroup(group);
                else RecallGroup(group);
                // Selection shortcuts consume the frame before any pointer order can use it.
                return;
            }
            if (input.SelectPressed && insideView && !PointerOverUI())
            {
                start = pointer;
                // Fix the gesture mode at mouse-down, so releasing Shift mid-drag is predictable.
                additiveSelection = input.AdditiveSelectionHeld;
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
            if (input.CommandPressed && insideView && !selecting && Time.timeScale > 0 && !PointerOverUI())
            {
                if (input.AttackMoveHeld) IssueMove(worldCamera.ScreenToWorldPoint(pointer), true);
                else IssueCommand(worldCamera.ScreenToWorldPoint(pointer));
            }
        }

        private void Select(Vector2 end)
        {
            if (!additiveSelection) ClearSelection();
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
            if (!dragging && closest != null)
            {
                if (additiveSelection && selectedSet.Remove(closest))
                {
                    selected.Remove(closest);
                    closest.SetSelected(false);
                }
                else AddSelected(closest);
            }
        }

        private void AddSelected(CommandableUnit unit)
        {
            if (!selectedSet.Add(unit)) return;
            selected.Add(unit);
            unit.SetSelected(true);
        }

        private void ClearSelection()
        {
            foreach (var unit in selected) if (unit != null) unit.SetSelected(false);
            selected.Clear();
            selectedSet.Clear();
        }

        private void StoreGroup(int slot)
        {
            var members = groups[slot];
            members.Clear();
            members.AddRange(selected);
        }

        private void RecallGroup(int slot)
        {
            ClearSelection();
            var members = groups[slot];
            for (int i = members.Count - 1; i >= 0; i--)
                if (!CanSelect(members[i])) members.RemoveAt(i);
            foreach (var unit in members) AddSelected(unit);
        }

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
            if (target == null)
            {
                int count = Physics2D.OverlapPoint(point, resourceFilter, resourceHits);
                for (int i = 0; i < count; i++)
                {
                    if (!resourceHits[i].TryGetComponent<WoodResourceNode>(out var node) || !node.IsAvailable) continue;
                    foreach (var unit in selected)
                        if (unit.TryGetComponent<WoodGatherer>(out var gatherer)) gatherer.Gather(node);
                    return;
                }
                IssueMove(point); return;
            }
            foreach (var unit in selected)
                if (unit.TryGetComponent<UnitCombat>(out var combat)) combat.Attack(target);
        }

        private void IssueMove(Vector2 target, bool attackMove = false)
        {
            if (selected.Count == 0) return;
            target.x = Mathf.Clamp(target.x, movementBounds.xMin, movementBounds.xMax);
            target.y = Mathf.Clamp(target.y, movementBounds.yMin, movementBounds.yMax);
            // Everyone shares the clicked gathering point. Nearby units claim inner spaces
            // first; navigation allocates non-overlapping arrival positions outwards.
            selected.Sort((a, b) => (a.Position - target).sqrMagnitude.CompareTo((b.Position - target).sqrMagnitude));
            foreach (var unit in selected)
            {
                if (attackMove)
                {
                    if (unit.TryGetComponent<AttackMoveOrder>(out var advance)) advance.Begin(target);
                }
                else unit.MoveTo(target);
            }
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
            selecting = dragging = additiveSelection = false;
            if (selectionBox != null) selectionBox.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            CancelDrag();
            ClearSelection();
        }
    }
}
