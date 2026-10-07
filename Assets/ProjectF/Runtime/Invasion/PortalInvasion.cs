using System;
using System.Collections.Generic;
using ProjectF.Combat;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Invasion
{
    public enum InvasionState { Ready, Preparing, Spawning, Fighting, Repelled, Defeated, Cancelled }

    /// <summary>Scene-owned invasion rounds. Owns progression, spawn timing and members; AI owns their orders.</summary>
    public sealed class PortalInvasion : MonoBehaviour
    {
        [SerializeField] private InvasionSettings settings;
        [SerializeField] private UnitCombat enemyPrefab;
        [SerializeField] private NavigationWorld2D navigation;
        [SerializeField] private Transform[] exits;
        [SerializeField] private Transform destination;
        [SerializeField] private UnitCombat[] defenders;
        private readonly List<UnitCombat> members = new List<UnitCombat>(8);
        private readonly List<UnitCombat> reinforcements = new List<UnitCombat>(8);
        private readonly List<Collider2D> overlaps = new List<Collider2D>(16);
        private readonly ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        private float nextSpawn, nextMemberCheck;
        private int nextExit, total;
        private bool initialized;
        private bool seriesStarted;
        private int plannedWaves, firstWaveUnits, additionalUnits;
        public event Action Changed;
        public InvasionState State { get; private set; } = InvasionState.Ready;
        public int SpawnedCount { get; private set; }
        public int ActiveCount => members.Count;
        public int TotalCount => initialized ? total : settings != null ? settings.UnitCount : 0;
        public int WaveNumber { get; private set; } = 1;
        public int WaveCount => seriesStarted ? plannedWaves : settings != null ? settings.WaveCount : 1;
        public bool CanPrepareNextWave => initialized && isActiveAndEnabled && State == InvasionState.Repelled && WaveNumber < WaveCount;
        public bool ExitBlocked { get; private set; }
        public float PreparationRemaining => State == InvasionState.Preparing ? Mathf.Max(0, nextSpawn - Time.time) : 0;
        public IReadOnlyList<UnitCombat> Members => members;
        public int DefenderCount
        {
            get
            {
                int count = 0;
                if (defenders != null) foreach (var defender in defenders) if (defender != null && defender.IsAlive) count++;
                foreach (var defender in reinforcements) if (defender != null && defender.IsAlive) count++;
                return count;
            }
        }
        internal void RegisterDefender(UnitCombat defender)
        {
            if (defender == null || !defender.IsAlive || defender.Faction != UnitFaction.Player || defender.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Only a live allied unit in this scene can join the defense.", nameof(defender));
            if (Array.IndexOf(defenders, defender) >= 0 || reinforcements.Contains(defender)) return;
            for (int i = reinforcements.Count - 1; i >= 0; i--)
                if (reinforcements[i] == null || !reinforcements[i].IsAlive) reinforcements.RemoveAt(i);
            reinforcements.Add(defender);
            Changed?.Invoke();
        }

        private void Start()
        {
            if (settings == null || enemyPrefab == null || enemyPrefab.gameObject.activeSelf
                || enemyPrefab.Faction != UnitFaction.Hostile || enemyPrefab.GetComponent<EnemyCombatAI>() == null
                || navigation == null || destination == null || exits == null || exits.Length == 0
                || Array.Exists(exits, e => e == null) || defenders == null || defenders.Length == 0
                || Array.Exists(defenders, d => d == null || d.Faction != UnitFaction.Player))
            {
                Debug.LogError("Portal invasion needs an inactive hostile prefab, navigation, exits, destination and player defenders.", this);
                enabled = false;
                return;
            }
            total = settings.UnitCount;
            initialized = true;
            Changed?.Invoke();
        }

        public bool Begin()
        {
            if (!initialized || !isActiveAndEnabled || State != InvasionState.Ready || Time.timeScale <= 0) return false;
            if (!seriesStarted)
            {
                // Lock the round plan at first launch; asset edits never alter a running series.
                plannedWaves = settings.WaveCount;
                firstWaveUnits = settings.UnitCount;
                additionalUnits = settings.AdditionalUnitsPerWave;
                total = firstWaveUnits;
                seriesStarted = true;
            }
            nextSpawn = Time.time + settings.PreparationSeconds;
            nextMemberCheck = Time.time;
            SetState(HasDefenders() ? InvasionState.Preparing : InvasionState.Defeated);
            return State == InvasionState.Preparing;
        }

        /// <summary>Return to preparation in the same scene, preserving survivors and estate resources.</summary>
        public bool PrepareNextWave()
        {
            if (!CanPrepareNextWave || Time.timeScale <= 0 || members.Count != 0) return false;
            if (!HasDefenders()) { Finish(InvasionState.Defeated); return false; }
            WaveNumber++;
            total = Mathf.Clamp(firstWaveUnits + additionalUnits * (WaveNumber - 1), 1, 32);
            SpawnedCount = 0;
            nextExit = 0;
            nextSpawn = nextMemberCheck = 0;
            ExitBlocked = false;
            SetState(InvasionState.Ready);
            return true;
        }

        private void Update()
        {
            if (!initialized || Time.timeScale <= 0 || State == InvasionState.Ready || IsFinished) return;
            if (Time.time >= nextMemberCheck)
            {
                nextMemberCheck = Time.time + .25f;
                RemoveLostMembers();
                if (!HasDefenders()) { Finish(InvasionState.Defeated); return; }
            }
            if ((State == InvasionState.Preparing || State == InvasionState.Spawning) && Time.time >= nextSpawn)
            {
                if (TrySpawn())
                {
                    ExitBlocked = false;
                    SpawnedCount++;
                    nextSpawn = Time.time + settings.SpawnInterval;
                    SetState(SpawnedCount >= total ? InvasionState.Fighting : InvasionState.Spawning);
                }
                else
                {
                    nextSpawn = Time.time + settings.BlockedRetryInterval;
                    if (!ExitBlocked) { ExitBlocked = true; Changed?.Invoke(); }
                }
            }
            if (SpawnedCount == total && members.Count == 0) Finish(InvasionState.Repelled);
        }

        private bool IsFinished => State == InvasionState.Repelled || State == InvasionState.Defeated || State == InvasionState.Cancelled;

        private bool HasDefenders() => DefenderCount > 0;

        private bool TrySpawn()
        {
            var circle = enemyPrefab.GetComponent<CircleCollider2D>();
            float radius = circle.radius * Mathf.Max(Mathf.Abs(enemyPrefab.transform.localScale.x), Mathf.Abs(enemyPrefab.transform.localScale.y));
            // Fresh physics positions matter when several exits spawn during the same frame.
            Physics2D.SyncTransforms();
            for (int i = 0; i < exits.Length; i++)
            {
                int index = (nextExit + i) % exits.Length;
                var exit = exits[index];
                if (exit == null) continue;
                overlaps.Clear();
                Physics2D.OverlapCircle(exit.position, radius + .1f, filter, overlaps);
                if (overlaps.Count != 0) continue;
                var member = Instantiate(enemyPrefab, exit.position, Quaternion.identity, transform);
                member.name = "Portal Raider " + WaveNumber + "-" + (SpawnedCount + 1);
                member.GetComponent<CommandableUnit>().InitializeNavigation(navigation);
                members.Add(member);
                member.Died += OnMemberDied;
                member.gameObject.SetActive(true);
                member.GetComponent<EnemyCombatAI>().AdvanceTo(destination.position);
                nextExit = (index + 1) % exits.Length;
                return true;
            }
            return false;
        }

        private void OnMemberDied() => RemoveLostMembers();

        private void RemoveLostMembers()
        {
            bool changed = false;
            for (int i = members.Count - 1; i >= 0; i--)
            {
                var member = members[i];
                if (member != null && member.IsAlive) continue;
                if (member != null) { member.Died -= OnMemberDied; Destroy(member.gameObject); }
                members.RemoveAt(i);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        private void Finish(InvasionState state)
        {
            foreach (var member in members)
            {
                if (member == null) continue;
                member.GetComponent<EnemyCombatAI>().enabled = false;
                member.CancelAttack();
            }
            ExitBlocked = false;
            SetState(state);
        }

        private void SetState(InvasionState state) { State = state; Changed?.Invoke(); }

        private void OnDisable()
        {
            foreach (var member in members)
            {
                if (member == null) continue;
                member.Died -= OnMemberDied;
                member.gameObject.SetActive(false);
                Destroy(member.gameObject);
            }
            members.Clear();
            ExitBlocked = false;
            if (initialized && (!IsFinished || (State == InvasionState.Repelled && WaveNumber < WaveCount)))
                SetState(InvasionState.Cancelled);
        }
    }
}
