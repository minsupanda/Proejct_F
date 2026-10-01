using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class EnemyAITests
    {
        private UnitCombat[] allies, enemies;
        private EnemyCombatAI ai;
        private NavigationWorld2D navigation;
        private readonly List<ScriptableObject> temporarySettings = new List<ScriptableObject>();
        private UnitCombat Enemy => enemies[0];
        private UnitCombat Ally => allies[0];

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            CombatTestScene.AddGuards();
            var combatants = Object.FindObjectsByType<UnitCombat>().OrderBy(c => c.name).ToArray();
            allies = combatants.Where(c => c.Faction == UnitFaction.Player).ToArray();
            enemies = combatants.Where(c => c.Faction == UnitFaction.Hostile).ToArray();
            Assert.That(enemies.All(c => c.GetComponent<EnemyCombatAI>() != null), Is.True);
            Assert.That(allies.All(c => c.GetComponent<EnemyCombatAI>() == null), Is.True);
            ai = Enemy.GetComponent<EnemyCombatAI>();
            navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            foreach (var other in allies.Skip(1).Concat(enemies.Skip(1))) other.gameObject.SetActive(false);
            GameObject.Find("Navigation Obstacles").SetActive(false);
            ai.enabled = false;
            var settings = ScriptableObject.CreateInstance<EnemyAISettings>();
            temporarySettings.Add(settings);
            SetField(ai, "settings", settings);
            Place(Enemy, Vector2.zero);
            Place(Ally, new Vector2(-12, 0));
            Sync();
            ai.enabled = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            foreach (var settings in temporarySettings) Object.Destroy(settings);
            temporarySettings.Clear();
        }

        private static void SetField(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Place(UnitCombat combat, Vector2 position)
        {
            combat.transform.position = position;
            combat.GetComponent<Rigidbody2D>().position = position;
        }
        private void Sync() { Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty(); }
        private void SetSpeed(UnitCombat combat, float speed)
        {
            var settings = ScriptableObject.CreateInstance<LordMovementSettings>();
            SetField(settings, "moveSpeed", speed);
            SetField(combat.Unit, "settings", settings);
            temporarySettings.Add(settings);
        }
        private GameObject Wall(Vector2 position, Vector2 size)
        {
            var wall = new GameObject("AI test wall", typeof(BoxCollider2D));
            wall.transform.position = position;
            wall.GetComponent<BoxCollider2D>().size = size;
            Sync();
            return wall;
        }
        private IEnumerator Until(Func<bool> condition, string message, float timeout = 5)
        {
            float deadline = Time.time + timeout;
            while (!condition() && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(condition(), Is.True, message);
        }
        private IEnumerator StartPursuit()
        {
            Place(Ally, new Vector2(4, 0));
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Engaging, "Enemy did not detect the ally.");
            yield return Until(() => Enemy.Unit.Position.x > 1, "Enemy did not pursue.");
        }

        [UnityTest]
        public IEnumerator NearbyAllyIsDetectedAndAttackedWithoutPlayerOrder()
        {
            Place(Ally, new Vector2(4, 0));
            Sync();
            yield return Until(() => Ally.Health < Ally.MaxHealth, "Automatic attack did not damage the ally.");
            Assert.That(Enemy.Target, Is.EqualTo(Ally));
            Assert.That(Ally.Target, Is.Null, "AI must not change the player's manual command policy.");
            Assert.That(Enemy.transform.Find("AI Status").GetComponent<TextMesh>().text, Is.EqualTo("ATTACK"));
        }

        [UnityTest]
        public IEnumerator OutOfRangeAllyDoesNotTriggerPursuit()
        {
            yield return new WaitForSeconds(.7f);
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Guarding));
            Assert.That(Enemy.Target, Is.Null);
            Assert.That(Enemy.Unit.Position, Is.EqualTo(Vector2.zero));
            Assert.That(ai.DetectionScanCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator NearestVisibleTargetIsChosenAndRetained()
        {
            allies[1].gameObject.SetActive(true);
            Place(Ally, new Vector2(4, 0));
            Place(allies[1], new Vector2(0, 3));
            Sync();
            yield return Until(() => Enemy.Target != null, "No target acquired.");
            Assert.That(Enemy.Target, Is.EqualTo(allies[1]));
            Place(Ally, new Vector2(.9f, 0));
            Sync();
            yield return new WaitForSeconds(.6f);
            Assert.That(Enemy.Target, Is.EqualTo(allies[1]), "A closer arrival must not cause target thrashing.");
        }

        [UnityTest]
        public IEnumerator WallHidesCloserAllyButVisibleAllyCanBeDetected()
        {
            Place(Ally, new Vector2(3, 0));
            Wall(new Vector2(1.5f, 0), new Vector2(.2f, 3));
            yield return new WaitForSeconds(.6f);
            Assert.That(Enemy.Target, Is.Null, "Enemy saw through terrain.");
            allies[1].gameObject.SetActive(true);
            Place(allies[1], new Vector2(0, 4));
            Sync();
            yield return Until(() => Enemy.Target != null, "Visible alternative was not found.");
            Assert.That(Enemy.Target, Is.EqualTo(allies[1]));
        }

        [UnityTest]
        public IEnumerator LeashCancelsAttackAndReturnsToStartingPosition()
        {
            yield return StartPursuit();
            Place(Ally, new Vector2(12, 0));
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Returning, "Leash did not force return.");
            Assert.That(Enemy.Target, Is.Null);
            yield return Until(() => ai.State == EnemyAIState.Guarding, "Enemy did not return home.");
            Assert.That(Vector2.Distance(Enemy.Unit.Position, ai.HomePosition), Is.LessThanOrEqualTo(.6f));
        }

        [UnityTest]
        public IEnumerator DamageDuringReturnDoesNotOverrideRetreatOrHealEnemy()
        {
            yield return StartPursuit();
            Place(Ally, new Vector2(12, 0));
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Returning, "Return not started.");
            Enemy.ReceiveDamage(7, Ally);
            int health = Enemy.Health;
            Assert.That(Enemy.Target, Is.Null);
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Returning));
            yield return Until(() => ai.State == EnemyAIState.Guarding, "Enemy did not get home.");
            Assert.That(Enemy.Health, Is.EqualTo(health), "Returning must not grant an implicit heal.");
        }

        [UnityTest]
        public IEnumerator SustainedSightLossAbandonsTarget()
        {
            SetSpeed(Enemy, 0);
            Place(Ally, new Vector2(4, 0));
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Engaging, "No target acquired.");
            Wall(new Vector2(2, 0), new Vector2(.2f, 12));
            yield return new WaitForSeconds(.5f);
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Engaging), "Brief occlusion cancelled too soon.");
            yield return Until(() => ai.State == EnemyAIState.Returning, "Lost target was tracked indefinitely.", 3);
            Assert.That(Ally.Health, Is.EqualTo(Ally.MaxHealth));
        }

        [UnityTest]
        public IEnumerator StalledPursuitIsAbandonedWithinConfiguredTimeout()
        {
            SetSpeed(Enemy, 0);
            Place(Ally, new Vector2(4, 0));
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Engaging, "No target acquired.");
            yield return Until(() => ai.State == EnemyAIState.Returning, "Stalled pursuit was never released.", 5);
            Assert.That(Enemy.Target, Is.Null);
        }

        [UnityTest]
        public IEnumerator DeadTargetIsReleasedAndAnotherCanBeAcquiredAfterReturn()
        {
            yield return StartPursuit();
            Ally.ReceiveDamage(int.MaxValue);
            yield return Until(() => ai.State == EnemyAIState.Returning, "Dead target did not trigger return.");
            allies[1].gameObject.SetActive(true);
            Place(allies[1], new Vector2(0, 3));
            Sync();
            Assert.That(Enemy.Target, Is.Null);
            yield return Until(() => Enemy.Target == allies[1], "Enemy did not resume guarding/acquisition after return.");
        }

        [UnityTest]
        public IEnumerator BlockedHomeWaitsAndRecoversWhenWallIsRemoved()
        {
            yield return StartPursuit();
            yield return Until(() => Enemy.Unit.Position.x > 2, "Enemy did not leave home area.");
            var wall = Wall(Vector2.zero, new Vector2(2, 2));
            Place(Ally, new Vector2(12, 0));
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Returning, "Return not started.");
            yield return new WaitForSeconds(1.5f);
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Returning));
            Assert.That(Vector2.Distance(Enemy.Unit.Position, ai.HomePosition), Is.GreaterThan(.6f));
            Object.Destroy(wall);
            yield return null;
            Sync();
            yield return Until(() => ai.State == EnemyAIState.Guarding, "Enemy did not recover its blocked home route.");
        }

        [UnityTest]
        public IEnumerator DisableStopsCommandsAndReenableEstablishesNewGuardPost()
        {
            yield return StartPursuit();
            ai.enabled = false;
            Assert.That(Enemy.Target, Is.Null);
            Assert.That(Enemy.Unit.HasDestination, Is.False);
            Place(Enemy, new Vector2(3, 3));
            Place(Ally, new Vector2(-12, 0));
            Sync();
            ai.enabled = true;
            Assert.That(ai.HomePosition, Is.EqualTo(new Vector2(3, 3)));
            Assert.That(ai.State, Is.EqualTo(EnemyAIState.Guarding));
            yield return new WaitForSeconds(.4f);
            Assert.That(Enemy.Target, Is.Null);
        }

        [UnityTest]
        public IEnumerator PauseSuspendsDetectionAndResumesNormally()
        {
            int scans = ai.DetectionScanCount;
            Time.timeScale = 0;
            Place(Ally, new Vector2(4, 0));
            Sync();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(ai.DetectionScanCount, Is.EqualTo(scans));
            Assert.That(Enemy.Target, Is.Null);
            Time.timeScale = 1;
            yield return Until(() => Enemy.Target == Ally, "Detection did not resume.");
        }

        [UnityTest]
        public IEnumerator ThirtyTwoIdleGuardsUseBoundedDetectionCadence()
        {
            Place(Ally, new Vector2(-20, -12));
            var guards = new List<EnemyCombatAI> { ai };
            for (int i = 1; i < 32; i++)
            {
                var copy = Object.Instantiate(Enemy.gameObject, new Vector3(3 + (i % 8) * 1.5f, 3 + (i / 8) * 1.5f, 0), Quaternion.identity);
                guards.Add(copy.GetComponent<EnemyCombatAI>());
            }
            Sync();
            var before = guards.Select(g => g.DetectionScanCount).ToArray();
            float started = Time.time;
            yield return new WaitForSeconds(1);
            int maxScans = Mathf.CeilToInt((Time.time - started) / .25f) + 1;
            for (int i = 0; i < guards.Count; i++)
            {
                Assert.That(guards[i].State, Is.EqualTo(EnemyAIState.Guarding));
                Assert.That(guards[i].DetectionScanCount - before[i], Is.InRange(1, maxScans));
            }
            Debug.Log($"Enemy AI cadence: 32 idle guards, {Time.time - started:F2}s, total scans {guards.Select((g, i) => g.DetectionScanCount - before[i]).Sum()}, maximum per guard {maxScans}.");
        }
    }
}
