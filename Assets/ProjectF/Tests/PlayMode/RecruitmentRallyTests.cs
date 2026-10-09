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
    public sealed class RecruitmentRallyTests
    {
        private UnitRecruitment recruitment;
        private RecruitmentRallyController controller;
        private RecruitmentSettings settings;
        private InvasionSettings invasionSettings;
        private PortalInvasion invasion;
        private EstateStockpile stock;
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private readonly Vector2 first = new Vector2(-16, 2), second = new Vector2(-16, 5);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); controller = Object.FindAnyObjectByType<RecruitmentRallyController>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>(); stock = Object.FindAnyObjectByType<EstateStockpile>();
            settings = Object.Instantiate(recruitment.Settings); Set(settings, "trainingSeconds", .1f); Set(recruitment, "settings", settings);
            invasionSettings = Object.Instantiate(Get<InvasionSettings>(invasion, "settings"));
            Set(invasionSettings, "unitCount", 1); Set(invasionSettings, "preparationSeconds", .1f); Set(invasion, "settings", invasionSettings);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); Object.Destroy(invasionSettings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> predicate, float seconds = 6)
        {
            float end = Time.time + seconds;
            while (!predicate() && Time.time < end) yield return null;
            Assert.That(predicate(), Is.True, "Rally test timed out.");
        }
        private IEnumerator Train()
        {
            Assert.That(recruitment.TryTrain(), Is.True); yield return Until(() => !recruitment.IsTraining);
        }
        private UnitCombat Soldier() => recruitment.GetComponentsInChildren<UnitCombat>().OrderBy(u => u.name).Last();
        private IEnumerator Pointer(Vector2 point, ushort buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = buttons }); yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 point, ushort buttons = 1)
        {
            yield return Pointer(point); yield return Pointer(point, buttons); yield return Pointer(point);
        }
        private IEnumerator Button(string name)
        {
            var rect = Object.FindAnyObjectByType<RecruitmentRallyHUD>().transform.Find(name);
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, rect.position));
        }
        private Vector2 Screen(Vector2 point) => Camera.main.WorldToScreenPoint(point);

        [UnityTest]
        public IEnumerator DefaultLeavesRecruitsAtCampWithoutMovementOrder()
        {
            Assert.That(recruitment.HasRallyPoint, Is.False); yield return Train();
            Assert.That(Soldier().Unit.HasDestination, Is.False); Assert.That(stock.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator RallyIssuesOneOrderAndRecruitArrivesUsingExistingNavigation()
        {
            Assert.That(recruitment.TrySetRallyPoint(first), Is.EqualTo(RallyPointResult.Available));
            yield return Train(); var unit = Soldier().Unit; long order = unit.CommandOrder;
            Assert.That(unit.RequestedDestination, Is.EqualTo(first)); Assert.That(stock.Wood, Is.EqualTo(60));
            yield return Until(() => unit.TravelState == UnitTravelState.Arrived);
            Assert.That(Vector2.Distance(unit.Position, first), Is.LessThan(.1f)); Assert.That(unit.CommandOrder, Is.EqualTo(order));
        }
        [UnityTest]
        public IEnumerator PendingTrainingUsesLatestPointAndExistingManualOrderIsPreserved()
        {
            recruitment.TrySetRallyPoint(first); yield return Train(); var original = Soldier().Unit;
            original.MoveTo(new Vector2(-17, 0)); long order = original.CommandOrder;
            Set(settings, "trainingSeconds", .3f); recruitment.TryTrain(); recruitment.TrySetRallyPoint(second);
            yield return Until(() => !recruitment.IsTraining);
            Assert.That(Soldier().Unit.RequestedDestination, Is.EqualTo(second));
            Assert.That(original.CommandOrder, Is.EqualTo(order)); Assert.That(original.RequestedDestination, Is.EqualTo(new Vector2(-17,0)));
        }
        [UnityTest]
        public IEnumerator ClearAffectsFutureRecruitOnlyAndCostsNothing()
        {
            recruitment.TrySetRallyPoint(first); yield return Train(); var original = Soldier().Unit; long order = original.CommandOrder;
            Assert.That(recruitment.ClearRallyPoint(), Is.True); Assert.That(recruitment.ClearRallyPoint(), Is.False);
            yield return Train(); Assert.That(Soldier().Unit.HasDestination, Is.False);
            Assert.That(original.CommandOrder, Is.EqualTo(order)); Assert.That(stock.Wood, Is.EqualTo(40));
        }
        [UnityTest]
        public IEnumerator InvalidCoordinatesAndTerrainPreserveExistingPointAndResources()
        {
            recruitment.TrySetRallyPoint(first); int changes = 0; recruitment.Changed += () => changes++;
            Assert.That(recruitment.TrySetRallyPoint(first), Is.EqualTo(RallyPointResult.Available)); Assert.That(changes, Is.Zero);
            foreach (var point in new[] {new Vector2(float.NaN,0),new Vector2(0,float.PositiveInfinity),new Vector2(24,0),new Vector2(-23,0)})
                Assert.That(recruitment.TrySetRallyPoint(point), Is.EqualTo(RallyPointResult.OutsideMap));
            var tree = Object.FindAnyObjectByType<WoodResourceNode>();
            Assert.That(recruitment.TrySetRallyPoint(tree.transform.position), Is.EqualTo(RallyPointResult.Blocked));
            var construction = Object.FindAnyObjectByType<WallConstruction>(); construction.TryBuild(new Vector2(-1,0));
            Assert.That(recruitment.TrySetRallyPoint(new Vector2(-1,0)), Is.EqualTo(RallyPointResult.Blocked));
            Assert.That(recruitment.RallyPoint, Is.EqualTo(first)); Assert.That(stock.Wood, Is.EqualTo(70)); yield return null;
        }
        [UnityTest]
        public IEnumerator OccupiedRallyUsesDistinctArrivalPositionsForTwoRecruits()
        {
            recruitment.TrySetRallyPoint(first); yield return Train(); var a = Soldier().Unit;
            yield return Until(() => a.TravelState == UnitTravelState.Arrived);
            Assert.That(recruitment.ValidateRallyPoint(first), Is.EqualTo(RallyPointResult.Available));
            yield return Train(); var b = Soldier().Unit; yield return Until(() => b.TravelState == UnitTravelState.Arrived);
            Assert.That(Vector2.Distance(a.Position,b.Position), Is.GreaterThanOrEqualTo(a.Radius+b.Radius));
            Assert.That(Vector2.Distance(b.Position,first), Is.LessThan(2));
        }
        [UnityTest]
        public IEnumerator ActualButtonsAndGroundClickSetFlagThenClearWithoutSelectingWorld()
        {
            yield return Button("Set Rally"); Assert.That(controller.IsChoosing, Is.True);
            yield return Click(Screen(first)); Assert.That(controller.IsChoosing, Is.False); Assert.That(recruitment.HasRallyPoint, Is.True);
            Assert.That(Vector2.Distance(recruitment.RallyPoint,first), Is.LessThan(.01f));
            Assert.That(Get<LineRenderer>(controller,"marker").enabled, Is.True);
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Is.Empty);
            yield return Button("Clear Rally"); Assert.That(recruitment.HasRallyPoint, Is.False);
            Assert.That(Get<LineRenderer>(controller,"marker").enabled, Is.False); Assert.That(stock.Wood, Is.EqualTo(80));
        }
        [UnityTest]
        public IEnumerator ConstructionAndRallyModesAreMutuallyExclusiveInBothDirections()
        {
            var placement = Object.FindAnyObjectByType<WallPlacementController>();
            foreach (Action toggle in new Action[] { placement.Toggle, placement.ToggleDemolition, placement.ToggleRepair })
            {
                toggle(); controller.Toggle(); Assert.That(placement.IsEditing, Is.False); Assert.That(controller.IsChoosing, Is.True);
                toggle(); Assert.That(controller.IsChoosing, Is.False); Assert.That(placement.IsEditing, Is.True); placement.Cancel();
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator RightClickEscapeAndToggleCancelWithoutLeakingMoveOrders()
        {
            var unit = GameObject.Find("Lord").GetComponent<CommandableUnit>(); yield return Click(Screen(unit.Position));
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Does.Contain(unit)); long order = unit.CommandOrder;
            controller.Toggle(); yield return Click(Screen(first),2); Assert.That(controller.IsChoosing, Is.False);
            Assert.That(unit.CommandOrder, Is.EqualTo(order));
            controller.Toggle(); fixture.Press(keyboard.escapeKey); yield return null; fixture.Release(keyboard.escapeKey); yield return null;
            Assert.That(controller.IsChoosing, Is.False); controller.Toggle(); controller.Toggle(); Assert.That(controller.IsChoosing, Is.False);
            yield return Click(Screen(first),2); Assert.That(Vector2.Distance(unit.RequestedDestination,first), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator UIOutsideViewPanningAndBlockedClickDoNotReplacePoint()
        {
            recruitment.TrySetRallyPoint(first); controller.Toggle();
            var text = Object.FindAnyObjectByType<RecruitmentRallyHUD>().transform.Find("Status");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null,text.position));
            yield return Click(new Vector2(-100,-100));
            yield return Pointer(Screen(second),4); yield return Pointer(Screen(second),5); yield return Pointer(Screen(second));
            var tree = Object.FindAnyObjectByType<WoodResourceNode>(); yield return Click(Screen(tree.transform.position));
            Assert.That(controller.Result, Is.EqualTo(RallyPointResult.Blocked)); Assert.That(controller.IsChoosing, Is.True);
            Assert.That(recruitment.RallyPoint, Is.EqualTo(first)); Assert.That(stock.Wood, Is.EqualTo(80));
        }
        [UnityTest]
        public IEnumerator PauseAndDisableCancelEditingButKeepPointAndReleaseInput()
        {
            recruitment.TrySetRallyPoint(first); controller.Toggle(); Time.timeScale = 0; yield return null;
            Assert.That(controller.IsChoosing, Is.False); Assert.That(recruitment.TrySetRallyPoint(second), Is.EqualTo(RallyPointResult.Unavailable));
            Assert.That(recruitment.ClearRallyPoint(), Is.False); Time.timeScale = 1;
            controller.Toggle(); controller.enabled = false; Assert.That(controller.IsChoosing, Is.False);
            Assert.That(Get<LineRenderer>(controller,"marker").enabled, Is.False); controller.enabled = true;
            recruitment.enabled = false; controller.Toggle(); Assert.That(controller.IsChoosing, Is.False);
            recruitment.enabled = true; yield return null; Assert.That(recruitment.RallyPoint, Is.EqualTo(first));
            Assert.That(Get<LineRenderer>(controller,"marker").enabled, Is.True);
            yield return Click(Screen(GameObject.Find("Lord").transform.position)); Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected.Count, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator InvasionLocksEditingRetainsPointAndNextPreparationUsesIt()
        {
            recruitment.TrySetRallyPoint(first); controller.Toggle(); Assert.That(invasion.Begin(), Is.True);
            Assert.That(controller.IsChoosing, Is.False); Assert.That(recruitment.ClearRallyPoint(), Is.False);
            Assert.That(recruitment.TrySetRallyPoint(second), Is.EqualTo(RallyPointResult.Unavailable));
            yield return Until(() => invasion.ActiveCount == 1); invasion.Members[0].ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled); Assert.That(invasion.PrepareNextWave(), Is.True);
            yield return Train(); Assert.That(Soldier().Unit.RequestedDestination, Is.EqualTo(first));
        }
        [UnityTest]
        public IEnumerator ObstacleAddedAfterSettingRallyUsesNearbyReachableGround()
        {
            var point = new Vector2(-1,0); recruitment.TrySetRallyPoint(point);
            var construction = Object.FindAnyObjectByType<WallConstruction>(); Assert.That(construction.TryBuild(point), Is.EqualTo(PlacementResult.Available));
            var wall = construction.FindDemolitionTarget(point); yield return Train(); var unit = Soldier().Unit;
            yield return Until(() => unit.TravelState == UnitTravelState.Arrived, 10);
            Assert.That(unit.RequestedDestination, Is.EqualTo(point)); Assert.That(unit.Destination, Is.Not.EqualTo(point));
            Assert.That(wall.GetComponent<Collider2D>().OverlapPoint(unit.Position), Is.False);
        }
        [UnityTest]
        public IEnumerator SceneReloadResetsRallyAndEditingState()
        {
            recruitment.TrySetRallyPoint(first); controller.Toggle(); yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindAnyObjectByType<UnitRecruitment>().HasRallyPoint, Is.False);
            Assert.That(Object.FindAnyObjectByType<RecruitmentRallyController>().IsChoosing, Is.False);
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
        }
    }
}
