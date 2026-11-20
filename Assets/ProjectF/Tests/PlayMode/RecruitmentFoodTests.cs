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
    public sealed class RecruitmentFoodTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private UnitRecruitment recruitment;
        private RecruitmentSettings settings;
        private EstateStockpile stockpile;
        private WallConstruction construction;
        private FarmSettings farmSettings;
        private PortalInvasion invasion;
        private InvasionSettings invasionSettings;
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>(); InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            construction = Object.FindAnyObjectByType<WallConstruction>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            settings = Object.Instantiate(recruitment.Settings);
            Assert.That(settings.FoodCost, Is.EqualTo(4), "Gameplay recruitment must consume food.");
            Set(settings, "trainingSeconds", .2f); Set(recruitment, "settings", settings);
            farmSettings = Object.Instantiate(construction.FarmSettings);
            Set(farmSettings, "harvestSeconds", .2f); Set(construction, "farmSettings", farmSettings);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var pause = Object.FindAnyObjectByType<GamePause>(); if (pause != null) pause.Resume();
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); Object.Destroy(farmSettings); Object.Destroy(invasionSettings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> done, float timeout = 5)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(done(), Is.True, "Food recruitment scenario timed out.");
        }
        private IEnumerator Click(Transform target)
        {
            var rect = (RectTransform)target;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        }
        private FarmProduction Farm()
        {
            Assert.That(construction.TryBuildFarm(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            return Object.FindAnyObjectByType<FarmProduction>();
        }
        [UnityTest]
        public IEnumerator MissingFoodRejectsTheWholePurchase()
        {
            Assert.That(stockpile.Food, Is.Zero);
            Assert.That(recruitment.TryTrain(), Is.False);
            stockpile.TryAddFood(3); Assert.That(recruitment.TryTrain(), Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(stockpile.Food, Is.EqualTo(3));
            Assert.That(recruitment.IsTraining, Is.False); Assert.That(stockpile.ReservedFoodCapacity, Is.Zero);
            stockpile.TryAddFood(1); Assert.That(recruitment.TryTrain(), Is.True);
            Assert.That(stockpile.Wood, Is.EqualTo(60)); Assert.That(stockpile.Food, Is.Zero);
            Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4)); yield return null;
        }
        [UnityTest]
        public IEnumerator MissingWoodDoesNotConsumeFoodOrReserveStorage()
        {
            stockpile.TryAddFood(4); stockpile.TrySpendWood(61);
            Assert.That(recruitment.TryTrain(), Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(19)); Assert.That(stockpile.Food, Is.EqualTo(4));
            Assert.That(stockpile.ReservedFoodCapacity, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator FarmHarvestFundsAnActualDeployedSoldier()
        {
            var farm = Farm(); yield return Until(() => stockpile.Food == 4); farm.enabled = false;
            Assert.That(recruitment.TryTrain(), Is.True);
            Assert.That(stockpile.Wood, Is.EqualTo(30)); Assert.That(stockpile.Food, Is.Zero);
            yield return Until(() => recruitment.AlliedCount == 5);
            Assert.That(recruitment.IsTraining, Is.False); Assert.That(stockpile.ReservedFoodCapacity, Is.Zero);
            Assert.That(recruitment.TryCancelTraining(), Is.False);
            Assert.That(recruitment.GetComponentsInChildren<UnitCombat>().Single().IsAlive, Is.True);
        }
        [UnityTest]
        public IEnumerator FullStorageRetainsRefundRoomAndReturnsBothResourcesOnce()
        {
            stockpile.TryAddFood(200); Assert.That(recruitment.TryTrain(), Is.True);
            Assert.That(stockpile.Food, Is.EqualTo(196)); Assert.That(stockpile.CanAddFood(1), Is.False);
            Assert.That(recruitment.TryCancelTraining(), Is.True);
            Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(stockpile.Food, Is.EqualTo(200));
            Assert.That(stockpile.ReservedFoodCapacity, Is.Zero); Assert.That(recruitment.TryCancelTraining(), Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator HeldHarvestResumesAfterDeploymentReleasesRefundRoom()
        {
            Farm(); stockpile.TryAddFood(200); Set(settings, "trainingSeconds", .7f);
            Assert.That(recruitment.TryTrain(), Is.True); yield return new WaitForSeconds(.3f);
            Assert.That(stockpile.Food, Is.EqualTo(196)); Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4));
            yield return Until(() => recruitment.AlliedCount == 5 && stockpile.Food == 200);
            Assert.That(stockpile.ReservedFoodCapacity, Is.Zero); yield return new WaitForSeconds(.25f);
            Assert.That(stockpile.Food, Is.EqualTo(200));
        }
        [UnityTest]
        public IEnumerator ObserversSeeAtomicPurchaseAndRefundAndCannotRepeatThem()
        {
            stockpile.TryAddFood(8); int notifications = 0;
            Action purchase = () =>
            {
                notifications++;
                Assert.That(stockpile.Wood, Is.EqualTo(60)); Assert.That(stockpile.Food, Is.EqualTo(4));
                Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4)); Assert.That(recruitment.IsTraining, Is.True);
                Assert.That(recruitment.TryTrain() || recruitment.TryCancelTraining(), Is.False);
            };
            stockpile.Changed += purchase;
            try { Assert.That(recruitment.TryTrain(), Is.True); }
            finally { stockpile.Changed -= purchase; }
            Action refund = () =>
            {
                notifications++;
                Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(stockpile.Food, Is.EqualTo(8));
                Assert.That(stockpile.ReservedFoodCapacity, Is.Zero); Assert.That(recruitment.IsTraining, Is.False);
                Assert.That(recruitment.TryTrain() || recruitment.TryCancelTraining(), Is.False);
            };
            stockpile.Changed += refund;
            try { Assert.That(recruitment.TryCancelTraining(), Is.True); }
            finally { stockpile.Changed -= refund; }
            Assert.That(notifications, Is.EqualTo(2)); yield return null;
        }
        [UnityTest]
        public IEnumerator CancellationUsesOriginalPricesAfterSettingsChange()
        {
            stockpile.TryAddFood(8); Assert.That(recruitment.TryTrain(), Is.True);
            Set(settings, "foodCost", 8); Set(settings, "woodCost", 25);
            Assert.That(recruitment.CancellationFoodRefund, Is.EqualTo(4));
            Assert.That(recruitment.TryCancelTraining(), Is.True);
            Assert.That(stockpile.Food, Is.EqualTo(8)); Assert.That(stockpile.Wood, Is.EqualTo(80));
            Assert.That(recruitment.TryTrain(), Is.True);
            Assert.That(stockpile.Food, Is.Zero); Assert.That(stockpile.Wood, Is.EqualTo(55));
            Assert.That(recruitment.CancellationFoodRefund, Is.EqualTo(8)); yield return null;
        }
        [UnityTest]
        public IEnumerator PauseAndDisableRetainPaymentUntilCancellation()
        {
            stockpile.TryAddFood(4); Set(settings, "trainingSeconds", 1f); recruitment.TryTrain();
            var pause = Object.FindAnyObjectByType<GamePause>(); pause.TryPause();
            float remaining = recruitment.RemainingSeconds; yield return new WaitForSecondsRealtime(.25f);
            Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(recruitment.TryCancelTraining(), Is.False);
            pause.Resume(); recruitment.enabled = false; yield return new WaitForSeconds(.25f);
            Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4));
            recruitment.enabled = true; Assert.That(recruitment.TryCancelTraining(), Is.True);
            Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(stockpile.ReservedFoodCapacity, Is.Zero);
        }
        [UnityTest]
        public IEnumerator InvasionSuspendsPaidTrainingWithoutChargingAgain()
        {
            var field = typeof(PortalInvasion).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
            invasionSettings = Object.Instantiate((InvasionSettings)field.GetValue(invasion));
            Set(invasionSettings, "unitCount", 1); Set(invasionSettings, "preparationSeconds", 0f); field.SetValue(invasion, invasionSettings);
            stockpile.TryAddFood(4); recruitment.TryTrain(); float remaining = recruitment.RemainingSeconds;
            Assert.That(invasion.Begin(), Is.True); yield return Until(() => invasion.ActiveCount > 0);
            Assert.That(recruitment.TryCancelTraining() || recruitment.TryTrain(), Is.False);
            Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining));
            foreach (var enemy in invasion.Members.ToArray()) enemy.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
            Assert.That(stockpile.Food, Is.Zero); Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4));
            Assert.That(invasion.PrepareNextWave(), Is.True);
            yield return Until(() => recruitment.AlliedCount == 5);
            Assert.That(stockpile.Wood, Is.EqualTo(60)); Assert.That(stockpile.Food, Is.Zero);
            Assert.That(stockpile.ReservedFoodCapacity, Is.Zero);
        }
        [UnityTest]
        public IEnumerator ActualHudShowsShortageAndBuysAndRefundsThePaidSlot()
        {
            Set(settings, "trainingSeconds", 2f);
            var hud = Object.FindAnyObjectByType<RecruitmentHUD>();
            var button = hud.GetComponentInChildren<UnityEngine.UI.Button>();
            var status = hud.transform.Find("Status").GetComponent<UnityEngine.UI.Text>();
            yield return null;
            Assert.That(button.interactable, Is.False); StringAssert.Contains("Need food", status.text);
            yield return Click(button.transform); Assert.That(stockpile.Wood, Is.EqualTo(80));
            stockpile.TryAddFood(4); yield return null;
            Assert.That(button.interactable, Is.True); yield return Click(button.transform);
            Assert.That(recruitment.IsTraining, Is.True); Assert.That(stockpile.Food, Is.Zero);
            StringAssert.Contains("+20W + 4F", status.text);
            yield return Click(button.transform); Assert.That(recruitment.IsTraining, Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(stockpile.Food, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator SceneRestartClearsFoodAndPendingRefundReservation()
        {
            stockpile.TryAddFood(4); recruitment.TryTrain();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            var estate = Object.FindAnyObjectByType<EstateStockpile>();
            Assert.That(estate.Food, Is.Zero); Assert.That(estate.ReservedFoodCapacity, Is.Zero);
            Assert.That(Object.FindAnyObjectByType<UnitRecruitment>().IsTraining, Is.False);
        }
        [UnityTest]
        public IEnumerator ZeroFoodPriceSupportsWoodOnlyConfigurations()
        {
            Set(settings, "foodCost", 0); Assert.That(recruitment.TryTrain(), Is.True);
            Assert.That(stockpile.Food, Is.Zero); Assert.That(stockpile.ReservedFoodCapacity, Is.Zero);
            Assert.That(recruitment.TryCancelTraining(), Is.True); Assert.That(stockpile.Wood, Is.EqualTo(80)); yield return null;
        }
        [UnityTest]
        public IEnumerator RosterRegistrationCannotRefundAnAlreadyDeployingSoldier()
        {
            stockpile.TryAddFood(8); recruitment.TryTrain();
            bool reachedDeployment = false, refunded = false, purchased = false;
            Action duringRegistration = () =>
            {
                if (reachedDeployment || recruitment.AlliedCount != 5 || !recruitment.IsTraining) return;
                reachedDeployment = true;
                refunded |= recruitment.TryCancelTraining();
                purchased |= recruitment.TryTrain();
            };
            recruitment.Changed += duringRegistration;
            try { yield return Until(() => recruitment.AlliedCount == 5 && !recruitment.IsTraining); }
            finally { recruitment.Changed -= duringRegistration; }
            Assert.That(reachedDeployment, Is.True);
            Assert.That(refunded || purchased, Is.False, "Deployment must lock its paid slot until all observers see the final state.");
            Assert.That(stockpile.Wood, Is.EqualTo(60)); Assert.That(stockpile.Food, Is.EqualTo(4));
            Assert.That(stockpile.ReservedFoodCapacity, Is.Zero);
        }
        [UnityTest]
        public IEnumerator DeploymentObserversSeeTheNewRosterBeforeAnotherPurchase()
        {
            stockpile.TryAddFood(8); Set(settings, "maximumAllies", 5);
            Assert.That(recruitment.TryTrain(), Is.True); bool observed = false;
            Action deployed = () =>
            {
                if (stockpile.ReservedFoodCapacity != 0) return;
                observed = true;
                Assert.That(recruitment.AlliedCount, Is.EqualTo(5));
                Assert.That(recruitment.IsTraining, Is.False);
                Assert.That(recruitment.TryTrain(), Is.False);
            };
            stockpile.Changed += deployed;
            try { yield return Until(() => observed); }
            finally { stockpile.Changed -= deployed; }
            Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(stockpile.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator OtherFoodSpendingAndHarvestingCannotOccupyRefundSpace()
        {
            stockpile.TryAddFood(8); recruitment.TryTrain();
            Assert.That(stockpile.TrySpendFood(4), Is.True); Assert.That(stockpile.TrySpendFood(1), Is.False);
            Assert.That(stockpile.TryAddFood(197), Is.False); Assert.That(stockpile.TryAddFood(196), Is.True);
            Assert.That(recruitment.TryCancelTraining(), Is.True);
            Assert.That(stockpile.Food, Is.EqualTo(200)); Assert.That(stockpile.ReservedFoodCapacity, Is.Zero); yield return null;
        }
    }
}
