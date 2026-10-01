using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Input;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectF.Tests
{
    public sealed class CombatTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Camera camera;
        private UnitCommandController commands;
        private NavigationWorld2D navigation;
        private UnitCombat[] allies, enemies;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture();
            fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            CombatTestScene.AddGuards();
            // These tests isolate explicit combat orders; autonomous decisions have their own suite.
            foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>()) ai.enabled = false;
            camera = Camera.main;
            commands = Object.FindAnyObjectByType<UnitCommandController>();
            navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            var units = Object.FindObjectsByType<UnitCombat>().OrderBy(u => u.name).ToArray();
            allies = units.Where(u => u.Faction == UnitFaction.Player).ToArray();
            enemies = units.Where(u => u.Faction == UnitFaction.Hostile).ToArray();
            Assert.That(allies.Length, Is.EqualTo(4));
            Assert.That(enemies.Length, Is.EqualTo(3));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            fixture?.TearDown();
        }

        private IEnumerator MouseState(Vector2 world, int buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                { position = camera.WorldToScreenPoint(world), buttons = (ushort)buttons });
            yield return null;
            yield return null;
        }
        private IEnumerator Click(Vector2 world, int button = 1)
        {
            yield return MouseState(world, button);
            yield return MouseState(world);
        }
        private void Duel(Vector2 player, Vector2 enemy)
        {
            foreach (var unit in allies.Skip(1).Concat(enemies.Skip(1))) unit.gameObject.SetActive(false);
            GameObject.Find("Navigation Obstacles").SetActive(false);
            Place(allies[0], player);
            Place(enemies[0], enemy);
            Physics2D.SyncTransforms();
            navigation.MarkObstaclesDirty();
        }
        private static void Place(UnitCombat combat, Vector2 position)
        {
            combat.transform.position = position;
            combat.GetComponent<Rigidbody2D>().position = position;
        }
        private IEnumerator WaitForDamage(UnitCombat victim, float timeout = 6)
        {
            int health = victim.Health;
            float until = Time.time + timeout;
            while (victim.Health == health && Time.time < until) yield return new WaitForFixedUpdate();
            Assert.That(victim.Health, Is.LessThan(health), "Target was never hit.");
        }

        [UnityTest]
        public IEnumerator InitialHealthBarsAreFullAndSceneReloadResetsHealth()
        {
            foreach (var combat in allies.Concat(enemies))
            {
                Assert.That(combat.Health, Is.EqualTo(combat.MaxHealth));
                Assert.That(combat.transform.Find("Health Bar/Health Fill").localScale.x, Is.EqualTo(1));
            }
            allies[0].ReceiveDamage(40);
            enemies[0].ReceiveDamage(int.MaxValue);
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            CombatTestScene.AddGuards();
            yield return null;
            var restored = Object.FindObjectsByType<UnitCombat>();
            Assert.That(restored.Length, Is.EqualTo(7));
            foreach (var combat in restored)
            {
                Assert.That(combat.Health, Is.EqualTo(combat.MaxHealth));
                Assert.That(combat.Target, Is.Null);
                Assert.That(combat.transform.Find("Health Bar/Health Fill").localScale.x, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator ClickAndDragSelectAlliesOnly()
        {
            yield return Click(enemies[0].Unit.Position);
            Assert.That(commands.Selected, Is.Empty);
            yield return MouseState(new Vector2(-8, -4), 1);
            yield return MouseState(new Vector2(8, 3), 1);
            yield return MouseState(new Vector2(8, 3));
            Assert.That(commands.Selected, Is.EquivalentTo(allies.Select(a => a.Unit)));
        }

        [UnityTest]
        public IEnumerator RightClickApproachesAttacksRetaliatesAndKills()
        {
            Duel(new Vector2(-4, 0), new Vector2(4, 0));
            yield return Click(allies[0].Unit.Position);
            yield return Click(enemies[0].Unit.Position, 2);
            Assert.That(allies[0].Target, Is.EqualTo(enemies[0]));
            yield return WaitForDamage(enemies[0]);
            Assert.That(enemies[0].Target, Is.EqualTo(allies[0]));
            float until = Time.time + 6;
            while (enemies[0].IsAlive && Time.time < until) yield return new WaitForFixedUpdate();
            Assert.That(enemies[0].Health, Is.Zero);
            Assert.That(enemies[0].gameObject.activeSelf, Is.False);
            Assert.That(allies[0].Health, Is.LessThan(allies[0].MaxHealth));
            Assert.That(allies[0].IsAlive, Is.True);
            Assert.That(allies[0].Target, Is.Null);
            Assert.That(allies[0].Unit.HasDestination, Is.False);
        }

        [UnityTest]
        public IEnumerator GroundOrderCancelsPursuitAndArrives()
        {
            Duel(new Vector2(-5, 0), new Vector2(5, 0));
            yield return Click(allies[0].Unit.Position);
            yield return Click(enemies[0].Unit.Position, 2);
            yield return new WaitForSeconds(.2f);
            var destination = new Vector2(-7, -4);
            yield return Click(destination, 2);
            Assert.That(allies[0].Target, Is.Null);
            float until = Time.time + 5;
            while (allies[0].Unit.HasDestination && Time.time < until) yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(allies[0].Unit.Position, destination), Is.LessThan(.1f));
            Assert.That(enemies[0].Health, Is.EqualTo(enemies[0].MaxHealth));
        }

        [UnityTest]
        public IEnumerator RepeatedAndCancelledOrdersDoNotResetCooldown()
        {
            Duel(Vector2.zero, new Vector2(1.2f, 0));
            allies[0].Attack(enemies[0]);
            yield return WaitForDamage(enemies[0]);
            int remaining = enemies[0].Health;
            for (int i = 0; i < 10; i++)
            {
                allies[0].CancelAttack();
                allies[0].Attack(enemies[0]);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(enemies[0].Health, Is.EqualTo(remaining));
            yield return WaitForDamage(enemies[0], 1.2f);
        }

        [UnityTest]
        public IEnumerator FriendlyNeutralAndNonPositiveDamageAreRejected()
        {
            int health = allies[0].Health;
            Assert.That(allies[0].Attack(allies[1]), Is.False);
            Assert.That(allies[0].Attack(allies[0]), Is.False);
            allies[0].ReceiveDamage(30, allies[1]);
            allies[0].ReceiveDamage(0);
            allies[0].ReceiveDamage(-10);
            Assert.That(allies[0].Health, Is.EqualTo(health));
            typeof(UnitCombat).GetField("faction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(enemies[1], UnitFaction.Neutral);
            Assert.That(allies[0].Attack(enemies[1]), Is.False);
            Assert.That(enemies[1].Attack(allies[0]), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WallBlocksDamageAndRemovalAllowsAttack()
        {
            Duel(new Vector2(-.7f, 0), new Vector2(.7f, 0));
            var wall = new GameObject("Combat test wall", typeof(BoxCollider2D));
            wall.GetComponent<BoxCollider2D>().size = new Vector2(.2f, 20);
            Physics2D.SyncTransforms();
            navigation.MarkObstaclesDirty();
            allies[0].Attack(enemies[0]);
            yield return new WaitForSeconds(.5f);
            Assert.That(enemies[0].Health, Is.EqualTo(enemies[0].MaxHealth));
            Object.Destroy(wall);
            yield return null;
            navigation.MarkObstaclesDirty();
            yield return WaitForDamage(enemies[0]);
        }

        [UnityTest]
        public IEnumerator MovingTargetIsPursued()
        {
            Duel(new Vector2(-4, 0), Vector2.zero);
            allies[0].Attack(enemies[0]);
            enemies[0].Unit.MoveTo(new Vector2(6, 3));
            yield return WaitForDamage(enemies[0], 8);
            Assert.That(allies[0].Unit.Position.x, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator DisabledTargetCancelsPursuit()
        {
            Duel(new Vector2(-5, 0), new Vector2(5, 0));
            allies[0].Attack(enemies[0]);
            yield return new WaitForFixedUpdate();
            enemies[0].gameObject.SetActive(false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(allies[0].Target, Is.Null);
            Assert.That(allies[0].Unit.HasDestination, Is.False);
            Assert.That(allies[0].GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator DestroyedTargetCancelsPursuit()
        {
            Duel(new Vector2(-5, 0), new Vector2(5, 0));
            allies[0].Attack(enemies[0]);
            yield return new WaitForFixedUpdate();
            Object.Destroy(enemies[0].gameObject);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(allies[0].Unit.HasDestination, Is.False);
        }

        [UnityTest]
        public IEnumerator DeathUnregistersCollisionSelectionAndFiresOnce()
        {
            yield return Click(allies[0].Unit.Position);
            int deaths = 0;
            allies[0].Died += () => deaths++;
            allies[0].ReceiveDamage(int.MaxValue);
            allies[0].ReceiveDamage(1);
            yield return null;
            yield return null;
            var units = new List<CommandableUnit>();
            navigation.CopyUnitsTo(units);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(allies[0].Health, Is.Zero);
            Assert.That(units, Has.No.Member(allies[0].Unit));
            Assert.That(commands.Selected, Is.Empty);
            Assert.That(allies[0].GetComponent<Collider2D>().isActiveAndEnabled, Is.False);
            Assert.That(enemies[0].Attack(allies[0]), Is.False);
        }

        [UnityTest]
        public IEnumerator GroupAttackKillsWithoutBodyOverlap()
        {
            enemies[1].gameObject.SetActive(false);
            enemies[2].gameObject.SetActive(false);
            foreach (var ally in allies) ally.Attack(enemies[0]);
            float until = Time.time + 10;
            while (enemies[0].IsAlive && Time.time < until)
            {
                for (int i = 0; i < allies.Length; i++)
                    for (int j = i + 1; j < allies.Length; j++)
                        Assert.That(Vector2.Distance(allies[i].Unit.Position, allies[j].Unit.Position),
                            Is.GreaterThanOrEqualTo(allies[i].Unit.Radius + allies[j].Unit.Radius - .015f));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(enemies[0].Health, Is.Zero);
            yield return new WaitForFixedUpdate();
            Assert.That(allies.All(a => a.Target == null && !a.Unit.HasDestination), Is.True);
        }

        [UnityTest]
        public IEnumerator PauseStopsDamageAndFeedbackTracksHealth()
        {
            Duel(Vector2.zero, new Vector2(1.2f, 0));
            allies[0].Attack(enemies[0]);
            yield return WaitForDamage(enemies[0]);
            var fill = enemies[0].transform.Find("Health Bar/Health Fill");
            Assert.That(fill.localScale.x, Is.EqualTo((float)enemies[0].Health / enemies[0].MaxHealth).Within(.001f));
            int health = enemies[0].Health;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(enemies[0].Health, Is.EqualTo(health));
            Time.timeScale = 1;
            yield return WaitForDamage(enemies[0], 1.2f);
        }

        [UnityTest]
        public IEnumerator DisablingCombatStopsItsPursuit()
        {
            Duel(new Vector2(-5, 0), new Vector2(5, 0));
            allies[0].Attack(enemies[0]);
            yield return new WaitForFixedUpdate();
            allies[0].enabled = false;
            yield return new WaitForFixedUpdate();
            Assert.That(allies[0].Target, Is.Null);
            Assert.That(allies[0].Unit.HasDestination, Is.False);
        }
    }
}
