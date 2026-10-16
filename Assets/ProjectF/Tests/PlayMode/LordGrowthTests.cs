using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
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
    public sealed class LordGrowthTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private LordGrowth growth;
        private UnitCombat lord;
        private PortalInvasion invasion;
        private UnitRecruitment recruitment;
        private InvasionSettings invasionSettings;
        private LordGrowthSettings growthSettings;
        private RecruitmentSettings recruitmentSettings;
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj,value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field,BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            growth = Object.FindAnyObjectByType<LordGrowth>(); lord = growth.GetComponent<UnitCombat>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>(); recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            invasionSettings = Object.Instantiate(Get<InvasionSettings>(invasion,"settings"));
            Set(invasionSettings,"unitCount",1); Set(invasionSettings,"additionalUnitsPerWave",0);
            Set(invasionSettings,"preparationSeconds",0f); Set(invasion,"settings",invasionSettings);
            growthSettings = Object.Instantiate(growth.Settings); Set(growth,"settings",growthSettings);
            recruitmentSettings = Object.Instantiate(recruitment.Settings); Set(recruitment,"settings",recruitmentSettings);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var pause = Object.FindAnyObjectByType<GamePause>(); if(pause != null) pause.Resume(); Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            Object.Destroy(invasionSettings); Object.Destroy(growthSettings); Object.Destroy(recruitmentSettings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> done, float timeout = 5)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while(!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(done(),Is.True,"Lord growth scenario timed out.");
        }
        private IEnumerator Win()
        {
            Assert.That(invasion.Begin(),Is.True); float end = Time.realtimeSinceStartup + 5;
            while(invasion.State != InvasionState.Repelled && Time.realtimeSinceStartup < end)
            { foreach(var member in invasion.Members.ToArray()) member.ReceiveDamage(int.MaxValue); yield return null; }
            Assert.That(invasion.State,Is.EqualTo(InvasionState.Repelled));
        }
        private IEnumerator Click(Transform target)
        {
            var rect=(RectTransform)target;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator OnlyLordStartsWithGrowthAndNoFreeUpgrades()
        {
            Assert.That(Object.FindObjectsByType<LordGrowth>().Length,Is.EqualTo(1));
            Assert.That(growth.name,Is.EqualTo("Lord")); Assert.That(growth.Points,Is.Zero);
            Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.False); Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.False);
            Assert.That(recruitment.MaximumAllies,Is.EqualTo(8)); Assert.That(lord.AttackDamage,Is.EqualTo(15)); yield return null;
        }
        [UnityTest]
        public IEnumerator EachVictoryAwardsOnceAndSurvivesNextPreparation()
        {
            yield return Win(); Assert.That(growth.Points,Is.EqualTo(1));
            for(int i=0;i<3;i++) { growth.enabled=false; growth.enabled=true; }
            Assert.That(growth.Points,Is.EqualTo(1));
            Assert.That(invasion.PrepareNextWave(),Is.True); Assert.That(growth.Points,Is.EqualTo(1));
            yield return Win(); Assert.That(growth.Points,Is.EqualTo(2));
            Assert.That(invasion.PrepareNextWave(),Is.True); yield return Win(); Assert.That(growth.Points,Is.EqualTo(3));
            Assert.That(invasion.CanPrepareNextWave,Is.False); yield return null; Assert.That(growth.Points,Is.EqualTo(3));
        }
        [UnityTest]
        public IEnumerator DefeatAndCancellationNeverAwardPoints()
        {
            invasion.enabled=false; yield return null; Assert.That(growth.Points,Is.Zero);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            growth=Object.FindAnyObjectByType<LordGrowth>(); invasion=Object.FindAnyObjectByType<PortalInvasion>();
            foreach(var ally in Object.FindObjectsByType<UnitCombat>().Where(c=>c.IsPlayerControlled)) ally.ReceiveDamage(int.MaxValue);
            Assert.That(invasion.Begin(),Is.False); Assert.That(invasion.State,Is.EqualTo(InvasionState.Defeated)); Assert.That(growth.Points,Is.Zero);
        }
        [UnityTest]
        public IEnumerator MartialChangesActualLordHitsWithoutChangingOtherSoldiersOrSettings()
        {
            yield return Win(); var basic=Get<CombatSettings>(lord,"settings");
            Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.True); Assert.That(growth.Points,Is.Zero);
            Assert.That(lord.AttackDamage,Is.EqualTo(17)); Assert.That(basic.AttackDamage,Is.EqualTo(15));
            Assert.That(GameObject.Find("Test Unit 2").GetComponent<UnitCombat>().AttackDamage,Is.EqualTo(15));
            var prefab=Get<UnitCombat>(invasion,"enemyPrefab"); var enemy=Object.Instantiate(prefab);
            enemy.GetComponent<EnemyCombatAI>().enabled=false; Set(enemy,"retaliateWhenIdle",false);
            enemy.GetComponent<CommandableUnit>().InitializeNavigation(Object.FindAnyObjectByType<NavigationWorld2D>());
            enemy.transform.position=lord.transform.position+new Vector3(1.3f,0,0); enemy.gameObject.SetActive(true); Physics2D.SyncTransforms();
            int health=enemy.Health; Assert.That(lord.Attack(enemy),Is.True); yield return Until(()=>enemy.Health<health);
            lord.Unit.StopCommand(); Assert.That(health-enemy.Health,Is.EqualTo(17)); Assert.That(growth.Points,Is.Zero);
        }
        [UnityTest]
        public IEnumerator LeadershipUnlocksARealRecruitAtThePreviousCapacity()
        {
            Set(recruitmentSettings,"maximumAllies",4); Set(recruitmentSettings,"trainingSeconds",.1f);
            Assert.That(recruitment.CanTrain,Is.False); yield return Win();
            Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.True); Assert.That(recruitment.MaximumAllies,Is.EqualTo(5));
            Assert.That(recruitmentSettings.MaximumAllies,Is.EqualTo(4)); Assert.That(invasion.PrepareNextWave(),Is.True);
            Assert.That(recruitment.TryTrain(),Is.True); yield return Until(()=>!recruitment.IsTraining);
            Assert.That(recruitment.AlliedCount,Is.EqualTo(5)); Assert.That(recruitment.CanTrain,Is.False);
            Assert.That(recruitment.GetComponentInChildren<LordGrowth>(),Is.Null);
            StringAssert.Contains("ARMY 5/5",Object.FindAnyObjectByType<RecruitmentHUD>().transform.Find("Status").GetComponent<UnityEngine.UI.Text>().text);
        }
        [UnityTest]
        public IEnumerator RankLimitsAndInvalidChoiceDoNotSpendPoints()
        {
            Set(growthSettings,"maximumRank",1); Set(growthSettings,"pointsPerVictory",3); yield return Win();
            Assert.That(growth.TryUpgrade((LordDiscipline)99),Is.False); Assert.That(growth.Points,Is.EqualTo(3));
            Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.True); Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.False);
            Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.True); Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.False);
            Assert.That(growth.Points,Is.EqualTo(1)); Set(recruitmentSettings,"maximumAllies",64); Assert.That(recruitment.MaximumAllies,Is.EqualTo(64));
        }
        [UnityTest]
        public IEnumerator PauseAndActiveInvasionBlockInvestmentWithoutLosingPoints()
        {
            yield return Win(); var pause=Object.FindAnyObjectByType<GamePause>(); Assert.That(pause.TryPause(),Is.True);
            yield return null;
            Assert.That(Object.FindAnyObjectByType<LordGrowthHUD>().transform.Find("Martial").GetComponent<UnityEngine.UI.Button>().interactable,Is.False);
            Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.False); Assert.That(growth.Points,Is.EqualTo(1));
            pause.Resume(); Assert.That(invasion.PrepareNextWave(),Is.True); Assert.That(invasion.Begin(),Is.True);
            Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.False); Assert.That(growth.Points,Is.EqualTo(1));
            yield return Until(()=>invasion.State==InvasionState.Fighting);
            Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.False);
        }
        [UnityTest]
        public IEnumerator DisableRetainsPurchasedBonusesAndReenableDoesNotRewardAgain()
        {
            yield return Win(); Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.True);
            growth.enabled=false; Assert.That(recruitment.MaximumAllies,Is.EqualTo(9)); Assert.That(growth.CanInvest,Is.False);
            growth.enabled=true; Assert.That(growth.Points,Is.Zero); Assert.That(growth.LeadershipRank,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DeadLordCannotEarnOrInvestAndPurchasedCapacityIsRetained()
        {
            yield return Win(); Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.True);
            lord.ReceiveDamage(int.MaxValue); Assert.That(recruitment.MaximumAllies,Is.EqualTo(9)); Assert.That(growth.CanInvest,Is.False);
            Assert.That(invasion.PrepareNextWave(),Is.True); yield return Win(); Assert.That(growth.Points,Is.Zero);
        }
        [UnityTest]
        public IEnumerator SceneRestartResetsPointsRanksAndCapacity()
        {
            Set(growthSettings,"pointsPerVictory",2); yield return Win(); Assert.That(growth.TryUpgrade(LordDiscipline.Leadership),Is.True);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            growth=Object.FindAnyObjectByType<LordGrowth>(); Assert.That(growth.Points,Is.Zero); Assert.That(growth.LeadershipRank,Is.Zero);
            Assert.That(Object.FindAnyObjectByType<UnitRecruitment>().MaximumAllies,Is.EqualTo(8));
        }
        [UnityTest]
        public IEnumerator ChangedListenerCannotSpendAgainDuringOnePurchase()
        {
            Set(growthSettings,"pointsPerVictory",2); yield return Win(); bool nested=true;
            Action listener=()=>nested=growth.TryUpgrade(LordDiscipline.Leadership); growth.Changed+=listener;
            try { Assert.That(growth.TryUpgrade(LordDiscipline.Martial),Is.True); }
            finally { growth.Changed-=listener; }
            Assert.That(nested,Is.False); Assert.That(growth.Points,Is.EqualTo(1)); Assert.That(growth.LeadershipRank,Is.Zero);
        }
        [UnityTest]
        public IEnumerator DisableAndCancelledInvasionDoNotPublishUnchangedGrowth()
        {
            int notifications=0; Action listener=()=>notifications++; growth.Changed+=listener;
            try
            {
                Assert.That(invasion.Begin(),Is.True);
                growth.enabled=false; growth.enabled=true;
                invasion.enabled=false; yield return null;
                Assert.That(notifications,Is.Zero,"Unchanged growth must not wake consumers while scene dependencies are being disabled.");
            }
            finally { growth.Changed-=listener; }
        }
        [UnityTest]
        public IEnumerator RealButtonsSpendChoicesAndConsumeWorldInput()
        {
            Set(growthSettings,"pointsPerVictory",2); yield return Win();
            var hud=Object.FindAnyObjectByType<LordGrowthHUD>(); var commands=Object.FindAnyObjectByType<UnitCommandController>();
            Vector2 point=Camera.main.WorldToScreenPoint(lord.transform.position);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return null; yield return null;
            Assert.That(commands.Selected,Is.EquivalentTo(new[]{lord.Unit}));
            for(int i=0;i<2;i++) { hud.enabled=false; hud.enabled=true; }
            var units=Object.FindObjectsByType<CommandableUnit>(); var orders=units.Select(u=>u.CommandOrder).ToArray();
            yield return Click(hud.transform.Find("Martial")); Assert.That(commands.PointerOverUI(),Is.True);
            Assert.That(growth.MartialRank,Is.EqualTo(1)); Assert.That(growth.Points,Is.EqualTo(1));
            yield return Click(hud.transform.Find("Leadership")); Assert.That(growth.LeadershipRank,Is.EqualTo(1)); Assert.That(growth.Points,Is.Zero);
            Assert.That(units.Select(u=>u.CommandOrder).ToArray(),Is.EqualTo(orders));
            Assert.That(commands.Selected,Is.EquivalentTo(new[]{lord.Unit}));
            Assert.That(hud.transform.Find("Martial").GetComponent<UnityEngine.UI.Button>().interactable,Is.False);
            StringAssert.Contains("POINTS 0",hud.transform.Find("Status").GetComponent<UnityEngine.UI.Text>().text);
        }
    }
}
