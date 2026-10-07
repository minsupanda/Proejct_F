using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Input;
using ProjectF.Invasion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class WallRepairTests
    {
        private WallConstruction construction;
        private WallPlacementController placement;
        private EstateStockpile stock;
        private PortalInvasion invasion;
        private ConstructionSettings settings;
        private InvasionSettings invasionSettings;
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            construction = Object.FindAnyObjectByType<WallConstruction>(); placement = Object.FindAnyObjectByType<WallPlacementController>();
            stock = Object.FindAnyObjectByType<EstateStockpile>(); invasion = Object.FindAnyObjectByType<PortalInvasion>();
            settings = Object.Instantiate(construction.Settings); Set(construction, "settings", settings);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); if (invasionSettings != null) Object.Destroy(invasionSettings); fixture.TearDown();
        }
        private WallStructure Build(Vector2? point = null)
        {
            Vector2 p = point ?? new Vector2(-1, 0);
            Assert.That(construction.TryBuild(p), Is.EqualTo(PlacementResult.Available)); return construction.FindDemolitionTarget(p);
        }
        private IEnumerator Until(Func<bool> predicate, float seconds = 5)
        {
            float end = Time.time + seconds;
            while (!predicate() && Time.time < end) yield return null;
            Assert.That(predicate(), Is.True, "Repair test timed out.");
        }
        private IEnumerator Pointer(Vector2 position, ushort buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = buttons }); yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 position, ushort buttons = 1)
        {
            yield return Pointer(position); yield return Pointer(position, buttons); yield return Pointer(position);
        }
        private IEnumerator Button(string name)
        {
            var b = Object.FindAnyObjectByType<ConstructionHUD>().transform.Find(name).GetComponent<RectTransform>();
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, b.position));
        }
        private string Hint() => Object.FindAnyObjectByType<ConstructionHUD>().transform.Find("Status").GetComponent<UnityEngine.UI.Text>().text;

        [UnityTest]
        public IEnumerator HalfDamagedWallCostsFiveAndRepairsInPlaceWithOneHealthEvent()
        {
            var wall = Build(); wall.ReceiveDamage(30); var position = wall.transform.position; var collider = wall.GetComponent<BoxCollider2D>();
            int changes = 0; wall.HealthChanged += () => changes++;
            Assert.That(construction.ValidateRepair(wall, out int cost), Is.EqualTo(RepairResult.Available)); Assert.That(cost, Is.EqualTo(5));
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available));
            Assert.That(stock.Wood, Is.EqualTo(65)); Assert.That(wall.Health, Is.EqualTo(60)); Assert.That(changes, Is.EqualTo(1));
            Assert.That(wall.transform.position, Is.EqualTo(position)); Assert.That(collider.enabled, Is.True);
            Assert.That(construction.FindDemolitionTarget(position), Is.SameAs(wall));
            Assert.That(wall.transform.Find("Wall Health Bar/Health Fill").localScale.x, Is.EqualTo(1).Within(.001f));
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.AlreadyHealthy)); Assert.That(stock.Wood, Is.EqualTo(65)); yield return null;
        }
        [UnityTest]
        public IEnumerator RepairRoundsUpRefundRoundsDownAndPurchaseTermsSurviveSettingsChanges()
        {
            Set(settings, "wallWoodCost", 17); Set(settings, "demolitionRefundPercent", 50);
            var wall = Build(); wall.ReceiveDamage(15);
            Assert.That(wall.RepairWoodCost, Is.EqualTo(5)); Assert.That(wall.RefundWood, Is.EqualTo(6));
            Set(settings, "wallWoodCost", 100); Set(settings, "wallMaxHealth", 120); Set(settings, "demolitionRefundPercent", 100);
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available));
            Assert.That(wall.Health, Is.EqualTo(60)); Assert.That(stock.Wood, Is.EqualTo(58)); Assert.That(wall.RefundWood, Is.EqualTo(8));
            wall.ReceiveDamage(1); Assert.That(wall.RepairWoodCost, Is.EqualTo(1)); Assert.That(wall.RefundWood, Is.EqualTo(7)); yield return null;
        }
        [UnityTest]
        public IEnumerator ExtremeIntegerCostsAndHealthDoNotOverflow()
        {
            stock.TryAddWood(int.MaxValue - stock.Wood); Set(settings, "wallWoodCost", int.MaxValue); Set(settings, "wallMaxHealth", int.MaxValue);
            var wall = Build(); wall.ReceiveDamage(int.MaxValue - 1);
            Assert.That(wall.RepairWoodCost, Is.EqualTo(int.MaxValue - 1)); Assert.That(wall.RefundWood, Is.EqualTo(1));
            stock.TryAddWood(int.MaxValue - 1); Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available));
            Assert.That(stock.Wood, Is.Zero); Assert.That(wall.Health, Is.EqualTo(int.MaxValue)); yield return null;
        }
        [UnityTest]
        public IEnumerator InsufficientWoodLeavesHealthUntouchedAndNewResourcesAllowRepair()
        {
            var wall = Build(); wall.ReceiveDamage(30); stock.TrySpendWood(68);
            Assert.That(construction.ValidateRepair(wall, out int cost), Is.EqualTo(RepairResult.InsufficientWood)); Assert.That(cost, Is.EqualTo(5));
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.InsufficientWood)); Assert.That(wall.Health, Is.EqualTo(30));
            stock.TryAddWood(3); Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available)); Assert.That(stock.Wood, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator NullForeignRetiredAndDisabledWallsCannotBeRepaired()
        {
            Assert.That(construction.TryRepair(null), Is.EqualTo(RepairResult.NoWall));
            var wall = Build(); wall.ReceiveDamage(10); wall.enabled = false;
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.NoWall)); wall.enabled = true;
            typeof(WallStructure).GetProperty("Owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wall, null);
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.NoWall));
            typeof(WallStructure).GetProperty("Owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wall, construction);
            construction.TryDemolish(wall); Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.NoWall)); yield return null;
        }
        [UnityTest]
        public IEnumerator ReentrantHealthAndResourceCallbacksSeeCommittedValuesAndCannotPurchaseTwice()
        {
            var wall = Build(); wall.ReceiveDamage(30); int callbacks = 0;
            Action observer = () =>
            {
                callbacks++; Assert.That(stock.Wood, Is.EqualTo(65)); Assert.That(wall.Health, Is.EqualTo(60));
                Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable));
                Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Unavailable));
                Assert.That(construction.TryBuild(new Vector2(-2, 0)), Is.EqualTo(PlacementResult.Unavailable));
            };
            wall.HealthChanged += observer; stock.Changed += observer;
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available));
            Assert.That(callbacks, Is.EqualTo(2)); wall.HealthChanged -= observer; stock.Changed -= observer; yield return null;
        }
        [UnityTest]
        public IEnumerator ResourceCallbackDamageAfterRepairCannotResurrectDestroyedWall()
        {
            var wall = Build(); wall.ReceiveDamage(30);
            Action destroy = () => wall.ReceiveDamage(int.MaxValue); stock.Changed += destroy;
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available)); stock.Changed -= destroy;
            Assert.That(wall.IsAlive, Is.False); Assert.That(stock.Wood, Is.EqualTo(65));
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.NoWall)); yield return null; Assert.That(wall == null, Is.True);
        }
        [UnityTest]
        public IEnumerator DamagedDemolitionAndRebuildCannotProvideFreeRepair()
        {
            var wall = Build(); wall.ReceiveDamage(30); Assert.That(wall.RefundWood, Is.EqualTo(5));
            construction.TryDemolish(wall); Assert.That(stock.Wood, Is.EqualTo(75));
            var rebuilt = Build(); Assert.That(rebuilt.Health, Is.EqualTo(60)); Assert.That(stock.Wood, Is.EqualTo(65));
            rebuilt.ReceiveDamage(59); Assert.That(rebuilt.RefundWood, Is.Zero);
            Assert.That(construction.TryDemolish(rebuilt), Is.EqualTo(DemolitionResult.Available)); Assert.That(stock.Wood, Is.EqualTo(65)); yield return null;
        }
        [UnityTest]
        public IEnumerator ActualRepairButtonAndWallClickUpdateHintWithoutWorldSelection()
        {
            var wall = Build(); wall.ReceiveDamage(30); yield return Button("Repair Wall");
            Assert.That(placement.IsRepairing, Is.True); yield return Pointer(Camera.main.WorldToScreenPoint(wall.transform.position));
            Assert.That(placement.RepairTarget, Is.SameAs(wall)); Assert.That(placement.RepairCost, Is.EqualTo(5));
            Assert.That(Hint(), Does.Contain("HP 30/60 / Repair 5 wood"));
            yield return Click(Camera.main.WorldToScreenPoint(wall.transform.position));
            Assert.That(stock.Wood, Is.EqualTo(65)); Assert.That(wall.Health, Is.EqualTo(60)); Assert.That(placement.IsRepairing, Is.True);
            Assert.That(Hint(), Does.Contain("already at full health")); Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Is.Empty);
            yield return Button("Repair Wall"); Assert.That(placement.IsEditing, Is.False);
        }
        [UnityTest]
        public IEnumerator StationaryPointerRefreshesRepairQuoteAndDamagedRefund()
        {
            var wall = Build(); placement.ToggleRepair(); yield return Pointer(Camera.main.WorldToScreenPoint(wall.transform.position));
            wall.ReceiveDamage(15); yield return new WaitForSeconds(.15f);
            Assert.That(placement.RepairCost, Is.EqualTo(3)); Assert.That(Hint(), Does.Contain("HP 45/60"));
            placement.ToggleDemolition(); yield return Pointer(Camera.main.WorldToScreenPoint(wall.transform.position));
            Assert.That(Hint(), Does.Contain("+7 wood")); wall.ReceiveDamage(15); yield return new WaitForSeconds(.15f);
            Assert.That(Hint(), Does.Contain("+5 wood"));
        }
        [UnityTest]
        public IEnumerator SwitchingModesAndRightClickOrEscapeReleasesPointerWithoutOrders()
        {
            Build(); placement.Toggle(); Assert.That(placement.IsPlacing, Is.True);
            yield return Button("Repair Wall"); Assert.That(placement.IsRepairing, Is.True); Assert.That(placement.IsPlacing, Is.False);
            yield return Button("Remove Wall"); Assert.That(placement.IsDemolishing, Is.True); Assert.That(placement.RepairTarget, Is.Null);
            yield return Button("Repair Wall"); yield return Click(Camera.main.WorldToScreenPoint(Vector2.zero), 2);
            Assert.That(placement.IsEditing, Is.False);
            placement.ToggleRepair(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
            Assert.That(placement.IsEditing, Is.False); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(stock.Wood, Is.EqualTo(70));
        }
        [UnityTest]
        public IEnumerator PointerOverUIAndCellGapsDoNotRepairAdjacentWalls()
        {
            var wall = Build(); wall.ReceiveDamage(30); placement.ToggleRepair();
            yield return Click(Camera.main.WorldToScreenPoint(new Vector2(-.5f,0)));
            Assert.That(wall.Health, Is.EqualTo(30)); Assert.That(stock.Wood, Is.EqualTo(70));
            var ui = Object.FindAnyObjectByType<ConstructionHUD>().transform.Find("Status").position;
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, ui));
            Assert.That(stock.Wood, Is.EqualTo(70)); Assert.That(placement.PreviewVisible, Is.False);
        }
        [UnityTest]
        public IEnumerator PauseAndDisableCancelRepairAndPreventSpending()
        {
            var wall = Build(); wall.ReceiveDamage(30); placement.ToggleRepair(); Time.timeScale = 0;
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable)); yield return null; Assert.That(placement.IsEditing, Is.False);
            Time.timeScale = 1; placement.ToggleRepair(); construction.enabled = false;
            Assert.That(placement.IsEditing, Is.False); Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable));
            construction.enabled = true; stock.enabled = false; Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable));
            stock.enabled = true; Assert.That(stock.Wood, Is.EqualTo(70));
        }
        [UnityTest]
        public IEnumerator RepairUnlocksOnlyInNextPreparationAndRepairedHealthCarriesIntoCombat()
        {
            var wall = Build(); wall.ReceiveDamage(20);
            invasionSettings = Object.Instantiate(Get<InvasionSettings>(invasion, "settings"));
            Set(invasionSettings, "unitCount", 1); Set(invasionSettings, "preparationSeconds", .1f); Set(invasion, "settings", invasionSettings);
            placement.ToggleRepair(); invasion.Begin(); Assert.That(placement.IsEditing, Is.False);
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable));
            yield return Until(() => invasion.ActiveCount > 0); invasion.Members[0].ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable));
            invasion.PrepareNextWave(); Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Available));
            Assert.That(stock.Wood, Is.EqualTo(66)); invasion.Begin(); Assert.That(wall.Health, Is.EqualTo(60));
            Assert.That(construction.TryRepair(wall), Is.EqualTo(RepairResult.Unavailable));
        }
    }
}
