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
    public sealed class ControlGroupTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private CommandableUnit[] units;
        private UnitCommandController commands;
        private CommandInput input;
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
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = buttons }); yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 world)
        {
            yield return Pointer(Screen(world), 1); yield return Pointer(Screen(world));
        }
        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
        }
        private IEnumerator Tap(params Key[] keys) { yield return Keys(keys); yield return Keys(); }
        private void Expect(params CommandableUnit[] expected)
        {
            Assert.That(commands.Selected, Is.EquivalentTo(expected));
            foreach (var unit in units.Where(u => u != null)) Assert.That(unit.IsSelected, Is.EqualTo(expected.Contains(unit)));
        }
        private IEnumerator StorePair()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            yield return Click(units[1].Position); yield return Keys(); yield return Tap(Key.LeftCtrl, Key.Digit1);
        }

        [UnityTest]
        public IEnumerator AllNineSlotsStoreAndRecallWithEitherControlKey()
        {
            var digits = new[] {Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9};
            for (int i = 0; i < digits.Length; i++)
            {
                yield return Click(units[i % units.Length].Position);
                yield return Tap(i % 2 == 0 ? Key.LeftCtrl : Key.RightCtrl, digits[i]);
            }
            for (int i = 0; i < digits.Length; i++) { yield return Tap(digits[i]); Expect(units[i % units.Length]); }
        }
        [UnityTest]
        public IEnumerator RecallReplacesSelectionAndGroupsKeepIndependentMembership()
        {
            yield return StorePair(); yield return Tap(Key.RightCtrl, Key.Digit2);
            yield return Click(units[2].Position); yield return Tap(Key.LeftCtrl, Key.Digit1);
            yield return Tap(Key.Digit2); Expect(units[0], units[1]);
            yield return Tap(Key.LeftShift, Key.Digit1); Expect(units[2]);
        }
        [UnityTest]
        public IEnumerator EmptySelectionClearsSlotAndEmptyRecallClearsSelection()
        {
            yield return StorePair(); yield return Click(Vector2.zero); Expect();
            yield return Tap(Key.LeftCtrl, Key.Digit1); yield return Click(units[2].Position);
            yield return Tap(Key.Digit1); Expect();
            yield return Click(units[0].Position); yield return Tap(Key.Digit9); Expect();
        }
        [UnityTest]
        public IEnumerator RecallPreservesMovementAndNextOrderOnlyAffectsRecalledUnits()
        {
            yield return StorePair(); yield return Click(units[2].Position);
            var destination = new Vector2(-16, 5);
            foreach (var unit in units) unit.MoveTo(destination);
            var orders = units.Select(u => u.CommandOrder).ToArray();
            yield return Tap(Key.Digit1); Expect(units[0], units[1]);
            for (int i = 0; i < units.Length; i++) Assert.That(units[i].CommandOrder, Is.EqualTo(orders[i]));
            // Keep the world order below the selection panel, including its proficiency row.
            var orderPoint = Screen(new Vector2(-12, -4));
            yield return Pointer(orderPoint);
            Assert.That(commands.PointerOverUI(), Is.False, "The movement order must target uncovered ground.");
            yield return Pointer(orderPoint, 2); yield return Pointer(Screen(Vector2.zero));
            Assert.That(units[0].CommandOrder, Is.GreaterThan(orders[0])); Assert.That(units[1].CommandOrder, Is.GreaterThan(orders[1]));
            Assert.That(units[2].CommandOrder, Is.EqualTo(orders[2])); Assert.That(units[3].CommandOrder, Is.EqualTo(orders[3]));
        }
        [UnityTest]
        public IEnumerator StoringAndRecallingDoNotCancelGatheringOrAttack()
        {
            yield return StorePair(); CombatTestScene.AddGuards(); yield return null;
            foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>(FindObjectsSortMode.None)) ai.enabled = false;
            var enemy = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).First(c => c.Faction == UnitFaction.Hostile);
            var combat = units[0].GetComponent<UnitCombat>(); Assert.That(combat.Attack(enemy), Is.True);
            var gatherer = units[1].GetComponent<WoodGatherer>();
            var tree = Object.FindObjectsByType<WoodResourceNode>(FindObjectsSortMode.None).First(); Assert.That(gatherer.Gather(tree), Is.True);
            yield return Tap(Key.LeftCtrl, Key.Digit2); yield return Tap(Key.Digit9); yield return Tap(Key.Digit2);
            Expect(units[0], units[1]); Assert.That(combat.Target, Is.SameAs(enemy)); Assert.That(gatherer.Target, Is.SameAs(tree));
        }
        [UnityTest]
        public IEnumerator InvalidMembersArePrunedWithoutLosingSurvivors()
        {
            yield return Keys(Key.LeftShift); foreach (var unit in units) yield return Click(unit.Position);
            yield return Keys(); yield return Tap(Key.LeftCtrl, Key.Digit1);
            units[1].GetComponent<UnitCombat>().ReceiveDamage(int.MaxValue);
            units[2].enabled = false; Object.Destroy(units[3].gameObject); yield return null; yield return null;
            yield return Tap(Key.Digit1); Expect(units[0]);
            units[2].enabled = true; yield return null; yield return Tap(Key.Digit1); Expect(units[0]);
        }
        [UnityTest]
        public IEnumerator HoldingNumberDoesNotRepeatedlyRecallOrStore()
        {
            yield return StorePair(); yield return Keys(Key.Digit1);
            yield return Click(units[2].Position); Expect(units[2]); yield return Keys();
            yield return Keys(Key.LeftCtrl, Key.Digit1); yield return Click(units[3].Position); yield return Keys();
            yield return Tap(Key.Digit1); Expect(units[2]);
        }
        [UnityTest]
        public IEnumerator LowestSlotWinsAndRecallConsumesSimultaneousRightClick()
        {
            yield return StorePair(); yield return Click(units[2].Position); yield return Tap(Key.LeftCtrl, Key.Digit2);
            var orderPoint = Screen(new Vector2(-12, -4));
            yield return Pointer(orderPoint);
            Assert.That(commands.PointerOverUI(), Is.False, "Only group recall should consume this world order.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2, Key.Digit1));
            yield return Pointer(orderPoint, 2); yield return Pointer(Screen(Vector2.zero));
            Expect(units[0], units[1]); Assert.That(units.All(u => !u.HasDestination), Is.True);
        }
        [UnityTest]
        public IEnumerator StopWinsOverGroupRecallInSameFrame()
        {
            yield return StorePair(); yield return Click(units[2].Position); units[2].MoveTo(new Vector2(-16, 5));
            yield return Tap(Key.S, Key.Digit1); Expect(units[2]); Assert.That(units[2].HasDestination, Is.False);
        }
        [UnityTest]
        public IEnumerator UiOutsideViewPauseAndDisabledInputBlockGroupShortcuts()
        {
            yield return StorePair(); yield return Click(units[2].Position);
            var ui = Object.FindAnyObjectByType<RecruitmentRallyHUD>().transform.Find("Clear Rally");
            yield return Pointer(RectTransformUtility.WorldToScreenPoint(null, ui.position));
            yield return Tap(Key.LeftCtrl, Key.Digit1); yield return Tap(Key.Digit1); Expect(units[2]);
            yield return Pointer(new Vector2(-10, -10)); yield return Tap(Key.Digit1); Expect(units[2]);
            yield return Pointer(Screen(Vector2.zero)); Time.timeScale = 0; yield return Tap(Key.Digit1); Expect(units[2]); Time.timeScale = 1;
            input.enabled = false; yield return Tap(Key.Digit1); input.enabled = true; yield return null; Expect(units[2]);
            yield return Tap(Key.Digit1); Expect(units[0], units[1]);
        }
        [UnityTest]
        public IEnumerator ConstructionRallyDragAndPanBlockGroupShortcuts()
        {
            yield return StorePair(); yield return Click(units[2].Position);
            var placement = Object.FindAnyObjectByType<WallPlacementController>();
            foreach (var toggle in new Action[] {placement.Toggle, placement.ToggleDemolition, placement.ToggleRepair})
            {
                toggle(); yield return Tap(Key.LeftCtrl, Key.Digit1); yield return Tap(Key.Digit1); Expect(units[2]);
                placement.Cancel(); yield return null; yield return null;
            }
            var rally = Object.FindAnyObjectByType<RecruitmentRallyController>(); rally.Toggle();
            yield return Tap(Key.Digit1); Expect(units[2]); rally.Cancel(); yield return null; yield return null;
            yield return Pointer(Screen(new Vector2(-7, 0)), 1); yield return Pointer(Screen(new Vector2(-4, 2)), 1);
            yield return Tap(Key.Digit1); Expect(units[2]); yield return Keys(Key.Escape); yield return Pointer(Screen(Vector2.zero)); yield return Keys();
            yield return Pointer(Screen(Vector2.zero), 4); yield return Tap(Key.Digit1); Expect(units[2]);
            yield return Pointer(Screen(Vector2.zero)); yield return Tap(Key.Digit1); Expect(units[0], units[1]);
        }
        [UnityTest]
        public IEnumerator TemporaryControllerDisableRetainsGroupsButSceneReloadResetsThem()
        {
            yield return StorePair(); commands.enabled = false; Expect(); commands.enabled = true; yield return null;
            yield return Tap(Key.Digit1); Expect(units[0], units[1]);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            commands = Object.FindAnyObjectByType<UnitCommandController>();
            yield return Tap(Key.Digit1); Assert.That(commands.Selected, Is.Empty);
        }
        [UnityTest]
        public IEnumerator NewlyTrainedSoldierCanBeStoredAndRecalled()
        {
            var recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); var original = recruitment.Settings;
            var settings = Object.Instantiate(original);
            var field = typeof(UnitRecruitment).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
            typeof(RecruitmentSettings).GetField("trainingSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, .1f);
            field.SetValue(recruitment, settings);
            try
            {
                Assert.That(recruitment.TryTrain(), Is.True); float end = Time.time + 3;
                while (recruitment.IsTraining && Time.time < end) yield return null;
                Assert.That(recruitment.IsTraining, Is.False); var soldier = recruitment.GetComponentInChildren<UnitCombat>().Unit;
                yield return Click(soldier.Position); yield return Tap(Key.RightCtrl, Key.Digit9);
                yield return Click(units[0].Position); yield return Tap(Key.Digit9);
                Assert.That(commands.Selected, Is.EquivalentTo(new[] {soldier})); Assert.That(soldier.IsSelected, Is.True);
            }
            finally { field.SetValue(recruitment, original); Object.Destroy(settings); }
        }
    }
}
