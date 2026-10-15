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
    public sealed class ProficiencyTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private UnitCombat soldier, enemy, peer, lord;
        private SoldierProficiency proficiency;
        private CombatSettings soldierSettings, enemySettings;
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Place(UnitCombat unit, Vector2 point)
        { unit.Unit.StopCommand(); unit.transform.position = point; unit.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms(); }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            soldier = GameObject.Find("Test Unit 2").GetComponent<UnitCombat>(); proficiency = soldier.GetComponent<SoldierProficiency>();
            peer = GameObject.Find("Test Unit 3").GetComponent<UnitCombat>(); lord = GameObject.Find("Lord").GetComponent<UnitCombat>();
            Place(lord, new Vector2(-9, 1)); Place(soldier, new Vector2(-6, 1));
            soldierSettings = Object.Instantiate(Get<CombatSettings>(soldier, "settings")); Set(soldierSettings, "attackInterval", .1f); Set(soldier, "settings", soldierSettings);
            var invasion = Object.FindAnyObjectByType<PortalInvasion>(); var prefab = Get<UnitCombat>(invasion, "enemyPrefab");
            enemy = Object.Instantiate(prefab, new Vector3(-4.7f, 1, 0), Quaternion.identity);
            enemySettings = Object.Instantiate(Get<CombatSettings>(enemy, "settings")); Set(enemySettings, "maxHealth", 10000); Set(enemy, "settings", enemySettings);
            Set(enemy, "retaliateWhenIdle", false); enemy.GetComponent<EnemyCombatAI>().enabled = false;
            enemy.GetComponent<CommandableUnit>().InitializeNavigation(Object.FindAnyObjectByType<NavigationWorld2D>()); enemy.gameObject.SetActive(true);
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var pause = Object.FindAnyObjectByType<GamePause>(); if (pause != null) pause.Resume(); Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene"); Object.Destroy(soldierSettings); Object.Destroy(enemySettings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> done, float seconds = 5)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(done(), Is.True, "Proficiency scenario timed out.");
        }
        private IEnumerator Earn(int experience)
        {
            Assert.That(soldier.Attack(enemy), Is.True); yield return Until(() => proficiency.Experience >= experience); soldier.Unit.StopCommand();
        }
        private IEnumerator Click(Vector2 world, ushort buttons = 1)
        {
            var point = (Vector2)Camera.main.WorldToScreenPoint(world);
            InputSystem.QueueStateEvent(mouse, new MouseState {position=point}); yield return null; yield return null;
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().PointerOverUI(), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState {position=point,buttons=buttons}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState {position=point}); yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator InitialSoldiersAreFreshAndLordAndEnemiesHaveNoProficiency()
        {
            Assert.That(Object.FindObjectsByType<SoldierProficiency>().Length, Is.EqualTo(3));
            Assert.That(proficiency.Experience, Is.Zero); Assert.That(proficiency.Rank, Is.EqualTo(ProficiencyRank.Recruit));
            Assert.That(proficiency.DamageBonus, Is.Zero); Assert.That(proficiency.NextThreshold, Is.EqualTo(5));
            Assert.That(lord.GetComponent<SoldierProficiency>(), Is.Null); Assert.That(enemy.GetComponent<SoldierProficiency>(), Is.Null);
            yield return null;
        }
        [UnityTest]
        public IEnumerator ThresholdStrikeUsesOldDamageAndFollowingStrikeUsesBonus()
        {
            int basic = soldier.AttackDamage, startHealth = enemy.Health;
            yield return Earn(5);
            Assert.That(startHealth - enemy.Health, Is.EqualTo(basic * 5));
            Assert.That(proficiency.Rank, Is.EqualTo(ProficiencyRank.Trained)); Assert.That(proficiency.DamageBonus, Is.EqualTo(2));
            startHealth = enemy.Health; yield return Earn(6); Assert.That(startHealth - enemy.Health, Is.EqualTo(basic + 2));
            Assert.That(soldierSettings.AttackDamage, Is.EqualTo(basic)); Assert.That(peer.AttackDamage, Is.EqualTo(basic)); Assert.That(lord.AttackDamage, Is.EqualTo(basic));
        }
        [UnityTest]
        public IEnumerator VeteranCapsExperienceAndRepeatedOrdersCannotAccelerateStrikes()
        {
            yield return Earn(15); Assert.That(proficiency.Rank, Is.EqualTo(ProficiencyRank.Veteran)); Assert.That(proficiency.DamageBonus, Is.EqualTo(4));
            int health = enemy.Health;
            for (int i = 0; i < 50; i++) soldier.Attack(enemy);
            yield return new WaitForSeconds(.35f); soldier.Unit.StopCommand();
            Assert.That(proficiency.Experience, Is.EqualTo(15)); Assert.That(health-enemy.Health, Is.InRange(soldier.AttackDamage, soldier.AttackDamage * 4));
        }
        [UnityTest]
        public IEnumerator KillingStrikeCountsOnceAndDamageTakenDoesNotTrain()
        {
            soldier.ReceiveDamage(1); yield return null; Assert.That(proficiency.Experience, Is.Zero);
            enemy.ReceiveDamage(enemy.Health-1); yield return Earn(1); Assert.That(enemy.IsAlive, Is.False);
            Assert.That(soldier.Attack(enemy), Is.False); yield return new WaitForSeconds(.3f); Assert.That(proficiency.Experience, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator WallsAndOutOfRangeTargetsCannotGrantExperience()
        {
            var wall = new GameObject("Blocking wall"); wall.transform.position = new Vector3(-5.35f,1,0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f,4);
            soldier.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll; Physics2D.SyncTransforms();
            soldier.Attack(enemy); yield return new WaitForSeconds(.4f); Assert.That(proficiency.Experience, Is.Zero);
            Object.Destroy(wall); Place(enemy,new Vector2(15,1)); yield return new WaitForSeconds(.3f); Assert.That(proficiency.Experience, Is.Zero);
        }
        [UnityTest]
        public IEnumerator DisableRetainsProgressAndReenableHasOneSubscription()
        {
            yield return Earn(5); proficiency.enabled = false; Assert.That(soldier.AttackDamage, Is.EqualTo(soldierSettings.AttackDamage));
            soldier.Attack(enemy); yield return new WaitForSeconds(.25f); soldier.Unit.StopCommand(); Assert.That(proficiency.Experience, Is.EqualTo(5));
            for(int i=0;i<3;i++) { proficiency.enabled=true; proficiency.enabled=false; }
            proficiency.enabled=true; Assert.That(proficiency.DamageBonus, Is.EqualTo(2)); yield return Earn(6); Assert.That(proficiency.Experience, Is.EqualTo(6));
            soldier.gameObject.SetActive(false); soldier.gameObject.SetActive(true); Assert.That(proficiency.Experience, Is.EqualTo(6));
        }
        [UnityTest]
        public IEnumerator PauseKeepsProgressAndPreventsHitsUntilResumed()
        {
            yield return Earn(2); soldier.Attack(enemy); var pause=Object.FindAnyObjectByType<GamePause>(); Assert.That(pause.TryPause(), Is.True);
            int health=enemy.Health; yield return new WaitForSecondsRealtime(.3f); Assert.That(proficiency.Experience, Is.EqualTo(2)); Assert.That(enemy.Health, Is.EqualTo(health));
            pause.Resume(); yield return Until(()=>proficiency.Experience>2); soldier.Unit.StopCommand();
        }
        [UnityTest]
        public IEnumerator ActualInputAndSelectionShowIndividualAndGroupProgress()
        {
            yield return Click(soldier.Unit.Position); yield return Click(enemy.Unit.Position,2); yield return Until(()=>proficiency.Experience>=5); soldier.Unit.StopCommand();
            yield return new WaitForSecondsRealtime(.13f);
            var label=Object.FindAnyObjectByType<SelectionStatusHUD>().GetComponentInChildren<UnityEngine.UI.Text>();
            StringAssert.Contains("TRAINED",label.text); StringAssert.Contains("ATK +2",label.text);
            var keyboard=InputSystem.AddDevice<Keyboard>(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift)); yield return null;
            yield return Click(peer.Unit.Position); InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return new WaitForSecondsRealtime(.13f);
            StringAssert.Contains("SKILL R1 T1 V0",label.text);
            yield return Click(lord.Unit.Position); yield return new WaitForSecondsRealtime(.13f); StringAssert.Contains("PROFICIENCY N/A",label.text);
        }
        [UnityTest]
        public IEnumerator NewRecruitAndSceneRestartStartFromZero()
        {
            yield return Earn(5); var recruitment=Object.FindAnyObjectByType<UnitRecruitment>(); Assert.That(recruitment.TryTrain(),Is.True);
            yield return Until(()=>!recruitment.IsTraining,7);
            var recruit=recruitment.GetComponentInChildren<SoldierProficiency>(); Assert.That(recruit,Is.Not.Null); Assert.That(recruit.Experience,Is.Zero); Assert.That(recruit.DamageBonus,Is.Zero);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindObjectsByType<SoldierProficiency>().All(p=>p.Experience==0),Is.True);
        }
        [UnityTest]
        public IEnumerator AttackMoveTrainsWithoutReplacingItsDestination()
        {
            var advance=soldier.GetComponent<AttackMoveOrder>(); var destination=new Vector2(8,1);
            Assert.That(advance.Begin(destination),Is.True); yield return Until(()=>proficiency.Experience>=5);
            Assert.That(advance.IsActive,Is.True); Assert.That(advance.Destination,Is.EqualTo(destination)); Assert.That(proficiency.DamageBonus,Is.EqualTo(2));
            soldier.Unit.StopCommand();
        }
        [UnityTest]
        public IEnumerator ProficiencySurvivesPreparingTheNextInvasion()
        {
            yield return Earn(5); var invasion=Object.FindAnyObjectByType<PortalInvasion>(); Assert.That(invasion.Begin(),Is.True);
            float end=Time.realtimeSinceStartup+15;
            while(invasion.State!=InvasionState.Repelled && Time.realtimeSinceStartup<end)
            { foreach(var member in invasion.Members.ToArray()) member.ReceiveDamage(int.MaxValue); yield return null; }
            Assert.That(invasion.State,Is.EqualTo(InvasionState.Repelled)); Assert.That(invasion.PrepareNextWave(),Is.True);
            Assert.That(proficiency.Experience,Is.EqualTo(5)); Assert.That(proficiency.DamageBonus,Is.EqualTo(2));
        }
    }
}
