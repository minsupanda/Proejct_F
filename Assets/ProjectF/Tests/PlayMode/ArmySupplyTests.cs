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
    public sealed class ArmySupplyTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private ArmySupply supply;
        private ArmySupplySettings settings;
        private EstateStockpile stockpile;
        private PortalInvasion invasion;
        private InvasionSettings invasionSettings;
        private UnitCombat lord;
        private RecruitmentSettings recruitmentSettings;
        private FarmSettings farmSettings;
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            supply = Object.FindAnyObjectByType<ArmySupply>(); stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>(); lord = GameObject.Find("Lord").GetComponent<UnitCombat>();
            Assert.That(supply, Is.Not.Null, "Configure ArmySupplySetup before running scene tests.");
            settings = Object.Instantiate(supply.Settings); Set(supply, "settings", settings);
            invasionSettings = Object.Instantiate(Get<InvasionSettings>(invasion, "settings"));
            Set(invasionSettings, "preparationSeconds", 0f); Set(invasionSettings, "unitCount", 1);
            Set(invasionSettings, "additionalUnitsPerWave", 0); Set(invasion, "settings", invasionSettings);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var pause = Object.FindAnyObjectByType<GamePause>(); if (pause != null) pause.Resume(); Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(settings); Object.Destroy(invasionSettings); Object.Destroy(recruitmentSettings); Object.Destroy(farmSettings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> done, float timeout = 5)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(done(), Is.True, "Army supply scenario timed out.");
        }
        private IEnumerator Repel()
        {
            yield return Until(() => invasion.ActiveCount > 0);
            foreach (var enemy in invasion.Members.ToArray()) enemy.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
        }
        private static void Place(UnitCombat unit, Vector2 point)
        { unit.Unit.StopCommand(); unit.transform.position = point; unit.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }
        [UnityTest]
        public IEnumerator FullRationsChargeOnceAndPreserveNormalDamage()
        {
            stockpile.TryAddFood(10); Assert.That(supply.RequiredFood, Is.EqualTo(4)); Assert.That(supply.PreviewDamagePercent, Is.EqualTo(100));
            Assert.That(invasion.Begin(), Is.True); Assert.That(invasion.Begin(), Is.False);
            Assert.That(stockpile.Food, Is.EqualTo(6)); Assert.That(supply.LastConsumedFood, Is.EqualTo(4));
            Assert.That(lord.AttackDamage, Is.EqualTo(lord.UnmodifiedAttackDamage));
            yield return Until(() => invasion.ActiveCount > 0);
            Assert.That(stockpile.Food, Is.EqualTo(6)); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(100));
        }
        [UnityTest]
        public IEnumerator PartialRationsConsumeAvailableFoodAndFreezeTheCombatRate()
        {
            stockpile.TryAddFood(2); Assert.That(supply.PreviewDamagePercent, Is.EqualTo(87)); invasion.Begin();
            Assert.That(stockpile.Food, Is.Zero); Assert.That(supply.LastConsumedFood, Is.EqualTo(2));
            Assert.That(lord.SupplyDamagePercent, Is.EqualTo(87));
            Set(settings, "foodPerDefender", 100); Set(settings, "unfedDamagePercent", 25); stockpile.TryAddFood(20);
            yield return Until(() => invasion.ActiveCount > 0);
            Assert.That(supply.LastRequiredFood, Is.EqualTo(4)); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(87));
            Assert.That(stockpile.Food, Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator EmptyFoodStillStartsInvasionAndReducesActualAlliedStrike()
        {
            Assert.That(invasion.Begin(), Is.True); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(75));
            yield return Until(() => invasion.ActiveCount > 0);
            var enemy = invasion.Members[0]; enemy.GetComponent<EnemyCombatAI>().enabled = false; Set(enemy, "retaliateWhenIdle", false);
            Place(lord, new Vector2(-1, 0)); Place(enemy, new Vector2(.1f, 0));
            int health = enemy.Health, expected = lord.AttackDamage;
            Assert.That(expected, Is.LessThan(lord.UnmodifiedAttackDamage)); Assert.That(enemy.SupplyDamagePercent, Is.EqualTo(100));
            Action<UnitCombat> stopAfterHit = _ => lord.Unit.StopCommand(); lord.Attacked += stopAfterHit;
            try { Assert.That(lord.Attack(enemy), Is.True); yield return Until(() => enemy.Health < health); }
            finally { lord.Attacked -= stopAfterHit; }
            Assert.That(health - enemy.Health, Is.EqualTo(expected));
        }
        [UnityTest]
        public IEnumerator RecruitsIncreaseRationsAndReceiveTheSameShortagePenalty()
        {
            var recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            recruitmentSettings = Object.Instantiate(recruitment.Settings); Set(recruitmentSettings, "trainingSeconds", .1f); Set(recruitment, "settings", recruitmentSettings);
            stockpile.TryAddFood(4); recruitment.TryTrain(); yield return Until(() => recruitment.AlliedCount == 5);
            Assert.That(supply.RequiredFood, Is.EqualTo(5)); invasion.Begin();
            var recruit = recruitment.GetComponentsInChildren<UnitCombat>().Single();
            Assert.That(recruit.SupplyDamagePercent, Is.EqualTo(75)); Assert.That(supply.LastRequiredFood, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator DeadAndDisabledDefendersDoNotConsumeRations()
        {
            GameObject.Find("Test Unit 2").GetComponent<UnitCombat>().ReceiveDamage(int.MaxValue);
            GameObject.Find("Test Unit 3").SetActive(false); stockpile.TryAddFood(2);
            Assert.That(supply.RequiredFood, Is.EqualTo(2)); invasion.Begin();
            Assert.That(supply.LastConsumedFood, Is.EqualTo(2)); Assert.That(stockpile.Food, Is.Zero);
            Assert.That(lord.SupplyDamagePercent, Is.EqualTo(100)); yield return null;
        }
        [UnityTest]
        public IEnumerator NextWaveRecomputesRationsAndResultRestoresDamage()
        {
            stockpile.TryAddFood(2); invasion.Begin(); yield return Repel();
            Assert.That(lord.SupplyDamagePercent, Is.EqualTo(100));
            Assert.That(invasion.PrepareNextWave(), Is.True); stockpile.TryAddFood(4);
            Assert.That(invasion.Begin(), Is.True); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(100));
            Assert.That(supply.LastConsumedFood, Is.EqualTo(4)); Assert.That(stockpile.Food, Is.Zero);
        }
        [UnityTest]
        public IEnumerator PauseBlocksStartingAndDoesNotIssueRationsAgainOnResume()
        {
            stockpile.TryAddFood(8); var pause = Object.FindAnyObjectByType<GamePause>(); pause.TryPause();
            Assert.That(invasion.Begin(), Is.False); Assert.That(stockpile.Food, Is.EqualTo(8));
            pause.Resume(); invasion.Begin(); pause.TryPause(); yield return new WaitForSecondsRealtime(.15f); pause.Resume();
            yield return null; Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(supply.LastConsumedFood, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator TrainingRefundSpaceIsNotAvailableToTheArmy()
        {
            stockpile.TryAddFood(4); Object.FindAnyObjectByType<UnitRecruitment>().TryTrain();
            Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4)); invasion.Begin();
            Assert.That(supply.LastConsumedFood, Is.Zero); Assert.That(stockpile.ReservedFoodCapacity, Is.EqualTo(4));
            Assert.That(supply.CurrentDamagePercent, Is.EqualTo(75)); yield return null;
        }
        [UnityTest]
        public IEnumerator ResourceObserversSeeCommittedSupplyAndCannotStartTwice()
        {
            stockpile.TryAddFood(4); int notifications = 0;
            Action observer = () =>
            {
                notifications++; Assert.That(stockpile.Food, Is.Zero);
                Assert.That(invasion.State, Is.EqualTo(InvasionState.Preparing));
                Assert.That(supply.LastConsumedFood, Is.EqualTo(4)); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(100));
                Assert.That(invasion.Begin(), Is.False);
            };
            stockpile.Changed += observer;
            try { Assert.That(invasion.Begin(), Is.True); }
            finally { stockpile.Changed -= observer; }
            Assert.That(notifications, Is.EqualTo(1)); yield return null;
        }
        [UnityTest]
        public IEnumerator DisabledStockpileKeepsFoodAndCannotSupplyTheBattleLater()
        {
            stockpile.TryAddFood(4); stockpile.enabled = false; invasion.Begin();
            Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(75));
            stockpile.enabled = true; yield return Until(() => invasion.ActiveCount > 0);
            Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(75));
        }
        [UnityTest]
        public IEnumerator SupplyDisableAndReenableRetainTheIssuedRations()
        {
            stockpile.TryAddFood(2); invasion.Begin(); supply.enabled = false;
            Assert.That(lord.SupplyDamagePercent, Is.EqualTo(100)); supply.enabled = true;
            Assert.That(lord.SupplyDamagePercent, Is.EqualTo(87)); Assert.That(stockpile.Food, Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator FarmHarvestAndRealStartButtonIssueTheExpectedRations()
        {
            var construction = Object.FindAnyObjectByType<WallConstruction>();
            farmSettings = Object.Instantiate(construction.FarmSettings); Set(farmSettings, "harvestSeconds", .1f); Set(construction, "farmSettings", farmSettings);
            construction.TryBuildFarm(new Vector2(-1, 0)); yield return Until(() => stockpile.Food >= 4);
            Object.FindAnyObjectByType<FarmProduction>().enabled = false;
            var button = Object.FindAnyObjectByType<InvasionHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            var rect = (RectTransform)button.transform; Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            Assert.That(invasion.State, Is.Not.EqualTo(InvasionState.Ready)); Assert.That(supply.LastConsumedFood, Is.EqualTo(4));
            var text = Object.FindAnyObjectByType<ArmySupplyHUD>().GetComponentInChildren<UnityEngine.UI.Text>().text;
            StringAssert.Contains("ATK 100%", text); StringAssert.Contains("RATIONS 4 / 4", text);
        }
        [UnityTest]
        public IEnumerator SceneRestartClearsIssuedRationsAndRestoresReadyDamage()
        {
            invasion.Begin(); yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            var reloaded = Object.FindAnyObjectByType<ArmySupply>();
            Assert.That(reloaded.LastConsumedFood, Is.Zero); Assert.That(reloaded.LastRequiredFood, Is.Zero);
            Assert.That(reloaded.CurrentDamagePercent, Is.EqualTo(100)); Assert.That(reloaded.RequiredFood, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator ZeroCostAndLargeCountsAreBoundedWithoutOverflow()
        {
            Set(settings, "foodPerDefender", 100); Assert.That(settings.RequiredFood(int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(settings.DamagePercent(int.MaxValue, int.MaxValue), Is.EqualTo(100));
            Set(settings, "foodPerDefender", 0); Assert.That(invasion.Begin(), Is.True);
            Assert.That(supply.LastRequiredFood, Is.Zero); Assert.That(supply.CurrentDamagePercent, Is.EqualTo(100)); yield return null;
        }
        [UnityTest]
        public IEnumerator MissingOptionalSupplyKeepsThePreviousCombatBehavior()
        {
            Set(invasion, "supply", null); stockpile.TryAddFood(4); invasion.Begin();
            Assert.That(stockpile.Food, Is.EqualTo(4)); Assert.That(lord.SupplyDamagePercent, Is.EqualTo(100)); yield return null;
        }
    }
}
