using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
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
    public sealed class RecruitmentTests
    {
        private UnitRecruitment recruitment;
        private RecruitmentSettings settings;
        private InvasionSettings invasionSettings;
        private PortalInvasion invasion;
        private EstateStockpile stockpile;
        private UnitCombat[] originalAllies;
        private InputTestFixture fixture;
        private Mouse mouse;
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); invasion = Object.FindAnyObjectByType<PortalInvasion>();
            stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            originalAllies = Object.FindObjectsByType<UnitCombat>().Where(u => u.Faction == UnitFaction.Player).ToArray();
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
        private IEnumerator Until(Func<bool> predicate, float seconds = 5)
        {
            float end = Time.time + seconds;
            while (!predicate() && Time.time < end) yield return null;
            Assert.That(predicate(), Is.True, "Recruitment timed out.");
        }
        private UnitCombat NewSoldier() => recruitment.GetComponentsInChildren<UnitCombat>().Single();
        private IEnumerator Train()
        {
            Assert.That(recruitment.TryTrain(), Is.True);
            yield return Until(() => !recruitment.IsTraining);
        }
        private void Place(UnitCombat unit, Vector2 position)
        {
            unit.Unit.MoveTo(position); unit.transform.position = position; unit.GetComponent<Rigidbody2D>().position = position; Physics2D.SyncTransforms();
        }
        private IEnumerator Click(Vector2 point, bool right = false)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = (ushort)(right ? 2 : 1) }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        }
        private IEnumerator Repel()
        {
            Assert.That(invasion.Begin(), Is.True);
            yield return Until(() => invasion.ActiveCount > 0);
            foreach (var unit in invasion.Members.ToArray()) unit.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Repelled);
        }

        [UnityTest]
        public IEnumerator PaidSlotDeploysOneHealthyNavigableAlliedSoldier()
        {
            Assert.That(recruitment.AlliedCount, Is.EqualTo(4));
            Assert.That(recruitment.TryTrain(), Is.True); Assert.That(stockpile.Wood, Is.EqualTo(60));
            Assert.That(recruitment.TryTrain(), Is.False);
            yield return Until(() => !recruitment.IsTraining);
            var soldier = NewSoldier();
            Assert.That(soldier.IsAlive, Is.True); Assert.That(soldier.Health, Is.EqualTo(soldier.MaxHealth));
            Assert.That(soldier.Faction, Is.EqualTo(UnitFaction.Player)); Assert.That(invasion.DefenderCount, Is.EqualTo(5));
            Assert.That(soldier.GetComponent<WoodGatherer>().CanGather, Is.True);
            var start = soldier.Unit.Position; soldier.Unit.MoveTo(start + Vector2.up * 2);
            yield return Until(() => Vector2.Distance(start, soldier.Unit.Position) > 1);
            Assert.That(stockpile.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator InsufficientWoodPauseCapacityAndDisabledRejectWithoutSpending()
        {
            stockpile.TrySpendWood(65); Assert.That(recruitment.TryTrain(), Is.False); Assert.That(stockpile.Wood, Is.EqualTo(15));
            stockpile.TryAddWood(65); Time.timeScale = 0; Assert.That(recruitment.TryTrain(), Is.False); Time.timeScale = 1;
            Set(settings, "maximumAllies", 4); Assert.That(recruitment.TryTrain(), Is.False); Set(settings, "maximumAllies", 8);
            recruitment.enabled = false; Assert.That(recruitment.TryTrain(), Is.False); recruitment.enabled = true;
            Assert.That(stockpile.Wood, Is.EqualTo(80)); Assert.That(recruitment.IsTraining, Is.False); yield return null;
        }
        [UnityTest]
        public IEnumerator ResourceCallbacksCannotPurchaseAnotherSlotOrLosePaidTraining()
        {
            bool duplicate = true;
            Action callback = () => { duplicate = recruitment.TryTrain(); invasion.Begin(); };
            stockpile.Changed += callback;
            Assert.That(recruitment.TryTrain(), Is.True); stockpile.Changed -= callback;
            Assert.That(duplicate, Is.False); Assert.That(recruitment.IsTraining, Is.True); Assert.That(stockpile.Wood, Is.EqualTo(60));
            yield return new WaitForSeconds(.25f);
            Assert.That(recruitment.State, Is.EqualTo(RecruitmentState.Suspended)); Assert.That(invasion.DefenderCount, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator TrainingPausesAcrossInvasionAndResumesNextPreparationWithoutSecondCharge()
        {
            Set(settings, "trainingSeconds", .6f); recruitment.TryTrain(); yield return new WaitForSeconds(.15f);
            float remaining = recruitment.RemainingSeconds;
            yield return Repel(); yield return new WaitForSeconds(.25f);
            Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining).Within(.05f));
            Assert.That(recruitment.State, Is.EqualTo(RecruitmentState.Suspended)); Assert.That(recruitment.TryTrain(), Is.False);
            invasion.PrepareNextWave(); yield return Until(() => !recruitment.IsTraining);
            Assert.That(invasion.DefenderCount, Is.EqualTo(5)); Assert.That(stockpile.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator PaidDurationDoesNotChangeWithSettingsAndPauseOrDisableKeepsProgress()
        {
            Set(settings, "trainingSeconds", .5f); recruitment.TryTrain();
            Set(settings, "trainingSeconds", 20f); Set(settings, "woodCost", 75);
            Time.timeScale = 0; float remaining = recruitment.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.2f); Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining));
            Time.timeScale = 1; recruitment.enabled = false;
            yield return new WaitForSeconds(.2f); Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining));
            recruitment.enabled = true; yield return Until(() => !recruitment.IsTraining, 2);
            Assert.That(stockpile.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator BlockedExitsWaitThenDeployWithoutOverlapOrExtraCost()
        {
            var exits = Get<Transform[]>(recruitment, "exits");
            var blockers = exits.Select(e => { var go = new GameObject("Recruitment test blocker"); go.transform.position = e.position; go.AddComponent<BoxCollider2D>().size = Vector2.one; return go; }).ToArray();
            recruitment.TryTrain(); yield return Until(() => recruitment.State == RecruitmentState.ExitBlocked);
            Assert.That(invasion.DefenderCount, Is.EqualTo(4)); Assert.That(stockpile.Wood, Is.EqualTo(60));
            Object.Destroy(blockers[1]); yield return Until(() => !recruitment.IsTraining);
            Assert.That(Vector2.Distance(NewSoldier().Unit.Position, exits[1].position), Is.LessThan(.1f));
            Assert.That(stockpile.Wood, Is.EqualTo(60));
        }
        [UnityTest]
        public IEnumerator CapacityIsCheckedAgainAtDeploymentAndDeathOpensSlot()
        {
            recruitment.TryTrain(); Set(settings, "maximumAllies", 4);
            yield return Until(() => recruitment.State == RecruitmentState.AtCapacity);
            Assert.That(invasion.DefenderCount, Is.EqualTo(4)); originalAllies[0].ReceiveDamage(int.MaxValue);
            yield return Until(() => !recruitment.IsTraining);
            Assert.That(invasion.DefenderCount, Is.EqualTo(4)); Assert.That(recruitment.CanTrain, Is.False);
            NewSoldier().ReceiveDamage(int.MaxValue); yield return null;
            Assert.That(recruitment.CanTrain, Is.True); Assert.That(recruitment.GetComponentsInChildren<UnitCombat>(true), Is.Empty);
            yield return Train(); Assert.That(invasion.DefenderCount, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator DefaultEightAllyCapCountsOriginalsAndSpendsExactlyFourPurchases()
        {
            for (int i = 0; i < 4; i++)
            {
                yield return Train();
                var soldier = recruitment.GetComponentsInChildren<UnitCombat>().OrderBy(u => u.name).Last();
                Place(soldier, new Vector2(-17 + i * 2, -6));
                Assert.That(invasion.DefenderCount, Is.EqualTo(5 + i));
            }
            Assert.That(stockpile.Wood, Is.Zero); Assert.That(recruitment.CanTrain, Is.False);
            stockpile.TryAddWood(20); Assert.That(recruitment.TryTrain(), Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(20));
        }
        [UnityTest]
        public IEnumerator RecruitedSoldierKeepsDefenseAliveAfterEveryOriginalAllyDies()
        {
            yield return Train(); var soldier = NewSoldier();
            foreach (var ally in originalAllies) ally.ReceiveDamage(int.MaxValue);
            Assert.That(invasion.DefenderCount, Is.EqualTo(1)); Assert.That(invasion.Begin(), Is.True);
            yield return Until(() => invasion.ActiveCount > 0);
            Assert.That(invasion.State, Is.Not.EqualTo(InvasionState.Defeated));
            var enemy = invasion.Members[0]; Place(enemy, soldier.Unit.Position + Vector2.up * 1.1f);
            yield return Until(() => enemy.Target == soldier);
            yield return Until(() => soldier.Health < soldier.MaxHealth);
            soldier.ReceiveDamage(int.MaxValue); yield return Until(() => invasion.State == InvasionState.Defeated);
            Assert.That(invasion.DefenderCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator ActualRecruitButtonDoesNotSelectWorldAndSoldierAcceptsGatherAndMoveInput()
        {
            var button = Object.FindAnyObjectByType<RecruitmentHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
            yield return Until(() => !recruitment.IsTraining);
            Assert.That(stockpile.Wood, Is.EqualTo(60)); var soldier = NewSoldier();
            var commands = Object.FindAnyObjectByType<UnitCommandController>(); Assert.That(commands.Selected, Is.Empty);
            var tree = Object.FindObjectsByType<WoodResourceNode>().OrderBy(n => n.name).First();
            Place(soldier, (Vector2)tree.transform.position + Vector2.right * 1.25f);
            yield return Click(Camera.main.WorldToScreenPoint(soldier.transform.position));
            Assert.That(commands.Selected, Does.Contain(soldier.Unit));
            yield return Click(Camera.main.WorldToScreenPoint(tree.transform.position), true);
            yield return Until(() => stockpile.Wood >= 65, 5);
            yield return Click(Camera.main.WorldToScreenPoint(new Vector3(-5, 0, 0)), true);
            Assert.That(soldier.GetComponent<WoodGatherer>().Target, Is.Null);
            Assert.That(soldier.Unit.HasDestination, Is.True);
        }
        [UnityTest]
        public IEnumerator RecruitedSoldierCanFightAndSurvivesRoundTransitionWithDamage()
        {
            yield return Train(); var soldier = NewSoldier(); soldier.ReceiveDamage(7);
            invasion.Begin(); yield return Until(() => invasion.ActiveCount > 0);
            var enemy = invasion.Members[0]; enemy.GetComponent<EnemyCombatAI>().enabled = false;
            Place(enemy, new Vector2(-12, 1.15f)); Place(soldier, new Vector2(-12, 0));
            Assert.That(soldier.Attack(enemy), Is.True); yield return Until(() => enemy.Health < enemy.MaxHealth);
            enemy.ReceiveDamage(int.MaxValue); yield return Until(() => invasion.State == InvasionState.Repelled);
            int health = soldier.Health; invasion.PrepareNextWave();
            Assert.That(soldier.IsAlive, Is.True); Assert.That(soldier.Health, Is.EqualTo(health)); Assert.That(invasion.DefenderCount, Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator DefeatKeepsPendingTrainingSuspendedAndRestartResetsEstateAndRoster()
        {
            Set(settings, "trainingSeconds", 5f); recruitment.TryTrain(); invasion.Begin();
            foreach (var ally in originalAllies) ally.ReceiveDamage(int.MaxValue);
            yield return Until(() => invasion.State == InvasionState.Defeated);
            yield return new WaitForSeconds(.2f); Assert.That(recruitment.State, Is.EqualTo(RecruitmentState.Suspended));
            Assert.That(recruitment.TryTrain(), Is.False); Assert.That(invasion.DefenderCount, Is.Zero);
            var button = Object.FindAnyObjectByType<InvasionHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            var old = recruitment;
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.transform.position));
            yield return Until(() => old == null);
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            Assert.That(recruitment.IsTraining, Is.False); Assert.That(recruitment.AlliedCount, Is.EqualTo(4));
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
        }
    }
}
