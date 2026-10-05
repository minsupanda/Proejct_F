using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Input;
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
    public sealed class WoodGatheringTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private WoodGatherer worker;
        private WoodResourceNode tree;
        private EstateStockpile stockpile;
        private PortalInvasion invasion;
        private WoodGatheringSettings settings;
        private UnitCombat ally;
        private Camera camera;
        private LordMovementSettings stoppedMovement;
        private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Field(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            worker = Object.FindObjectsByType<WoodGatherer>().OrderBy(g => g.name).First();
            ally = worker.GetComponent<UnitCombat>();
            foreach (var other in Object.FindObjectsByType<WoodGatherer>()) if (other != worker) other.gameObject.SetActive(false);
            tree = Object.FindObjectsByType<WoodResourceNode>().OrderBy(n => n.name).First();
            stockpile = Object.FindAnyObjectByType<EstateStockpile>(); invasion = Object.FindAnyObjectByType<PortalInvasion>(); camera = Camera.main;
            settings = Object.Instantiate(Field<WoodGatheringSettings>(worker, "settings"));
            JsonUtility.FromJsonOverwrite("{\"cycleSeconds\":0.2,\"stalledSeconds\":1}", settings); Field(worker, "settings", settings);
            Place((Vector2)tree.transform.position + Vector2.right * 1.25f);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); if (stoppedMovement != null) Object.Destroy(stoppedMovement); fixture.TearDown();
        }
        private void Place(Vector2 point)
        {
            ally.transform.position = point; ally.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms();
        }
        private IEnumerator Pointer(Vector2 screen, int buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)buttons });
            yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 world, int buttons = 1)
        {
            var p = (Vector2)camera.WorldToScreenPoint(world); yield return Pointer(p); yield return Pointer(p, buttons); yield return Pointer(p);
        }
        private IEnumerator Until(System.Func<bool> predicate, float seconds = 5)
        {
            float end = Time.time + seconds;
            while (!predicate() && Time.time < end) yield return null;
            Assert.That(predicate(), Is.True, "Timed out waiting for gathering outcome.");
        }

        [UnityTest]
        public IEnumerator FreshSceneShowsAllDepositsBeforeAnyGathering()
        {
            foreach (var node in Object.FindObjectsByType<WoodResourceNode>())
            {
                Assert.That(node.Remaining, Is.EqualTo(40));
                Assert.That(node.GetComponentInChildren<TextMesh>().text, Is.EqualTo("WOOD 40"));
                Assert.That(node.transform.Find("Foliage").gameObject.activeSelf, Is.True);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator MouseGatherMovesAndFundsAdditionalConstruction()
        {
            Place((Vector2)tree.transform.position + Vector2.up * 3);
            Assert.That(stockpile.TrySpendWood(80), Is.True);
            yield return Click(ally.Unit.Position); yield return Click(tree.transform.position, 2);
            Assert.That(worker.Target, Is.EqualTo(tree));
            yield return Until(() => stockpile.Wood >= 10);
            Assert.That(Vector2.Distance(ally.Unit.Position, tree.ClosestPoint(ally.Unit.Position)), Is.LessThan(1.3f));
            worker.Cancel(); int before = stockpile.Wood;
            Assert.That(Object.FindAnyObjectByType<WallConstruction>().TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            Assert.That(stockpile.Wood, Is.EqualTo(before - 10));
        }
        [UnityTest]
        public IEnumerator FiniteNodeTransfersExactRemainderAndOpensGround()
        {
            Field(settings, "woodPerCycle", 17);
            Assert.That(worker.Gather(tree), Is.True);
            yield return Until(() => tree.Remaining == 0);
            Assert.That(stockpile.Wood, Is.EqualTo(120));
            Assert.That(tree.GetComponent<BoxCollider2D>().enabled, Is.False);
            Assert.That(tree.GetComponentInChildren<TextMesh>().text, Is.EqualTo("DEPLETED"));
            Assert.That(worker.Target, Is.Null); Assert.That(worker.Gather(tree), Is.False);
            Assert.That(Object.FindAnyObjectByType<WallConstruction>().TryBuild(tree.transform.position), Is.EqualTo(PlacementResult.Available));
        }
        [UnityTest]
        public IEnumerator RepeatOrdersCannotSkipCycleAndRetargetRequiresNewWork()
        {
            Field(settings, "cycleSeconds", .6f);
            worker.Gather(tree);
            float until = Time.time + .35f;
            while (Time.time < until) { worker.Gather(tree); yield return null; }
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            yield return Until(() => stockpile.Wood == 85);
            worker.Cancel(); worker.Gather(tree);
            yield return new WaitForSeconds(.3f);
            Assert.That(stockpile.Wood, Is.EqualTo(85));
        }
        [UnityTest]
        public IEnumerator GroundOrderCancelsWithoutOverwritingNewDestination()
        {
            worker.Gather(tree); yield return Until(() => stockpile.Wood > 80);
            int before = stockpile.Wood;
            ally.Unit.MoveTo(new Vector2(-3, 0));
            Assert.That(worker.Target, Is.Null); Assert.That(ally.Unit.RequestedDestination, Is.EqualTo(new Vector2(-3, 0)));
            yield return new WaitForSeconds(.4f); Assert.That(stockpile.Wood, Is.EqualTo(before));
        }
        [UnityTest]
        public IEnumerator AttackOrderCancelsGatheringAndGatherOrderCancelsAttack()
        {
            var prefab = Field<UnitCombat>(invasion, "enemyPrefab");
            var enemy = Object.Instantiate(prefab, new Vector3(10, 5, 0), Quaternion.identity);
            enemy.GetComponent<CommandableUnit>().InitializeNavigation(Object.FindAnyObjectByType<NavigationWorld2D>());
            enemy.gameObject.SetActive(true); enemy.GetComponent<EnemyCombatAI>().enabled = false;
            worker.Gather(tree); Assert.That(ally.Attack(enemy), Is.True); Assert.That(worker.Target, Is.Null);
            Assert.That(ally.Target, Is.EqualTo(enemy)); Assert.That(worker.Gather(tree), Is.True); Assert.That(ally.Target, Is.Null);
            Object.Destroy(enemy.gameObject); yield return null;
        }
        [UnityTest]
        public IEnumerator InvasionImmediatelyStopsWorkAndRejectsNewOrders()
        {
            worker.Gather(tree); yield return Until(() => stockpile.Wood > 80);
            int before = stockpile.Wood; Assert.That(invasion.Begin(), Is.True);
            Assert.That(worker.Target, Is.Null); Assert.That(worker.Gather(tree), Is.False);
            yield return new WaitForSeconds(.4f); Assert.That(stockpile.Wood, Is.EqualTo(before));
        }
        [UnityTest]
        public IEnumerator PauseDoesNotAccumulateAndDisableOrDeathCancels()
        {
            worker.Gather(tree); Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.35f); Assert.That(stockpile.Wood, Is.EqualTo(80));
            Time.timeScale = 1; yield return Until(() => stockpile.Wood > 80);
            worker.enabled = false; int before = stockpile.Wood;
            yield return new WaitForSeconds(.3f); Assert.That(stockpile.Wood, Is.EqualTo(before));
            worker.enabled = true; Assert.That(worker.Target, Is.Null);
            worker.Gather(tree); ally.ReceiveDamage(int.MaxValue);
            Assert.That(worker.Target, Is.Null); yield return new WaitForSeconds(.3f); Assert.That(stockpile.Wood, Is.EqualTo(before));
        }
        [UnityTest]
        public IEnumerator DisabledOrDestroyedNodeStopsWithoutPaying()
        {
            worker.Gather(tree); tree.gameObject.SetActive(false);
            yield return new WaitForSeconds(.3f); Assert.That(worker.Target, Is.Null); Assert.That(stockpile.Wood, Is.EqualTo(80));
            tree.gameObject.SetActive(true); worker.Gather(tree); Object.Destroy(tree.gameObject);
            yield return new WaitForSeconds(.3f); Assert.That(worker.Target, Is.Null); Assert.That(stockpile.Wood, Is.EqualTo(80));
        }
        [UnityTest]
        public IEnumerator FullStockpileDoesNotConsumeDepositAndResumesAfterSpending()
        {
            Assert.That(stockpile.TryAddWood(int.MaxValue - 80), Is.True); worker.Gather(tree);
            yield return new WaitForSeconds(.35f);
            Assert.That(worker.State, Is.EqualTo(GatheringState.StorageFull)); Assert.That(tree.Remaining, Is.EqualTo(40));
            stockpile.TrySpendWood(5); yield return Until(() => tree.Remaining == 35);
            Assert.That(stockpile.Wood, Is.EqualTo(int.MaxValue));
        }
        [UnityTest]
        public IEnumerator SolidBarrierPreventsGatheringThroughWall()
        {
            var barrier = new GameObject("Gathering Test Barrier", typeof(BoxCollider2D));
            barrier.transform.position = tree.transform.position + Vector3.right * .7f;
            barrier.GetComponent<BoxCollider2D>().size = new Vector2(.1f, 10);
            Physics2D.SyncTransforms(); Object.FindAnyObjectByType<NavigationWorld2D>().MarkObstaclesDirty();
            worker.Gather(tree); yield return new WaitForSeconds(.7f);
            Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(tree.Remaining, Is.EqualTo(40));
            Object.Destroy(barrier); yield return null;
        }
        [UnityTest]
        public IEnumerator MultipleGatherersCannotOverdrawLastWood()
        {
            var other = Object.FindObjectsByType<WoodGatherer>(FindObjectsInactive.Include).First(g => g != worker);
            other.transform.position = tree.transform.position + Vector3.left * 1.25f;
            other.GetComponent<Rigidbody2D>().position = other.transform.position;
            Field(other, "settings", settings); other.gameObject.SetActive(true); Physics2D.SyncTransforms();
            Field(settings, "woodPerCycle", 17); worker.Gather(tree); other.Gather(tree);
            yield return Until(() => tree.Remaining == 0); yield return new WaitForSeconds(.3f);
            Assert.That(stockpile.Wood, Is.EqualTo(120)); Assert.That(worker.Target, Is.Null); Assert.That(other.Target, Is.Null);
        }
        [UnityTest]
        public IEnumerator BuildingToolAndUIClickDoNotIssueGatherOrders()
        {
            yield return Click(ally.Unit.Position);
            var placement = Object.FindAnyObjectByType<WallPlacementController>(); placement.Toggle();
            yield return Click(tree.transform.position, 2); Assert.That(worker.Target, Is.Null);
            var button = Object.FindAnyObjectByType<ConstructionHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            var screen = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            yield return Pointer(screen, 2); yield return Pointer(screen);
            Assert.That(worker.Target, Is.Null); Assert.That(ally.Unit.HasDestination, Is.False);
        }
        [UnityTest]
        public IEnumerator StalledApproachStopsAndCanBeRetried()
        {
            Place((Vector2)tree.transform.position + Vector2.up * 3);
            stoppedMovement = Object.Instantiate(Field<LordMovementSettings>(ally.Unit, "settings"));
            Field(stoppedMovement, "moveSpeed", 0f); Field(ally.Unit, "settings", stoppedMovement);
            worker.Gather(tree); yield return Until(() => worker.State == GatheringState.Blocked, 2);
            Assert.That(worker.Target, Is.Null); Assert.That(ally.Unit.HasDestination, Is.False); Assert.That(stockpile.Wood, Is.EqualTo(80));
            Field(stoppedMovement, "moveSpeed", 5f); Assert.That(worker.Gather(tree), Is.True);
            yield return Until(() => stockpile.Wood > 80);
        }
        [UnityTest]
        public IEnumerator ReloadResetsWoodDepositsOrdersAndStockpile()
        {
            worker.Gather(tree); yield return Until(() => stockpile.Wood > 80);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
            Assert.That(Object.FindObjectsByType<WoodResourceNode>().All(n => n.Remaining == 40), Is.True);
            Assert.That(Object.FindObjectsByType<WoodGatherer>().All(g => g.Target == null && g.State == GatheringState.Idle), Is.True);
        }
    }
}
