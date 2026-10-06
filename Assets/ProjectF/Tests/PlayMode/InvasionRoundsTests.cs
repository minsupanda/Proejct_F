using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class InvasionRoundsTests
    {
        private PortalInvasion invasion;
        private InvasionSettings settings;
        private UnitCombat[] allies;
        private InputTestFixture fixture;
        private Mouse mouse;
        private WoodGatheringSettings gatheringSettings;
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            settings = Object.Instantiate(Get<InvasionSettings>(invasion, "settings"));
            JsonUtility.FromJsonOverwrite("{\"waveCount\":3,\"unitCount\":3,\"additionalUnitsPerWave\":1,\"preparationSeconds\":0.1,\"spawnInterval\":0.1}", settings);
            Set(invasion, "settings", settings);
            allies = Object.FindObjectsByType<UnitCombat>().Where(c => c.Faction == UnitFaction.Player).OrderBy(c => c.name).ToArray();
            for (int i = 0; i < allies.Length; i++) Place(allies[i], new Vector2(-18 + i, -10));
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); if (gatheringSettings != null) Object.Destroy(gatheringSettings); fixture.TearDown();
        }
        private void Place(UnitCombat unit, Vector2 point)
        {
            unit.transform.position = point; unit.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms();
        }
        private IEnumerator Until(Func<bool> predicate, float seconds = 5)
        {
            float end = Time.time + seconds;
            while (!predicate() && Time.time < end) yield return null;
            Assert.That(predicate(), Is.True, "Round progression timed out.");
        }
        private IEnumerator Repel()
        {
            Assert.That(invasion.Begin(), Is.True);
            yield return Until(() => invasion.SpawnedCount == invasion.TotalCount);
            foreach (var member in invasion.Members.ToArray()) member.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
            yield return null;
        }
        private IEnumerator ClickAction()
        {
            var button = Object.FindAnyObjectByType<InvasionHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            var p = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p }); yield return null; yield return null;
        }
        private string ActionLabel() => Object.FindAnyObjectByType<InvasionHUD>().GetComponentInChildren<UnityEngine.UI.Button>().GetComponentInChildren<UnityEngine.UI.Text>().text;

        [UnityTest]
        public IEnumerator ThreeRoundsSpawnThreeFourFiveAndRequireExplicitPreparationAndStart()
        {
            var original = invasion;
            for (int round = 1; round <= 3; round++)
            {
                Assert.That(invasion.WaveNumber, Is.EqualTo(round)); Assert.That(invasion.TotalCount, Is.EqualTo(round + 2));
                yield return Repel();
                Assert.That(invasion.SpawnedCount, Is.EqualTo(round + 2)); Assert.That(invasion.ActiveCount, Is.Zero);
                Assert.That(invasion.Begin(), Is.False);
                yield return new WaitForSeconds(.25f); Assert.That(invasion.State, Is.EqualTo(InvasionState.Repelled));
                if (round == 3) break;
                Assert.That(invasion.PrepareNextWave(), Is.True); Assert.That(invasion, Is.SameAs(original));
                Assert.That(invasion.SpawnedCount, Is.Zero); Assert.That(invasion.PrepareNextWave(), Is.False);
                yield return new WaitForSeconds(.25f); Assert.That(invasion.State, Is.EqualTo(InvasionState.Ready));
            }
            Assert.That(invasion.CanPrepareNextWave, Is.False); Assert.That(invasion.PrepareNextWave(), Is.False);
            Assert.That(ActionLabel(), Is.EqualTo("RESTART"));
        }
        [UnityTest]
        public IEnumerator NextRoundKeepsDamagedSurvivorsWallsWoodAndDepletedResourceState()
        {
            var construction = Object.FindAnyObjectByType<WallConstruction>(); var stock = Object.FindAnyObjectByType<EstateStockpile>();
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            var wall = Object.FindAnyObjectByType<WallStructure>(); wall.ReceiveDamage(15);
            allies[0].ReceiveDamage(7); allies[3].ReceiveDamage(int.MaxValue);
            var worker = allies[0].GetComponent<WoodGatherer>(); var tree = Object.FindObjectsByType<WoodResourceNode>().OrderBy(n => n.name).First();
            gatheringSettings = Object.Instantiate(Get<WoodGatheringSettings>(worker, "settings"));
            Set(gatheringSettings, "cycleSeconds", .1f); Set(worker, "settings", gatheringSettings);
            Place(allies[0], (Vector2)tree.transform.position + Vector2.right * 1.25f);
            worker.Gather(tree); yield return Until(() => stock.Wood > 70); worker.Cancel();
            int wood = stock.Wood, remaining = tree.Remaining, health = allies[0].Health;
            Place(allies[0], new Vector2(-18, -10));
            yield return Repel(); Assert.That(construction.CanConstruct, Is.False); Assert.That(worker.CanGather, Is.False);
            invasion.PrepareNextWave();
            Assert.That(construction.CanConstruct, Is.True); Assert.That(worker.CanGather, Is.True);
            Assert.That(stock.Wood, Is.EqualTo(wood)); Assert.That(tree.Remaining, Is.EqualTo(remaining));
            Assert.That(wall.Health, Is.EqualTo(45)); Assert.That(allies[0].Health, Is.EqualTo(health)); Assert.That(allies[3].IsAlive, Is.False);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Available));
            Assert.That(stock.Wood, Is.EqualTo(wood + 10));
        }
        [UnityTest]
        public IEnumerator ActualNextRoundButtonPreservesSceneAndAllowsConstructionThenLocksOnStart()
        {
            yield return Repel(); Assert.That(ActionLabel(), Is.EqualTo("NEXT ROUND")); var old = invasion;
            yield return ClickAction();
            Assert.That(invasion, Is.SameAs(old)); Assert.That(invasion.WaveNumber, Is.EqualTo(2));
            Assert.That(invasion.State, Is.EqualTo(InvasionState.Ready)); Assert.That(ActionLabel(), Is.EqualTo("START INVASION"));
            Assert.That(Object.FindAnyObjectByType<ProjectF.Input.UnitCommandController>().Selected, Is.Empty);
            var construction = Object.FindAnyObjectByType<WallConstruction>();
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            yield return ClickAction(); Assert.That(construction.CanConstruct, Is.False);
            yield return Until(() => invasion.SpawnedCount == 4);
        }
        [UnityTest]
        public IEnumerator ProgressionRejectsReadyActivePausedAndDuplicateRequests()
        {
            Assert.That(invasion.PrepareNextWave(), Is.False); invasion.Begin(); Assert.That(invasion.PrepareNextWave(), Is.False);
            yield return Until(() => invasion.SpawnedCount == 3);
            foreach (var member in invasion.Members.ToArray()) member.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
            Time.timeScale = 0; Assert.That(invasion.PrepareNextWave(), Is.False);
            yield return ClickAction(); Assert.That(invasion.WaveNumber, Is.EqualTo(1));
            Time.timeScale = 1; Assert.That(invasion.PrepareNextWave(), Is.True); Assert.That(invasion.PrepareNextWave(), Is.False);
            Assert.That(invasion.WaveNumber, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator LosingAllSurvivorsBetweenRoundsCannotResume()
        {
            yield return Repel(); foreach (var ally in allies) ally.ReceiveDamage(int.MaxValue);
            Assert.That(invasion.PrepareNextWave(), Is.False); Assert.That(invasion.State, Is.EqualTo(InvasionState.Defeated));
            Assert.That(invasion.Begin(), Is.False); Assert.That(ActionLabel(), Is.EqualTo("RESTART"));
        }
        [UnityTest]
        public IEnumerator DefeatOnSecondRoundStopsWaveAndDisallowsFurtherProgression()
        {
            yield return Repel(); invasion.PrepareNextWave(); invasion.Begin();
            yield return Until(() => invasion.ActiveCount > 0);
            foreach (var ally in allies) ally.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Defeated);
            int spawned = invasion.SpawnedCount;
            yield return new WaitForSeconds(.4f);
            Assert.That(invasion.SpawnedCount, Is.EqualTo(spawned)); Assert.That(invasion.PrepareNextWave(), Is.False);
            Assert.That(invasion.Members.All(m => m.Target == null && !m.Unit.HasDestination), Is.True);
        }
        [UnityTest]
        public IEnumerator DisabledIntermissionCancelsSeriesAndDoesNotUnlockOnEnable()
        {
            yield return Repel(); invasion.enabled = false; invasion.enabled = true;
            Assert.That(invasion.State, Is.EqualTo(InvasionState.Cancelled)); Assert.That(invasion.PrepareNextWave(), Is.False);
            Assert.That(invasion.Begin(), Is.False);
        }
        [UnityTest]
        public IEnumerator FirstLaunchSnapshotsPlanAndLaterRoundCountsAreCapped()
        {
            Set(settings, "unitCount", 1); Set(settings, "waveCount", 2); Set(settings, "additionalUnitsPerWave", 32);
            yield return Repel();
            Set(settings, "unitCount", 32); Set(settings, "waveCount", 10); Set(settings, "additionalUnitsPerWave", 0);
            Assert.That(invasion.WaveCount, Is.EqualTo(2)); invasion.PrepareNextWave();
            Assert.That(invasion.TotalCount, Is.EqualTo(32)); Assert.That(invasion.WaveNumber, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator FinalVictoryButtonReloadsEntireSeriesAndEstate()
        {
            Set(settings, "unitCount", 1); Set(settings, "waveCount", 2); Set(settings, "additionalUnitsPerWave", 0);
            Object.FindAnyObjectByType<EstateStockpile>().TrySpendWood(30); allies[0].ReceiveDamage(12);
            yield return Repel(); invasion.PrepareNextWave(); yield return Repel();
            Assert.That(ActionLabel(), Is.EqualTo("RESTART")); var old = invasion;
            yield return ClickAction(); yield return Until(() => old == null);
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            Assert.That(invasion.WaveNumber, Is.EqualTo(1)); Assert.That(invasion.State, Is.EqualTo(InvasionState.Ready));
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
            Assert.That(Object.FindObjectsByType<UnitCombat>().All(u => u.Health == u.MaxHealth), Is.True);
        }
        [UnityTest]
        public IEnumerator ZeroIncrementKeepsWaveSizeAndSingleWaveStillEndsNormally()
        {
            Set(settings, "unitCount", 1); Set(settings, "waveCount", 2); Set(settings, "additionalUnitsPerWave", 0);
            yield return Repel(); invasion.PrepareNextWave(); Assert.That(invasion.TotalCount, Is.EqualTo(1));
            yield return Repel(); Assert.That(invasion.CanPrepareNextWave, Is.False);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); invasion = Object.FindAnyObjectByType<PortalInvasion>();
            Set(settings, "waveCount", 1); Set(invasion, "settings", settings); yield return null;
            yield return Repel(); Assert.That(invasion.WaveCount, Is.EqualTo(1)); Assert.That(invasion.PrepareNextWave(), Is.False);
        }
    }
}
