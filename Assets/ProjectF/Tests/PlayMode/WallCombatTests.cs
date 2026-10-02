using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class WallCombatTests
    {
        private WallConstruction construction;
        private EstateStockpile stockpile;
        private PortalInvasion invasion;
        private NavigationWorld2D navigation;
        private UnitCombat enemy, ally;
        private EnemyCombatAI ai;
        private readonly List<ScriptableObject> temporarySettings = new List<ScriptableObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            construction = Object.FindAnyObjectByType<WallConstruction>();
            stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            var allies = Object.FindObjectsByType<UnitCombat>().OrderBy(u => u.name).ToArray();
            ally = allies[0];
            foreach (var other in allies.Skip(1)) other.gameObject.SetActive(false);
            Place(ally, new Vector2(-16, -10));
            GameObject.Find("Navigation Obstacles").SetActive(false);
            var prefab = GetField<UnitCombat>(invasion, "enemyPrefab");
            enemy = Object.Instantiate(prefab, new Vector3(3, 0, 0), Quaternion.identity);
            enemy.GetComponent<CommandableUnit>().InitializeNavigation(navigation);
            enemy.gameObject.SetActive(true);
            ai = enemy.GetComponent<EnemyCombatAI>(); ai.enabled = false;
            Sync();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            foreach (var settings in temporarySettings) Object.Destroy(settings);
            temporarySettings.Clear();
        }
        private static T GetField<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private T Settings<T>(object target, string field, string json) where T : ScriptableObject
        {
            var clone = Object.Instantiate(GetField<T>(target, field));
            temporarySettings.Add(clone); JsonUtility.FromJsonOverwrite(json, clone); SetField(target, field, clone);
            return clone;
        }
        private static void Place(UnitCombat unit, Vector2 point)
        {
            unit.transform.position = point; unit.GetComponent<Rigidbody2D>().position = point;
        }
        private void Sync() { Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty(); }
        private WallStructure Build(Vector2 point)
        {
            Assert.That(construction.TryBuild(point), Is.EqualTo(PlacementResult.Available));
            return construction.FindDemolitionTarget(point);
        }
        private IEnumerator Until(Func<bool> condition, string message, float seconds = 6)
        {
            float until = Time.time + seconds;
            while (!condition() && Time.time < until) yield return new WaitForFixedUpdate();
            Assert.That(condition(), Is.True, message);
        }
        private void StartAdvance(Vector2 destination)
        {
            ai.enabled = true;
            Assert.That(ai.AdvanceTo(destination), Is.True);
        }

        [UnityTest]
        public IEnumerator NewWallUsesHealthSnapshotAndUpdatesItsBar()
        {
            var settings = Settings<ConstructionSettings>(construction, "settings", "{\"wallMaxHealth\":60}");
            var wall = Build(new Vector2(-1, 0));
            var fill = wall.transform.Find("Wall Health Bar/Health Fill");
            Assert.That(wall.MaxHealth, Is.EqualTo(60));
            Assert.That(wall.Health, Is.EqualTo(60));
            Assert.That(fill.localScale.x, Is.EqualTo(1));
            JsonUtility.FromJsonOverwrite("{\"wallMaxHealth\":1000}", settings);
            wall.ReceiveDamage(10, enemy);
            Assert.That(wall.MaxHealth, Is.EqualTo(60));
            Assert.That(wall.Health, Is.EqualTo(50));
            Assert.That(fill.localScale.x, Is.EqualTo(50f / 60).Within(.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FriendlyDeadSourceAndNonPositiveDamageAreRejected()
        {
            var wall = Build(new Vector2(-1, 0));
            Assert.That(ally.AttackWall(wall), Is.False);
            wall.ReceiveDamage(10, ally); wall.ReceiveDamage(0); wall.ReceiveDamage(-10);
            enemy.ReceiveDamage(int.MaxValue);
            wall.ReceiveDamage(10, enemy);
            Assert.That(wall.Health, Is.EqualTo(wall.MaxHealth));
            wall.enabled = false; wall.ReceiveDamage(10);
            Assert.That(wall.Health, Is.EqualTo(wall.MaxHealth));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestructionRemovesCollisionOnceWithoutRefundEvenDuringCallbacks()
        {
            var wall = Build(new Vector2(-1, 0));
            int deaths = 0; DemolitionResult demolition = DemolitionResult.Available;
            wall.Died += () => { deaths++; wall.ReceiveDamage(1); };
            wall.HealthChanged += () => { if (wall.Health == 0) demolition = construction.TryDemolish(wall); };
            wall.ReceiveDamage(int.MaxValue, enemy);
            wall.ReceiveDamage(int.MaxValue);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(demolition, Is.EqualTo(DemolitionResult.NoWall));
            Assert.That(wall.IsAlive, Is.False);
            Assert.That(wall.gameObject.activeSelf, Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            yield return null;
            Assert.That(Object.FindObjectsByType<WallStructure>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RepeatedWallOrdersAndSwitchingToUnitShareWeaponCooldown()
        {
            var wall = Build(new Vector2(-1, 0));
            Settings<CombatSettings>(enemy, "settings", "{\"attackInterval\":0.6}");
            Place(enemy, Vector2.zero); Place(ally, new Vector2(1, 0)); Sync();
            Assert.That(enemy.AttackWall(wall), Is.True);
            yield return Until(() => wall.Health < wall.MaxHealth, "No first wall strike.");
            float until = Time.time + .2f;
            while (Time.time < until) { enemy.AttackWall(wall); yield return new WaitForFixedUpdate(); }
            Assert.That(wall.Health, Is.EqualTo(50));
            Assert.That(enemy.Attack(ally), Is.True);
            Assert.That(enemy.WallTarget, Is.Null);
            yield return new WaitForSeconds(.2f);
            Assert.That(ally.Health, Is.EqualTo(ally.MaxHealth));
            yield return Until(() => ally.Health < ally.MaxHealth, "Unit strike did not resume after shared cooldown.", 1);
        }

        [UnityTest]
        public IEnumerator WallAttackRequiresReachAndClearLineOfSight()
        {
            var wall = Build(new Vector2(-1, 0));
            Settings<LordMovementSettings>(enemy.Unit, "settings", "{\"moveSpeed\":0}");
            Assert.That(enemy.AttackWall(wall), Is.True);
            yield return new WaitForSeconds(.3f);
            Assert.That(wall.Health, Is.EqualTo(60), "Out-of-range strike hit the wall.");
            Place(enemy, Vector2.zero);
            var blocker = new GameObject("Solid shield", typeof(BoxCollider2D));
            blocker.transform.position = new Vector2(-.3f, 0);
            blocker.GetComponent<BoxCollider2D>().size = new Vector2(.1f, 3);
            Sync();
            yield return new WaitForSeconds(.3f);
            Assert.That(wall.Health, Is.EqualTo(60), "Strike passed through another solid obstacle.");
            blocker.SetActive(false); Sync();
            yield return Until(() => wall.Health == 50, "Clear in-range strike failed.");
        }

        [UnityTest]
        public IEnumerator DisabledOrDestroyedWallAndGroundOrdersCancelPursuit()
        {
            var wall = Build(new Vector2(-1, 0));
            enemy.AttackWall(wall); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(enemy.Unit.HasDestination, Is.True);
            wall.enabled = false;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(enemy.WallTarget, Is.Null); Assert.That(enemy.Unit.HasDestination, Is.False);
            wall.enabled = true;
            enemy.AttackWall(wall); enemy.Unit.MoveTo(new Vector2(5, 4));
            Assert.That(enemy.WallTarget, Is.Null); Assert.That(enemy.Unit.HasDestination, Is.True);
            enemy.AttackWall(wall); Object.Destroy(wall.gameObject);
            yield return null; yield return new WaitForFixedUpdate();
            Assert.That(enemy.WallTarget == null, Is.True); Assert.That(enemy.Unit.HasDestination, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseDisableAndDeathStopWallDamage()
        {
            var wall = Build(new Vector2(-1, 0));
            Place(enemy, Vector2.zero); Sync(); enemy.AttackWall(wall);
            yield return Until(() => wall.Health == 50, "No initial strike.");
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
            Assert.That(wall.Health, Is.EqualTo(50));
            Time.timeScale = 1; enemy.enabled = false;
            yield return new WaitForSeconds(1.1f);
            Assert.That(wall.Health, Is.EqualTo(50));
            enemy.enabled = true; enemy.AttackWall(wall); enemy.ReceiveDamage(int.MaxValue);
            yield return new WaitForSeconds(.2f);
            Assert.That(wall.Health, Is.EqualTo(50));
        }

        [UnityTest]
        public IEnumerator AdvancingEnemyBreaksForwardWallAndResumesOriginalDestination()
        {
            Settings<ConstructionSettings>(construction, "settings", "{\"wallMaxHealth\":20}");
            for (int y = -2; y <= 2; y++) Build(new Vector2(-1, y));
            var wall = construction.FindDemolitionTarget(new Vector2(-1, 0));
            Settings<CombatSettings>(enemy, "settings", "{\"attackInterval\":0.3}");
            StartAdvance(new Vector2(-6, 0));
            yield return Until(() => ai.State == EnemyAIState.Breaching, "No automatic wall acquisition.");
            Assert.That(enemy.Target, Is.Null);
            Assert.That(enemy.transform.Find("AI Status").GetComponent<TextMesh>().text, Is.EqualTo("BREAK WALL"));
            yield return Until(() => wall == null, "Wall was not destroyed.");
            Assert.That(stockpile.Wood, Is.EqualTo(30));
            yield return Until(() => ai.State == EnemyAIState.Guarding && enemy.Unit.Position.x < -5.5f, "Advance did not resume through the breach.");
        }

        [UnityTest]
        public IEnumerator VisibleDefenderInterruptsWallAttack()
        {
            Build(new Vector2(-1, 0));
            StartAdvance(new Vector2(-6, 0));
            yield return Until(() => ai.State == EnemyAIState.Breaching, "No wall engagement.");
            Place(ally, enemy.Unit.Position + Vector2.up * 2); Sync();
            yield return Until(() => ai.State == EnemyAIState.Engaging, "Visible defender did not take priority.");
            Assert.That(enemy.Target, Is.EqualTo(ally));
            Assert.That(enemy.WallTarget, Is.Null);
        }

        [UnityTest]
        public IEnumerator NearbyOffRouteWallIsIgnoredAndGuardsDoNotBesiege()
        {
            var wall = Build(new Vector2(-1, 2));
            ai.enabled = true;
            yield return new WaitForSeconds(.4f);
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Guarding));
            Assert.That(enemy.WallTarget, Is.Null);
            ai.AdvanceTo(new Vector2(-6, 0));
            yield return Until(() => ai.State == EnemyAIState.Guarding, "Enemy did not follow the open path.");
            Assert.That(wall.Health, Is.EqualTo(60));
        }

        [UnityTest]
        public IEnumerator IndestructibleObstacleHidesWallAndStalledBreachBacksOff()
        {
            var wall = Build(new Vector2(-1, 0));
            Settings<LordMovementSettings>(enemy.Unit, "settings", "{\"moveSpeed\":0}");
            Settings<EnemyAISettings>(ai, "settings", "{\"thinkInterval\":0.1,\"stalledPursuitTimeout\":0.5,\"wallRetryDelay\":2}");
            var blocker = new GameObject("Indestructible barrier", typeof(BoxCollider2D));
            blocker.transform.position = new Vector2(1, 0); blocker.GetComponent<BoxCollider2D>().size = new Vector2(.2f, 5);
            Sync(); StartAdvance(new Vector2(-6, 0));
            yield return new WaitForSeconds(.4f);
            Assert.That(enemy.WallTarget, Is.Null);
            blocker.SetActive(false); Sync();
            yield return Until(() => ai.State == EnemyAIState.Breaching, "Exposed wall was not acquired.");
            yield return Until(() => ai.State == EnemyAIState.Advancing, "Stalled wall pursuit did not yield.", 2);
            yield return new WaitForSeconds(.5f);
            Assert.That(enemy.WallTarget, Is.Null, "Failed wall was immediately reacquired.");
            Assert.That(wall.Health, Is.EqualTo(60));
        }

        [UnityTest]
        public IEnumerator LosingWallOrDisablingAICancelsWallOrder()
        {
            var wall = Build(new Vector2(-1, 0));
            StartAdvance(new Vector2(-6, 0));
            yield return Until(() => ai.State == EnemyAIState.Breaching, "No breach started.");
            wall.gameObject.SetActive(false); Sync();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Advancing));
            Assert.That(enemy.WallTarget, Is.Null);
            wall.gameObject.SetActive(true); Place(enemy, new Vector2(3, 0)); Sync(); ai.AdvanceTo(new Vector2(-6, 0));
            yield return Until(() => ai.State == EnemyAIState.Breaching, "Re-enabled wall was not detected.");
            ai.enabled = false;
            Assert.That(enemy.WallTarget, Is.Null); Assert.That(enemy.Unit.HasDestination, Is.False);
        }

        [UnityTest]
        public IEnumerator TwoAttackersDestroyOneWallWithoutDoubleDeathOrRefund()
        {
            var wall = Build(new Vector2(-1, 0)); int deaths = 0;
            wall.Died += () => deaths++;
            var prefab = GetField<UnitCombat>(invasion, "enemyPrefab");
            var second = Object.Instantiate(prefab, new Vector3(-1, 1.5f, 0), Quaternion.identity);
            second.GetComponent<CommandableUnit>().InitializeNavigation(navigation); second.gameObject.SetActive(true);
            second.GetComponent<EnemyCombatAI>().enabled = false;
            Place(enemy, Vector2.zero); Sync();
            enemy.AttackWall(wall); second.AttackWall(wall);
            yield return Until(() => wall == null, "Combined strikes did not break wall.", 5);
            Assert.That(deaths, Is.EqualTo(1)); Assert.That(stockpile.Wood, Is.EqualTo(70));
            yield return new WaitForFixedUpdate();
            Assert.That(enemy.WallTarget == null && second.WallTarget == null, Is.True);
        }

        [UnityTest]
        public IEnumerator RealInvasionBreachesEnclosedDefenderAndStopsOnDefeat()
        {
            Object.Destroy(enemy.gameObject);
            Place(ally, new Vector2(-4, 0)); Sync();
            Settings<ConstructionSettings>(construction, "settings", "{\"wallMaxHealth\":20}");
            for (int x = -5; x <= -3; x++) for (int y = -1; y <= 1; y++)
                if (x != -4 || y != 0) Build(new Vector2(x, y));
            Assert.That(stockpile.Wood, Is.Zero);
            Assert.That(invasion.Begin(), Is.True);
            yield return Until(() => Object.FindObjectsByType<WallStructure>().Length < 8, "Real wave did not breach the enclosed defender.", 14);
            yield return Until(() => ally.Health < ally.MaxHealth, "No unit attack after breach.", 6);
            ally.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Defeated, "No defeat result.", 2);
            var health = Object.FindObjectsByType<WallStructure>().ToDictionary(w => w, w => w.Health);
            yield return new WaitForSeconds(1.2f);
            foreach (var pair in health) Assert.That(pair.Key != null && pair.Key.Health == pair.Value, Is.True, "Wall damage continued after result.");
            Assert.That(stockpile.Wood, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReloadResetsWallHealthAndConstructionState()
        {
            var wall = Build(new Vector2(-1, 0)); wall.ReceiveDamage(30);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindObjectsByType<WallStructure>(), Is.Empty);
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
            var builder = Object.FindAnyObjectByType<WallConstruction>();
            Assert.That(builder.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            var fresh = builder.FindDemolitionTarget(new Vector2(-1, 0));
            Assert.That(fresh.Health, Is.EqualTo(60));
        }
    }
}
