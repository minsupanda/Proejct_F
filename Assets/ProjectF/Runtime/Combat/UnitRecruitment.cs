using System;
using System.Collections.Generic;
using ProjectF.Economy;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Combat
{
    public enum RecruitmentState { Idle, Training, Suspended, ExitBlocked, AtCapacity }
    public enum RallyPointResult { Available, Unavailable, OutsideMap, Blocked }

    /// <summary>One paid training slot. Scene dependencies are bound before a recruit is activated.</summary>
    public sealed class UnitRecruitment : MonoBehaviour
    {
        [SerializeField] private RecruitmentSettings settings;
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private PortalInvasion invasion;
        [SerializeField] private NavigationWorld2D navigation;
        [SerializeField] private UnitCombat soldierPrefab;
        [SerializeField] private Transform[] exits;
        private readonly Collider2D[] overlaps = new Collider2D[16];
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private readonly List<UnitCombat> recruits = new List<UnitCombat>(8);
        private bool initialized, starting;
        private float nextExitCheck, nextRosterCheck;
        private int lastCount, serial;
        public event Action Changed;
        public RecruitmentSettings Settings => settings;
        public RecruitmentState State { get; private set; }
        public bool IsTraining { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool HasRallyPoint { get; private set; }
        public Vector2 RallyPoint { get; private set; }
        public int AlliedCount => invasion != null ? invasion.DefenderCount : 0;
        public bool PreparationAvailable => initialized && isActiveAndEnabled && invasion != null
            && invasion.isActiveAndEnabled && invasion.State == InvasionState.Ready && Time.timeScale > 0;
        public bool CanTrain => PreparationAvailable && !starting && !IsTraining && stockpile.isActiveAndEnabled
            && AlliedCount < settings.MaximumAllies && stockpile.Wood >= settings.WoodCost;

        private void OnEnable()
        {
            if (invasion != null) invasion.Changed += OnContextChanged;
            if (stockpile != null) stockpile.Changed += OnContextChanged;
            if (initialized) OnContextChanged();
        }
        private void OnDisable()
        {
            if (invasion != null) invasion.Changed -= OnContextChanged;
            if (stockpile != null) stockpile.Changed -= OnContextChanged;
            if (IsTraining) State = RecruitmentState.Suspended;
            Changed?.Invoke();
        }
        private void Start()
        {
            if (settings == null || stockpile == null || invasion == null || navigation == null
                || soldierPrefab == null || soldierPrefab.gameObject.activeSelf || soldierPrefab.Faction != UnitFaction.Player
                || soldierPrefab.GetComponent<WoodGatherer>() == null || exits == null || exits.Length == 0
                || Array.Exists(exits, e => e == null))
            {
                Debug.LogError("Recruitment requires settings, estate, invasion, navigation, an inactive player soldier and exits.", this);
                enabled = false; return;
            }
            initialized = true; lastCount = AlliedCount; Changed?.Invoke();
        }
        public bool TryTrain()
        {
            if (!CanTrain) return false;
            starting = true;
            // Set the slot before spending: resource listeners cannot queue a second purchase.
            IsTraining = true; RemainingSeconds = settings.TrainingSeconds; nextExitCheck = 0;
            bool paid;
            try { paid = stockpile.TrySpendWood(settings.WoodCost); }
            finally { starting = false; }
            if (!paid) { IsTraining = false; RemainingSeconds = 0; }
            OnContextChanged();
            return paid;
        }
        public RallyPointResult ValidateRallyPoint(Vector2 point)
        {
            if (!PreparationAvailable || !navigation.isActiveAndEnabled) return RallyPointResult.Unavailable;
            float radius = SoldierRadius;
            Rect bounds = navigation.MovementBounds;
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y)
                || point.x < bounds.xMin + radius || point.x > bounds.xMax - radius
                || point.y < bounds.yMin + radius || point.y > bounds.yMax - radius)
                return RallyPointResult.OutsideMap;
            Physics2D.SyncTransforms();
            int count = Physics2D.OverlapCircle(point, radius + .1f, filter, overlaps);
            if (count == overlaps.Length) return RallyPointResult.Blocked;
            // Units share a rally point; navigation assigns separate arrival positions.
            for (int i = 0; i < count; i++)
                if (overlaps[i].GetComponentInParent<CommandableUnit>() == null) return RallyPointResult.Blocked;
            return RallyPointResult.Available;
        }
        public RallyPointResult TrySetRallyPoint(Vector2 point)
        {
            var result = ValidateRallyPoint(point);
            if (result != RallyPointResult.Available) return result;
            if (HasRallyPoint && RallyPoint == point) return result;
            RallyPoint = point; HasRallyPoint = true; Changed?.Invoke();
            return result;
        }
        public bool ClearRallyPoint()
        {
            if (!PreparationAvailable || !HasRallyPoint) return false;
            HasRallyPoint = false; RallyPoint = Vector2.zero; Changed?.Invoke(); return true;
        }
        private float SoldierRadius
        {
            get
            {
                var circle = soldierPrefab.GetComponent<CircleCollider2D>();
                return circle.radius * Mathf.Max(Mathf.Abs(soldierPrefab.transform.localScale.x), Mathf.Abs(soldierPrefab.transform.localScale.y));
            }
        }
        private void OnContextChanged()
        {
            if (IsTraining && !PreparationAvailable) State = RecruitmentState.Suspended;
            Changed?.Invoke();
        }
        private void Update()
        {
            if (!initialized) return;
            if (Time.unscaledTime >= nextRosterCheck)
            {
                nextRosterCheck = Time.unscaledTime + .25f;
                int count = AlliedCount;
                if (lastCount != count) { lastCount = count; Changed?.Invoke(); }
            }
            if (!IsTraining) return;
            if (!PreparationAvailable) { SetState(RecruitmentState.Suspended); return; }
            if (RemainingSeconds > 0)
            {
                RemainingSeconds = Mathf.Max(0, RemainingSeconds - Time.deltaTime);
                SetState(RecruitmentState.Training);
                if (RemainingSeconds > 0) return;
            }
            if (AlliedCount >= settings.MaximumAllies) { SetState(RecruitmentState.AtCapacity); return; }
            if (Time.time < nextExitCheck) return;
            nextExitCheck = Time.time + .5f;
            if (!TryDeploy()) SetState(RecruitmentState.ExitBlocked);
        }
        private bool TryDeploy()
        {
            float radius = SoldierRadius;
            Physics2D.SyncTransforms();
            foreach (var exit in exits)
            {
                if (exit == null || Physics2D.OverlapCircle(exit.position, radius + .1f, filter, overlaps) > 0) continue;
                var recruit = Instantiate(soldierPrefab, exit.position, Quaternion.identity, transform);
                recruit.name = "Soldier " + (++serial);
                recruit.GetComponent<CommandableUnit>().InitializeNavigation(navigation);
                recruit.GetComponent<WoodGatherer>().InitializeEstate(stockpile, invasion);
                recruit.Died += RemoveDeadRecruits;
                recruits.Add(recruit);
                recruit.gameObject.SetActive(true);
                // Issue once at deployment. Later edits affect only future recruits, never player orders.
                if (HasRallyPoint) recruit.Unit.MoveTo(RallyPoint);
                IsTraining = false; RemainingSeconds = 0; State = RecruitmentState.Idle;
                invasion.RegisterDefender(recruit);
                lastCount = AlliedCount;
                Changed?.Invoke();
                return true;
            }
            return false;
        }
        private void RemoveDeadRecruits()
        {
            for (int i = recruits.Count - 1; i >= 0; i--)
            {
                var recruit = recruits[i];
                if (recruit != null && recruit.IsAlive) continue;
                if (recruit != null) { recruit.Died -= RemoveDeadRecruits; Destroy(recruit.gameObject); }
                recruits.RemoveAt(i);
            }
            Changed?.Invoke();
        }
        private void OnDestroy()
        {
            foreach (var recruit in recruits) if (recruit != null) recruit.Died -= RemoveDeadRecruits;
        }
        private void SetState(RecruitmentState value)
        {
            if (State == value) return;
            State = value; Changed?.Invoke();
        }
    }
}
