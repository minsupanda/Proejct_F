using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
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
    public sealed class InvasionTests
    {
        private PortalInvasion invasion;
        private InvasionSettings settings;
        private UnitCombat[] allies;
        private NavigationWorld2D navigation;
        private InputTestFixture fixture;
        private Mouse mouse;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            allies = Object.FindObjectsByType<UnitCombat>().Where(c => c.Faction == UnitFaction.Player).OrderBy(c => c.name).ToArray();
            settings = ScriptableObject.CreateInstance<InvasionSettings>();
            Set(settings, "preparationSeconds", .1f);
            Set(settings, "spawnInterval", .2f);
            Set(invasion, "settings", settings);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings);
            fixture.TearDown();
        }

        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private IEnumerator Until(Func<bool> condition, string message, float timeout = 8)
        {
            float until = Time.time + timeout;
            while (!condition() && Time.time < until) yield return new WaitForFixedUpdate();
            Assert.That(condition(), Is.True, message);
        }
        private IEnumerator SpawnAll()
        {
            Assert.That(invasion.Begin(), Is.True);
            yield return Until(() => invasion.SpawnedCount == 3, "Wave did not spawn exactly three units.");
        }
        private void Place(UnitCombat unit, Vector2 position)
        {
            unit.transform.position = position;
            unit.GetComponent<Rigidbody2D>().position = position;
            Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty();
        }
        private void HideAllies()
        {
            for (int i = 0; i < allies.Length; i++) Place(allies[i], new Vector2(-19 + i * 1.5f, -11));
            GameObject.Find("Navigation Obstacles").SetActive(false);
            navigation.MarkObstaclesDirty();
        }

        [UnityTest]
        public IEnumerator ReadyWaitsForUserAndDuplicateStartCannotCreateAnotherWave()
        {
            yield return new WaitForSeconds(.3f);
            Assert.That(invasion.State, Is.EqualTo(InvasionState.Ready));
            Assert.That(invasion.ActiveCount, Is.Zero);
            Assert.That(allies.Length, Is.EqualTo(4));
            Assert.That(Object.FindObjectsByType<UnitCombat>().Length, Is.EqualTo(4));
            yield return SpawnAll();
            Assert.That(invasion.Begin(), Is.False);
            yield return new WaitForSeconds(.5f);
            Assert.That(invasion.SpawnedCount, Is.EqualTo(3));
            Assert.That(invasion.ActiveCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator SpawnedPrefabHasIndependentHealthNavigationAndAdvanceOrder()
        {
            HideAllies();
            yield return SpawnAll();
            var members = invasion.Members.ToArray();
            var registered = new System.Collections.Generic.List<CommandableUnit>();
            navigation.CopyUnitsTo(registered);
            foreach (var member in members)
            {
                Assert.That(member.Health, Is.EqualTo(member.MaxHealth));
                Assert.That(registered, Does.Contain(member.Unit));
                Assert.That(member.GetComponent<EnemyCombatAI>().State, Is.EqualTo(EnemyAIState.Advancing));
            }
            members[0].ReceiveDamage(7);
            Assert.That(members[1].Health, Is.EqualTo(members[1].MaxHealth));
            yield return Until(() => members.All(m => m.Unit.Position.x < 3), "Spawned raiders never moved towards the allies.");
            for (int i = 0; i < members.Length; i++)
                for (int j = i + 1; j < members.Length; j++)
                    Assert.That(Vector2.Distance(members[i].Unit.Position, members[j].Unit.Position), Is.GreaterThan(.7f));
        }

        [UnityTest]
        public IEnumerator AdvancingRaiderAcquiresTargetThenResumesAfterTargetDies()
        {
            HideAllies();
            yield return SpawnAll();
            var enemy = invasion.Members[0];
            var ai = enemy.GetComponent<EnemyCombatAI>();
            Place(allies[0], enemy.Unit.Position + Vector2.left * 3);
            yield return Until(() => enemy.Target == allies[0], "Advancing raider did not detect opponent.");
            allies[0].ReceiveDamage(int.MaxValue);
            yield return Until(() => ai.State == EnemyAIState.Advancing, "Lost target did not resume advance.");
            yield return Until(() => ai.State == EnemyAIState.Guarding, "Raider failed to reach destination.");
            Assert.That(Vector2.Distance(enemy.Unit.Position, new Vector2(-4, 0)), Is.LessThanOrEqualTo(2f));
            Assert.That(enemy.Unit.HasDestination, Is.False);
        }

        [UnityTest]
        public IEnumerator RaidersSharingDestinationSettleIntoFreeArrivalPositions()
        {
            HideAllies();
            yield return SpawnAll();
            var members = invasion.Members.ToArray();
            yield return Until(() => members.All(m => m.GetComponent<EnemyCombatAI>().State == EnemyAIState.Guarding),
                "Raiders kept retrying an occupied rally point instead of accepting their arrival slots.");
            var orders = members.Select(m => m.Unit.CommandOrder).ToArray();
            yield return new WaitForSeconds(1.2f);
            for (int i = 0; i < members.Length; i++)
            {
                Assert.That(members[i].Unit.HasDestination, Is.False);
                Assert.That(members[i].Unit.CommandOrder, Is.EqualTo(orders[i]));
                Assert.That(Vector2.Distance(members[i].Unit.Position, new Vector2(-4, 0)), Is.LessThanOrEqualTo(2f));
            }
        }

        [UnityTest]
        public IEnumerator RealWaveReachesAndDamagesAlliesWithoutPlayerAttackOrder()
        {
            yield return SpawnAll();
            yield return Until(() => allies.Any(a => a.Health < a.MaxHealth), "Raiders never attacked the defending units.", 12);
        }

        [UnityTest]
        public IEnumerator BlockedExitsWaitWithoutConsumingWaveThenRecover()
        {
            var wall = new GameObject("Blocked portal exits", typeof(BoxCollider2D));
            wall.transform.position = Get<Transform[]>(invasion, "exits")[1].position;
            wall.GetComponent<BoxCollider2D>().size = new Vector2(4, 7);
            Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty();
            invasion.Begin();
            yield return Until(() => invasion.ExitBlocked, "Blocked exit was not reported.");
            yield return new WaitForSeconds(.7f);
            Assert.That(invasion.SpawnedCount, Is.Zero);
            Assert.That(invasion.ActiveCount, Is.Zero);
            Object.Destroy(wall);
            yield return null;
            navigation.MarkObstaclesDirty();
            yield return Until(() => invasion.SpawnedCount == 3, "Portal did not recover when unblocked.");
            Assert.That(invasion.ExitBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseSuspendsPreparationAndSpawnTiming()
        {
            Set(settings, "preparationSeconds", .5f);
            invasion.Begin();
            float remaining = invasion.PreparationRemaining;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(invasion.SpawnedCount, Is.Zero);
            Assert.That(invasion.PreparationRemaining, Is.EqualTo(remaining).Within(.03f));
            Time.timeScale = 1;
            yield return Until(() => invasion.SpawnedCount > 0, "Wave did not resume.");
        }

        [UnityTest]
        public IEnumerator KillingEarlySpawnDoesNotWinBeforeRemainingMonstersAppear()
        {
            Set(settings, "spawnInterval", .5f);
            invasion.Begin();
            yield return Until(() => invasion.SpawnedCount == 1, "First monster missing.");
            invasion.Members[0].ReceiveDamage(int.MaxValue);
            yield return null;
            Assert.That(invasion.State, Is.EqualTo(InvasionState.Spawning));
            Assert.That(invasion.ActiveCount, Is.Zero);
            yield return Until(() => invasion.SpawnedCount == 3, "Remaining monsters missing.");
            foreach (var member in invasion.Members.ToArray()) member.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled, "Last kill did not complete wave.");
            Assert.That(invasion.Begin(), Is.False);
            var registered = new System.Collections.Generic.List<CommandableUnit>();
            navigation.CopyUnitsTo(registered);
            Assert.That(registered.Count, Is.EqualTo(4), "Dead spawned units left navigation registrations.");
        }

        [UnityTest]
        public IEnumerator AllDefendersLostStopsSpawnsAndExistingOrders()
        {
            Set(settings, "spawnInterval", 2f);
            invasion.Begin();
            yield return Until(() => invasion.ActiveCount == 1, "No first member.");
            var enemy = invasion.Members[0];
            foreach (var ally in allies) ally.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Defeated, "Defeat not reported.");
            Assert.That(enemy.Unit.HasDestination, Is.False);
            Assert.That(enemy.Target, Is.Null);
            yield return new WaitForSeconds(.5f);
            Assert.That(invasion.SpawnedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableCleansSpawnedMembersAndCannotRestartCancelledWave()
        {
            yield return SpawnAll();
            var members = invasion.Members.ToArray();
            invasion.enabled = false;
            yield return null;
            Assert.That(invasion.State, Is.EqualTo(InvasionState.Cancelled));
            Assert.That(members.All(m => m == null), Is.True);
            invasion.enabled = true;
            Assert.That(invasion.Begin(), Is.False);
            Assert.That(invasion.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ExternalRemovalIsPrunedAndDoesNotLeaveWaveStuck()
        {
            yield return SpawnAll();
            foreach (var member in invasion.Members.ToArray()) Object.Destroy(member.gameObject);
            yield return Until(() => invasion.State == InvasionState.Repelled, "Removed members left the wave stuck.");
            Assert.That(invasion.ActiveCount, Is.Zero);
        }

        private IEnumerator ClickButton()
        {
            var button = Object.FindAnyObjectByType<InvasionHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            Vector2 pointer = RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = 1 });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator ActualUIButtonStartsAndRestartsWithoutSelectingWorldUnits()
        {
            yield return ClickButton();
            Assert.That(invasion.State, Is.Not.EqualTo(InvasionState.Ready));
            Assert.That(Object.FindAnyObjectByType<ProjectF.Input.UnitCommandController>().Selected, Is.Empty);
            yield return Until(() => invasion.SpawnedCount == 3, "Button did not start spawning.");
            foreach (var member in invasion.Members.ToArray()) member.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled, "Wave incomplete.");
            var old = invasion;
            yield return ClickButton();
            yield return Until(() => old == null, "Restart did not reload the scene.");
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            Assert.That(invasion.State, Is.EqualTo(InvasionState.Ready));
            Assert.That(invasion.ActiveCount, Is.Zero);
            Assert.That(Object.FindObjectsByType<UnitCombat>().All(c => c.Health == c.MaxHealth), Is.True);
        }
    }
}
