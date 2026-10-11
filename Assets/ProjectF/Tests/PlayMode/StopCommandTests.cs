using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Input;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class StopCommandTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private CommandableUnit[] units;
        private UnitCommandController commands;
        private CommandInput input;
        private readonly Vector2 destination = new Vector2(-16, 5);
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            units = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).Where(c => c.IsPlayerControlled)
                .OrderBy(c => c.name).Select(c => c.Unit).ToArray();
            commands = Object.FindAnyObjectByType<UnitCommandController>(); input = Object.FindAnyObjectByType<CommandInput>();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene"); fixture.TearDown();
        }
        private Vector2 Screen(Vector2 world) => Camera.main.WorldToScreenPoint(world);
        private IEnumerator Pointer(Vector2 screen, ushort buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState {position = screen, buttons = buttons}); yield return null; yield return null;
        }
        private IEnumerator Click(CommandableUnit unit)
        {
            yield return Pointer(Screen(unit.Position), 1); yield return Pointer(Screen(unit.Position));
        }
        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
        }
        private IEnumerator StopKey() { yield return Keys(Key.S); yield return Keys(); }
        private IEnumerator Until(Func<bool> test, float timeout = 6)
        {
            float end = Time.time + timeout; while (!test() && Time.time < end) yield return null;
            Assert.That(test(), Is.True, "Stop command scenario timed out.");
        }
        private static void Stopped(CommandableUnit unit)
        {
            Assert.That(unit.HasDestination, Is.False);
            Assert.That(unit.TravelState, Is.EqualTo(UnitTravelState.Idle));
            Assert.That(unit.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude, Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator SelectedUnitsStopAndUnselectedUnitKeepsItsOrder()
        {
            yield return Click(units[0]); yield return Keys(Key.LeftShift); yield return Click(units[1]); yield return Keys();
            foreach (var unit in units) unit.MoveTo(destination);
            yield return new WaitForSeconds(.15f); yield return StopKey();
            Stopped(units[0]); Stopped(units[1]); Assert.That(units[2].HasDestination, Is.True);
            Assert.That(commands.Selected, Is.EquivalentTo(new[] {units[0], units[1]}));
            var position = units[0].Position; yield return new WaitForSeconds(.3f);
            Assert.That(Vector2.Distance(units[0].Position, position), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator StopCancelsPursuitAndLaterAttackCanResume()
        {
            yield return Click(units[0]); CombatTestScene.AddGuards(); yield return null;
            foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>(FindObjectsSortMode.None)) ai.enabled = false;
            var enemy = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).First(c => c.Faction == UnitFaction.Hostile);
            var combat = units[0].GetComponent<UnitCombat>(); Assert.That(combat.Attack(enemy), Is.True);
            yield return new WaitForSeconds(.1f); yield return StopKey();
            Assert.That(combat.Target, Is.Null); Stopped(units[0]);
            var position = units[0].Position; yield return new WaitForSeconds(.5f);
            Assert.That(Vector2.Distance(position, units[0].Position), Is.LessThan(.01f));
            Assert.That(combat.Attack(enemy), Is.True); yield return Until(() => units[0].HasDestination);
        }
        [UnityTest]
        public IEnumerator StopCancelsGatheringWithoutFurtherWoodAndGatherCanResume()
        {
            yield return Click(units[0]); var gatherer = units[0].GetComponent<WoodGatherer>();
            var node = Object.FindObjectsByType<WoodResourceNode>(FindObjectsSortMode.None).OrderBy(n => Vector2.Distance(n.transform.position, units[0].Position)).First();
            var stock = Object.FindAnyObjectByType<EstateStockpile>();
            Assert.That(gatherer.Gather(node), Is.True); yield return Until(() => gatherer.State == GatheringState.Gathering);
            yield return StopKey(); var wood = stock.Wood; var remaining = node.Remaining;
            Assert.That(gatherer.Target, Is.Null); Assert.That(gatherer.State, Is.EqualTo(GatheringState.Idle)); Stopped(units[0]);
            yield return new WaitForSeconds(2.2f); Assert.That(stock.Wood, Is.EqualTo(wood)); Assert.That(node.Remaining, Is.EqualTo(remaining));
            Assert.That(gatherer.Gather(node), Is.True); yield return Until(() => stock.Wood > wood);
        }
        [UnityTest]
        public IEnumerator HoldingStopIssuesOnceAndAllowsANewMove()
        {
            yield return Click(units[0]); int count = 0; units[0].StopCommandIssued += () => count++;
            units[0].MoveTo(destination); yield return Keys(Key.S); Stopped(units[0]);
            units[0].MoveTo(destination); yield return new WaitForSeconds(.3f);
            Assert.That(count, Is.EqualTo(1)); Assert.That(units[0].HasDestination, Is.True);
            yield return Keys(); yield return StopKey(); Assert.That(count, Is.EqualTo(2)); Stopped(units[0]);
        }
        [UnityTest]
        public IEnumerator StopWinsOverSimultaneousRightClick()
        {
            yield return Click(units[0]); units[0].MoveTo(destination);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return Pointer(Screen(destination), 2); yield return Pointer(Screen(destination));
            Stopped(units[0]);
        }
        [UnityTest]
        public IEnumerator StopWithoutSelectionDoesNotChangeAnyOrders()
        {
            foreach (var unit in units) unit.MoveTo(destination);
            var orders = units.Select(u => u.CommandOrder).ToArray();
            yield return Pointer(Screen(Vector2.zero)); yield return StopKey();
            for (int i = 0; i < units.Length; i++) { Assert.That(units[i].HasDestination, Is.True); Assert.That(units[i].CommandOrder, Is.EqualTo(orders[i])); }
        }
        [UnityTest]
        public IEnumerator UiOutsideViewPauseAndDisabledInputBlockStop()
        {
            yield return Click(units[0]); units[0].MoveTo(destination);
            var ui = Object.FindAnyObjectByType<RecruitmentRallyHUD>().transform.Find("Clear Rally");
            yield return Pointer(RectTransformUtility.WorldToScreenPoint(null, ui.position)); yield return StopKey(); Assert.That(units[0].HasDestination, Is.True);
            yield return Pointer(new Vector2(-10, -10)); yield return StopKey(); Assert.That(units[0].HasDestination, Is.True);
            yield return Pointer(Screen(Vector2.zero)); Time.timeScale = 0; yield return Keys(Key.S); Time.timeScale = 1;
            yield return null; Assert.That(units[0].HasDestination, Is.True); yield return Keys();
            input.enabled = false; yield return StopKey(); input.enabled = true; yield return null;
            Assert.That(units[0].HasDestination, Is.True); yield return StopKey(); Stopped(units[0]);
        }
        [UnityTest]
        public IEnumerator ConstructionAndRallyModesBlockStop()
        {
            yield return Click(units[0]); var placement = Object.FindAnyObjectByType<WallPlacementController>();
            foreach (var toggle in new Action[] {placement.Toggle, placement.ToggleDemolition, placement.ToggleRepair})
            {
                units[0].MoveTo(destination); toggle(); Assert.That(placement.IsEditing, Is.True);
                yield return StopKey(); Assert.That(units[0].HasDestination, Is.True); placement.Cancel(); yield return null;
            }
            units[0].MoveTo(destination); var rally = Object.FindAnyObjectByType<RecruitmentRallyController>(); rally.Toggle();
            yield return StopKey(); Assert.That(units[0].HasDestination, Is.True); rally.Cancel(); yield return null; yield return null;
            yield return StopKey(); Stopped(units[0]);
        }
        [UnityTest]
        public IEnumerator SelectionDragAndCameraPanBlockStop()
        {
            yield return Click(units[0]); units[0].MoveTo(destination);
            yield return Pointer(Screen(new Vector2(-7, 0)), 1); yield return Pointer(Screen(new Vector2(-4, 2)), 1);
            yield return StopKey(); Assert.That(units[0].HasDestination, Is.True);
            yield return Keys(Key.Escape); yield return Pointer(Screen(Vector2.zero)); yield return Keys();
            yield return Pointer(Screen(Vector2.zero), 4); yield return StopKey(); Assert.That(units[0].HasDestination, Is.True);
            yield return Pointer(Screen(Vector2.zero)); yield return StopKey(); Stopped(units[0]);
        }
        [UnityTest]
        public IEnumerator StopDoesNotResetAttackCooldown()
        {
            yield return Click(units[0]); CombatTestScene.AddGuards(); yield return null;
            foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>(FindObjectsSortMode.None)) ai.enabled = false;
            var enemy = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).First(c => c.Faction == UnitFaction.Hostile);
            typeof(UnitCombat).GetField("retaliateWhenIdle", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, false);
            enemy.Unit.GetComponent<Rigidbody2D>().position = units[0].Position + Vector2.right;
            var combat = units[0].GetComponent<UnitCombat>(); int hits = 0; combat.Attacked += _ => hits++;
            combat.Attack(enemy); yield return Until(() => hits > 0); int health = enemy.Health;
            yield return StopKey(); combat.Attack(enemy); yield return new WaitForFixedUpdate();
            Assert.That(enemy.Health, Is.EqualTo(health)); Assert.That(hits, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator ArrivalDoesNotCancelGatheringAndReenabledActionsStillStop()
        {
            var gatherer = units[0].GetComponent<WoodGatherer>(); var combat = units[0].GetComponent<UnitCombat>();
            for (int i = 0; i < 3; i++) { gatherer.enabled = false; combat.enabled = false; combat.enabled = true; gatherer.enabled = true; }
            yield return Click(units[0]);
            var node = Object.FindObjectsByType<WoodResourceNode>(FindObjectsSortMode.None).OrderBy(n => Vector2.Distance(n.transform.position, units[0].Position)).First();
            gatherer.Gather(node); yield return Until(() => gatherer.State == GatheringState.Gathering);
            Assert.That(gatherer.Target, Is.EqualTo(node)); yield return StopKey(); Assert.That(gatherer.Target, Is.Null); Stopped(units[0]);
            combat.ReceiveDamage(combat.MaxHealth); long order = units[0].CommandOrder; units[0].StopCommand();
            Assert.That(units[0].CommandOrder, Is.EqualTo(order));
        }
    }
}
