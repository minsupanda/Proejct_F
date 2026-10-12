using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
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
    public sealed class RecruitmentCancelTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private UnitRecruitment recruitment;
        private EstateStockpile stockpile;
        private PortalInvasion invasion;
        private RecruitmentSettings settings;
        private InvasionSettings invasionSettings;
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); stockpile = Object.FindAnyObjectByType<EstateStockpile>(); invasion = Object.FindAnyObjectByType<PortalInvasion>();
            settings = Object.Instantiate(recruitment.Settings); Set(settings, "trainingSeconds", .5f); Set(recruitment, "settings", settings);
            invasionSettings = Object.Instantiate(Get<InvasionSettings>(invasion, "settings"));
            Set(invasionSettings, "unitCount", 1); Set(invasionSettings, "preparationSeconds", .1f); Set(invasion, "settings", invasionSettings);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene"); Object.Destroy(settings); Object.Destroy(invasionSettings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> predicate)
        {
            float end = Time.time + 5; while (!predicate() && Time.time < end) yield return null;
            Assert.That(predicate(), Is.True, "Recruitment cancellation scenario timed out.");
        }
        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        }
        private void Cancelled()
        {
            Assert.That(recruitment.IsTraining, Is.False); Assert.That(recruitment.RemainingSeconds, Is.Zero);
            Assert.That(recruitment.CancellationRefund, Is.Zero); Assert.That(recruitment.State, Is.EqualTo(RecruitmentState.Idle));
            Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(invasion.DefenderCount, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator CancelRefundsOnceAndNeverDeploysTheCancelledSlot()
        {
            Assert.That(recruitment.TryTrain(), Is.True); yield return new WaitForSeconds(.1f);
            Assert.That(recruitment.CancellationRefund, Is.EqualTo(20)); Assert.That(recruitment.TryCancelTraining(), Is.True); Cancelled();
            Assert.That(recruitment.TryCancelTraining(), Is.False); yield return new WaitForSeconds(.6f); Cancelled();
            Assert.That(recruitment.GetComponentsInChildren<UnitCombat>(true), Is.Empty);
        }
        [UnityTest]
        public IEnumerator RefundUsesPaidPriceAndNextTrainingUsesNewPriceAndFullDuration()
        {
            recruitment.TryTrain(); yield return new WaitForSeconds(.1f); Set(settings, "woodCost", 35); Set(settings, "trainingSeconds", 2f);
            Assert.That(recruitment.CancellationRefund, Is.EqualTo(20)); Assert.That(recruitment.TryCancelTraining(), Is.True); Cancelled();
            Assert.That(recruitment.TryTrain(), Is.True); Assert.That(stockpile.Wood, Is.EqualTo(45));
            Assert.That(recruitment.RemainingSeconds, Is.EqualTo(2)); Assert.That(recruitment.CancellationRefund, Is.EqualTo(35));
            recruitment.TryCancelTraining(); Cancelled();
        }
        [UnityTest]
        public IEnumerator ResourceCallbacksSeeCommittedSlotAndCannotDuplicateRefundOrPurchase()
        {
            bool attemptedCancel = true, attemptedTrain = true;
            Action duringPayment = () => { attemptedCancel = recruitment.TryCancelTraining(); attemptedTrain = recruitment.TryTrain(); };
            stockpile.Changed += duringPayment; Assert.That(recruitment.TryTrain(), Is.True); stockpile.Changed -= duringPayment;
            Assert.That(attemptedCancel || attemptedTrain, Is.False); Assert.That(stockpile.Wood, Is.EqualTo(60));
            bool sawCleared = false;
            Action duringRefund = () => { sawCleared = !recruitment.IsTraining && recruitment.CancellationRefund == 0 && stockpile.Wood == 80; attemptedCancel = recruitment.TryCancelTraining(); attemptedTrain = recruitment.TryTrain(); };
            stockpile.Changed += duringRefund; Assert.That(recruitment.TryCancelTraining(), Is.True); stockpile.Changed -= duringRefund;
            Assert.That(sawCleared, Is.True); Assert.That(attemptedCancel || attemptedTrain, Is.False); Cancelled(); yield return null;
        }
        [UnityTest]
        public IEnumerator RefundOverflowKeepsPaidSlotUntilThereIsEnoughStorage()
        {
            recruitment.TryTrain(); Assert.That(stockpile.TryAddWood(int.MaxValue - stockpile.Wood), Is.True);
            float remaining = recruitment.RemainingSeconds;
            Assert.That(recruitment.CanCancelTraining, Is.False); Assert.That(recruitment.TryCancelTraining(), Is.False);
            Assert.That(recruitment.IsTraining, Is.True); Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(recruitment.CancellationRefund, Is.EqualTo(20));
            stockpile.TrySpendWood(20); Assert.That(recruitment.TryCancelTraining(), Is.True);
            Assert.That(stockpile.Wood, Is.EqualTo(int.MaxValue)); Assert.That(recruitment.IsTraining, Is.False); yield return null;
        }
        [UnityTest]
        public IEnumerator CompletedTrainingAtBlockedExitsCanBeCancelled()
        {
            Set(settings, "trainingSeconds", .1f);
            var blockers = Get<Transform[]>(recruitment, "exits").Select(exit => { var go = new GameObject("Cancel test blocker"); go.transform.position = exit.position; go.AddComponent<BoxCollider2D>(); return go; }).ToArray();
            recruitment.TryTrain(); yield return Until(() => recruitment.State == RecruitmentState.ExitBlocked);
            Assert.That(recruitment.TryCancelTraining(), Is.True); Cancelled(); foreach (var blocker in blockers) Object.Destroy(blocker);
            yield return new WaitForSeconds(.6f); Cancelled();
        }
        [UnityTest]
        public IEnumerator AtCapacitySlotCanBeCancelledWithoutChangingExistingArmy()
        {
            Set(settings, "trainingSeconds", .1f); recruitment.TryTrain(); Set(settings, "maximumAllies", 4);
            yield return Until(() => recruitment.State == RecruitmentState.AtCapacity); Assert.That(recruitment.TryCancelTraining(), Is.True); Cancelled();
            Assert.That(recruitment.CanTrain, Is.False);
        }
        [UnityTest]
        public IEnumerator PauseAndDisabledOwnersRejectCancellationWithoutLosingPayment()
        {
            recruitment.TryTrain(); Time.timeScale = 0; Assert.That(recruitment.TryCancelTraining(), Is.False);
            yield return new WaitForSecondsRealtime(.1f); Time.timeScale = 1;
            recruitment.enabled = false; Assert.That(recruitment.TryCancelTraining(), Is.False); recruitment.enabled = true;
            stockpile.enabled = false; Assert.That(recruitment.TryCancelTraining(), Is.False); stockpile.enabled = true;
            Assert.That(recruitment.CancellationRefund, Is.EqualTo(20)); Assert.That(recruitment.TryCancelTraining(), Is.True); Cancelled();
            recruitment.TryTrain(); invasion.enabled = false; Assert.That(recruitment.TryCancelTraining(), Is.False); invasion.enabled = true;
            // Disabling the invasion director ends the session; re-enable does not restore preparation.
            Assert.That(recruitment.TryCancelTraining(), Is.False); Assert.That(recruitment.CancellationRefund, Is.EqualTo(20)); Assert.That(stockpile.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator InvasionAndResultLockCancelUntilNextPreparation()
        {
            Set(settings, "trainingSeconds", 5f); recruitment.TryTrain(); invasion.Begin(); Assert.That(recruitment.TryCancelTraining(), Is.False);
            yield return Until(() => invasion.ActiveCount > 0); foreach (var enemy in invasion.Members.ToArray()) enemy.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled); Assert.That(recruitment.TryCancelTraining(), Is.False);
            Assert.That(invasion.PrepareNextWave(), Is.True); Assert.That(recruitment.TryCancelTraining(), Is.True); Cancelled();
        }
        [UnityTest]
        public IEnumerator DeployedSoldierHasNoRefundAndCannotBeCancelled()
        {
            Set(settings, "trainingSeconds", .1f); recruitment.TryTrain(); yield return Until(() => !recruitment.IsTraining);
            Assert.That(recruitment.CancellationRefund, Is.Zero); Assert.That(recruitment.TryCancelTraining(), Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(60)); Assert.That(invasion.DefenderCount, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator ActualButtonCancelsAndPreservesSelectionOrdersAndRally()
        {
            Set(settings, "trainingSeconds", 5f); var ally = GameObject.Find("Lord").GetComponent<UnitCombat>();
            yield return Click(Camera.main.WorldToScreenPoint(ally.transform.position)); ally.Unit.MoveTo(new Vector2(-16, 5)); long order = ally.Unit.CommandOrder;
            Assert.That(recruitment.TrySetRallyPoint(new Vector2(-6, -5)), Is.EqualTo(RallyPointResult.Available));
            var hud = Object.FindAnyObjectByType<RecruitmentHUD>(); var button = hud.transform.Find("Train Soldier").GetComponent<UnityEngine.UI.Button>();
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(); var point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            yield return Click(point); Assert.That(label.text, Is.EqualTo("CANCEL +20")); Assert.That(button.interactable, Is.True);
            yield return Click(point); Cancelled(); Assert.That(label.text, Is.EqualTo("TRAIN SOLDIER"));
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Does.Contain(ally.Unit)); Assert.That(ally.Unit.CommandOrder, Is.EqualTo(order));
            Assert.That(recruitment.HasRallyPoint, Is.True); Assert.That(recruitment.RallyPoint, Is.EqualTo(new Vector2(-6, -5)));
        }
        [UnityTest]
        public IEnumerator HudReenableHasOneClickListenerAndTracksPauseAvailability()
        {
            Set(settings, "trainingSeconds", 5f); var hud = Object.FindAnyObjectByType<RecruitmentHUD>();
            for (int i = 0; i < 3; i++) { hud.enabled = false; hud.enabled = true; }
            var button = hud.transform.Find("Train Soldier").GetComponent<UnityEngine.UI.Button>();
            button.onClick.Invoke(); Assert.That(recruitment.IsTraining, Is.True); Assert.That(stockpile.Wood, Is.EqualTo(60));
            Time.timeScale = 0; yield return null; Assert.That(button.interactable, Is.False);
            Time.timeScale = 1; yield return null; Assert.That(button.interactable, Is.True);
            button.onClick.Invoke(); Cancelled();
        }
        [UnityTest]
        public IEnumerator RestartDiscardsPendingRefundAndRestoresInitialEstate()
        {
            recruitment.TryTrain(); yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); stockpile = Object.FindAnyObjectByType<EstateStockpile>(); invasion = Object.FindAnyObjectByType<PortalInvasion>();
            Cancelled(); Assert.That(recruitment.TryCancelTraining(), Is.False);
        }
    }
}
