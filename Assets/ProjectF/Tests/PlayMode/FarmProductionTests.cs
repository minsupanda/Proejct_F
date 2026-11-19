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
using ProjectF.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class FarmProductionTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private WallConstruction construction;
        private WallPlacementController placement;
        private EstateStockpile stockpile;
        private PortalInvasion invasion;
        private FarmSettings settings;
        private InvasionSettings invasionSettings;
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            construction = Object.FindAnyObjectByType<WallConstruction>();
            placement = Object.FindAnyObjectByType<WallPlacementController>();
            stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            Assert.That(construction.FarmSettings, Is.Not.Null, "Run FarmSetup.Configure before these scene tests.");
            settings = Object.Instantiate(construction.FarmSettings);
            Set(settings, "harvestSeconds", .2f); Set(construction, "farmSettings", settings);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var pause = Object.FindAnyObjectByType<GamePause>(); if (pause != null) pause.Resume();
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); Object.Destroy(invasionSettings); fixture.TearDown();
        }
        private FarmProduction Build(Vector2 point)
        {
            Assert.That(construction.TryBuildFarm(point), Is.EqualTo(PlacementResult.Available));
            return Object.FindObjectsByType<FarmProduction>().Single(f => (Vector2)f.transform.position == construction.Settings.Snap(point));
        }
        private IEnumerator Until(Func<bool> ready, float seconds = 5)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(ready(), Is.True, "Farm scenario timed out.");
        }
        private IEnumerator Pointer(Vector2 point, ushort buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = buttons });
            yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 screen)
        {
            yield return Pointer(screen); yield return Pointer(screen, 1); yield return Pointer(screen);
        }
        [UnityTest]
        public IEnumerator BuildSnapsChargesOnceAndUsesFullFootprint()
        {
            var farm = Build(new Vector2(-1.1f, .1f));
            Assert.That(stockpile.Wood, Is.EqualTo(50));
            Assert.That(farm.GetComponent<WallStructure>().PaidWood, Is.EqualTo(30));
            Assert.That(farm.GetComponent<WallStructure>().Footprint.x, Is.EqualTo(1.8f).Within(.01f));
            Assert.That(construction.TryBuildFarm(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Occupied));
            Assert.That(stockpile.Wood, Is.EqualTo(50));
            Assert.That(farm.GetComponentInChildren<SpriteRenderer>().sprite, Is.Not.Null);
            yield return null;
        }
        [UnityTest]
        public IEnumerator WholeFootprintAndTerritoryRejectWithoutSpending()
        {
            Assert.That(construction.TryBuild(new Vector2(-2, 3)), Is.EqualTo(PlacementResult.Available));
            Assert.That(construction.TryBuildFarm(new Vector2(-1, 3)), Is.EqualTo(PlacementResult.Occupied));
            Assert.That(construction.TryBuildFarm(new Vector2(0, 0)), Is.EqualTo(PlacementResult.OutsideTerritory));
            Assert.That(construction.TryBuildFarm(new Vector2(float.NaN, 0)), Is.EqualTo(PlacementResult.OutsideTerritory));
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            Assert.That(stockpile.TrySpendWood(50), Is.True);
            Assert.That(construction.TryBuildFarm(new Vector2(-1, 3)), Is.EqualTo(PlacementResult.InsufficientWood));
            Assert.That(Object.FindObjectsByType<FarmProduction>(), Is.Empty); yield return null;
        }
        [UnityTest]
        public IEnumerator HarvestAddsFoodWithoutChangingWoodOrSharedSettings()
        {
            var farm = Build(new Vector2(-1, 0));
            yield return Until(() => stockpile.Food > 0);
            Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(stockpile.Wood, Is.EqualTo(50));
            Assert.That(farm.RemainingSeconds, Is.GreaterThan(0));
            StringAssert.Contains("FOOD 4 / 200", Object.FindAnyObjectByType<FarmHUD>().transform.Find("Status").GetComponent<UnityEngine.UI.Text>().text);
        }
        [UnityTest]
        public IEnumerator PauseAndDisableRetainProgressWithoutOfflineCatchup()
        {
            Set(settings, "harvestSeconds", .5f); var farm = Build(new Vector2(-1, 0));
            yield return null; var pause = Object.FindAnyObjectByType<GamePause>(); Assert.That(pause.TryPause(), Is.True);
            float left = farm.RemainingSeconds; yield return new WaitForSecondsRealtime(.15f);
            Assert.That(farm.RemainingSeconds, Is.EqualTo(left)); Assert.That(stockpile.Food, Is.Zero);
            pause.Resume(); farm.enabled = false; yield return new WaitForSecondsRealtime(.15f);
            Assert.That(farm.RemainingSeconds, Is.EqualTo(left));
            farm.enabled = true; yield return Until(() => stockpile.Food > 0); Assert.That(stockpile.Food, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator InvasionAndResultHoldHarvestUntilNextPreparation()
        {
            var farm = Build(new Vector2(-1, 0));
            invasionSettings = Object.Instantiate(Get<InvasionSettings>(invasion, "settings"));
            Set(invasionSettings, "unitCount", 1); Set(invasionSettings, "preparationSeconds", 0f);
            Set(invasion, "settings", invasionSettings); Assert.That(invasion.Begin(), Is.True);
            float left = farm.RemainingSeconds;
            yield return Until(() => invasion.Members.Count == 1);
            invasion.Members[0].ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(stockpile.Food, Is.Zero); Assert.That(farm.RemainingSeconds, Is.EqualTo(left));
            Assert.That(invasion.PrepareNextWave(), Is.True); yield return Until(() => stockpile.Food == 4);
        }
        [UnityTest]
        public IEnumerator FullStorageHoldsOneHarvestAndResumesWhenSpaceReturns()
        {
            var farm = Build(new Vector2(-1, 0)); Assert.That(stockpile.TryAddFood(200), Is.True);
            yield return Until(() => farm.RemainingSeconds == 0);
            yield return new WaitForSecondsRealtime(.25f); Assert.That(stockpile.Food, Is.EqualTo(200));
            Assert.That(stockpile.TrySpendFood(4), Is.True); yield return Until(() => stockpile.Food == 200);
            Assert.That(farm.RemainingSeconds, Is.GreaterThan(0));
            Assert.That(stockpile.TryAddFood(1), Is.False);
        }
        [UnityTest]
        public IEnumerator TwoFarmsCannotOverflowTheLastStorageSlot()
        {
            Build(new Vector2(-1, 0)); Build(new Vector2(-1, 3)); Assert.That(stockpile.TryAddFood(196), Is.True);
            yield return Until(() => stockpile.Food == 200);
            yield return new WaitForSecondsRealtime(.25f); Assert.That(stockpile.Food, Is.EqualTo(200));
            Assert.That(stockpile.Wood, Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator RepairAndDemolitionUseFarmPurchaseAndHealth()
        {
            var farm = Build(new Vector2(-1, 0)); var structure = farm.GetComponent<WallStructure>();
            structure.ReceiveDamage(30);
            Assert.That(structure.RepairWoodCost, Is.EqualTo(15));
            Assert.That(construction.TryRepair(structure), Is.EqualTo(RepairResult.Available));
            Assert.That(stockpile.Wood, Is.EqualTo(35)); Assert.That(structure.Health, Is.EqualTo(60));
            structure.ReceiveDamage(30);
            Assert.That(construction.TryDemolish(structure), Is.EqualTo(DemolitionResult.Available));
            Assert.That(stockpile.Wood, Is.EqualTo(50));
            Assert.That(construction.TryDemolish(structure), Is.EqualTo(DemolitionResult.NoWall));
            yield return new WaitForSecondsRealtime(.3f); Assert.That(stockpile.Food, Is.Zero);
            Assert.That(construction.ValidateFarm(new Vector2(-1, 0), out _), Is.EqualTo(PlacementResult.Available));
        }
        [UnityTest]
        public IEnumerator HostileActualAttackDamagesFarmAndDestructionStopsProduction()
        {
            Set(settings, "harvestSeconds", 30f); var farm = Build(new Vector2(-1, 0)); var structure = farm.GetComponent<WallStructure>();
            var enemy = Object.Instantiate(Get<UnitCombat>(invasion, "enemyPrefab"));
            enemy.GetComponent<EnemyCombatAI>().enabled = false;
            enemy.GetComponent<CommandableUnit>().InitializeNavigation(Object.FindAnyObjectByType<NavigationWorld2D>());
            enemy.transform.position = farm.transform.position + new Vector3(1.5f, 0, 0); enemy.gameObject.SetActive(true);
            Physics2D.SyncTransforms(); Assert.That(enemy.AttackWall(structure), Is.True);
            yield return Until(() => structure.Health < structure.MaxHealth);
            structure.ReceiveDamage(int.MaxValue); yield return null;
            Assert.That(farm == null, Is.True); Assert.That(stockpile.Wood, Is.EqualTo(50)); Assert.That(stockpile.Food, Is.Zero);
        }
        [UnityTest]
        public IEnumerator RealFarmButtonAndMapClickConsumeWorldOrders()
        {
            var commands = Object.FindAnyObjectByType<UnitCommandController>();
            var lord = GameObject.Find("Lord").GetComponent<CommandableUnit>();
            yield return Click(Camera.main.WorldToScreenPoint(lord.Position));
            Assert.That(commands.Selected, Is.EquivalentTo(new[] { lord })); long order = lord.CommandOrder;
            var button = Object.FindAnyObjectByType<FarmHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            var rect = (RectTransform)button.transform;
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)));
            Assert.That(placement.IsPlacingFarm, Is.True);
            Vector2 screen = Camera.main.WorldToScreenPoint(new Vector2(-1, 0)); yield return Pointer(screen);
            Assert.That(commands.PointerOverUI(), Is.False); Assert.That(placement.PreviewVisible, Is.True);
            yield return Click(screen);
            Assert.That(Object.FindObjectsByType<FarmProduction>().Length, Is.EqualTo(1));
            Assert.That(commands.Selected, Is.EquivalentTo(new[] { lord })); Assert.That(lord.CommandOrder, Is.EqualTo(order));
            yield return Pointer(screen, 2); yield return Pointer(screen); Assert.That(placement.IsEditing, Is.False);
        }
        [UnityTest]
        public IEnumerator RallyWallFarmAndPauseHaveOneActiveTool()
        {
            var rally = Object.FindAnyObjectByType<RecruitmentRallyController>();
            rally.Toggle(); Assert.That(rally.IsChoosing, Is.True);
            placement.ToggleFarm(); Assert.That(rally.IsChoosing, Is.False); Assert.That(placement.IsPlacingFarm, Is.True);
            placement.Toggle(); Assert.That(placement.Mode, Is.EqualTo(ConstructionMode.Build));
            placement.ToggleFarm(); rally.Toggle(); Assert.That(placement.IsEditing, Is.False);
            placement.ToggleFarm(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null; Assert.That(placement.IsEditing, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            placement.ToggleFarm(); Object.FindAnyObjectByType<GamePause>().TryPause(); yield return null;
            Assert.That(placement.IsEditing, Is.False);
            Assert.That(construction.TryBuildFarm(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Unavailable));
        }
        [UnityTest]
        public IEnumerator SceneRestartResetsFoodAndBuiltFarms()
        {
            Build(new Vector2(-1, 0)); yield return Until(() => stockpile.Food > 0);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindObjectsByType<FarmProduction>(), Is.Empty);
            Assert.That(Object.FindObjectsByType<FarmHUD>().Length, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Food, Is.Zero);
        }
        [UnityTest]
        public IEnumerator HarvestIsCommittedBeforeResourceObserversDisableTheFarm()
        {
            var farm = Build(new Vector2(-1, 0));
            float observedRemaining = 0; int deliveries = 0;
            Action listener = () => { deliveries++; observedRemaining = farm.RemainingSeconds; farm.enabled = false; };
            stockpile.Changed += listener;
            try
            {
                yield return Until(() => stockpile.Food == 4);
                Assert.That(observedRemaining, Is.GreaterThan(0));
                yield return new WaitForSecondsRealtime(.25f); Assert.That(deliveries, Is.EqualTo(1));
                Assert.That(stockpile.Food, Is.EqualTo(4));
            }
            finally { stockpile.Changed -= listener; }
            farm.enabled = true; yield return Until(() => stockpile.Food == 8);
        }
        [UnityTest]
        public IEnumerator ResourceBoundsRejectInvalidAndDisabledTransactions()
        {
            Assert.That(stockpile.TryAddFood(0), Is.False); Assert.That(stockpile.TryAddFood(-1), Is.False);
            Assert.That(stockpile.TryAddFood(int.MaxValue), Is.False); Assert.That(stockpile.TrySpendFood(1), Is.False);
            Assert.That(stockpile.TryAddFood(10), Is.True); Assert.That(stockpile.TrySpendFood(-1), Is.False);
            stockpile.enabled = false;
            Assert.That(stockpile.TryAddFood(1), Is.False); Assert.That(stockpile.TrySpendFood(1), Is.False);
            Assert.That(stockpile.Food, Is.EqualTo(10)); Assert.That(stockpile.Wood, Is.EqualTo(80)); yield return null;
        }
    }
}
