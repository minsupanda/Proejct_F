using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
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
    public sealed class AdditiveSelectionTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private UnitCommandController commands;
        private CommandInput input;
        private CommandableUnit[] units;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            commands = Object.FindAnyObjectByType<UnitCommandController>();
            input = Object.FindAnyObjectByType<CommandInput>();
            units = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None)
                .Where(c => c.IsPlayerControlled).OrderBy(c => c.name).Select(c => c.Unit).ToArray();
            Assert.That(units.Length, Is.EqualTo(4));
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            fixture.TearDown();
        }
        private Vector2 Screen(Vector2 world) => Camera.main.WorldToScreenPoint(world);
        private IEnumerator Pointer(Vector2 screen, ushort buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = buttons });
            yield return null; yield return null;
        }
        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 world, ushort button = 1)
        {
            yield return Pointer(Screen(world), button); yield return Pointer(Screen(world));
        }
        private IEnumerator Drag(Vector2 a, Vector2 b)
        {
            yield return Pointer(Screen(a));
            Assert.That(commands.PointerOverUI(), Is.False, "World selection gestures must start outside the HUD.");
            yield return Pointer(Screen(a), 1); yield return Pointer(Screen(b), 1);
            Assert.That(commands.IsDragging, Is.True); yield return Pointer(Screen(b));
        }
        private void Expect(params CommandableUnit[] expected)
        {
            Assert.That(commands.Selected, Is.EquivalentTo(expected));
            Assert.That(commands.Selected.Distinct().Count(), Is.EqualTo(commands.Selected.Count));
            foreach (var unit in units) if (unit != null && unit.isActiveAndEnabled)
                Assert.That(unit.IsSelected, Is.EqualTo(expected.Contains(unit)), unit.name);
        }
        private IEnumerator DragAll(bool reverse)
        {
            var a = new Vector2(units.Min(u => u.Position.x) - .8f, units.Min(u => u.Position.y) - .8f);
            var b = new Vector2(units.Max(u => u.Position.x) + .8f, units.Max(u => u.Position.y) + .8f);
            yield return Drag(reverse ? b : a, reverse ? a : b);
        }

        [UnityTest]
        public IEnumerator BothShiftKeysAddAndToggleWithoutReplacingOthers()
        {
            yield return Click(units[0].Position);
            foreach (var key in new[] {Key.LeftShift, Key.RightShift})
            {
                yield return Keys(key); yield return Click(units[1].Position); Expect(units[0], units[1]);
                yield return Click(units[1].Position); Expect(units[0]); yield return Keys();
            }
            yield return Keys(Key.LeftShift); yield return Click(units[0].Position); Expect();
        }
        [UnityTest]
        public IEnumerator EmptyOrEnemyShiftClickPreservesSelection()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            yield return Click(new Vector2(-16, 5)); Expect(units[0]);
            CombatTestScene.AddGuards(); yield return null;
            var enemy = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).First(c => c.Faction == UnitFaction.Hostile);
            yield return Click(enemy.Unit.Position); Expect(units[0]);
        }
        [UnityTest]
        public IEnumerator AdditiveDragIsAUnionInBothDirectionsWithoutDuplicates()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            yield return DragAll(false); Expect(units);
            yield return DragAll(true); Expect(units);
            // Keep the empty-area gesture below the selection status panel.
            yield return Drag(new Vector2(-12, -4), new Vector2(-10, -3)); Expect(units);
        }
        [UnityTest]
        public IEnumerator NormalClickAndDragStillReplaceAndEmptyClears()
        {
            yield return Keys(Key.LeftShift); yield return DragAll(false); Expect(units);
            yield return Keys(); yield return Click(units[1].Position); Expect(units[1]);
            yield return Drag(units[0].Position - Vector2.one * .65f, units[0].Position + Vector2.one * .65f); Expect(units[0]);
            yield return Click(new Vector2(-16, 5)); Expect();
        }
        [UnityTest]
        public IEnumerator GestureKeepsModifierFromMouseDown()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            var start = Screen(units[1].Position - Vector2.one * .65f);
            var end = Screen(units[1].Position + Vector2.one * .65f);
            yield return Pointer(start, 1); yield return Keys(); yield return Pointer(end, 1); yield return Pointer(end);
            Expect(units[0], units[1]);
            yield return Pointer(Screen(units[2].Position), 1); yield return Keys(Key.LeftShift);
            yield return Pointer(Screen(units[2].Position)); Expect(units[2]);
        }
        [UnityTest]
        public IEnumerator EscapeAndPanCancelAdditiveDragWithoutChangingSelection()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            yield return Pointer(Screen(units[1].Position), 1); yield return Pointer(Screen(units[2].Position), 1);
            yield return Keys(Key.LeftShift, Key.Escape); yield return Pointer(Screen(units[2].Position));
            Assert.That(commands.IsDragging, Is.False); Expect(units[0]);
            yield return Keys(Key.LeftShift); yield return Pointer(Screen(units[1].Position), 1);
            yield return Pointer(Screen(units[2].Position), 5); yield return Pointer(Screen(units[2].Position)); Expect(units[0]);
        }
        [UnityTest]
        public IEnumerator UiAndOutOfViewReleasePreserveSelection()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            var ui = Object.FindAnyObjectByType<RecruitmentRallyHUD>().transform.Find("Clear Rally");
            var point = RectTransformUtility.WorldToScreenPoint(null, ui.position);
            yield return Pointer(point, 1); yield return Pointer(Screen(units[1].Position)); Expect(units[0]);
            yield return Pointer(Screen(units[1].Position), 1); yield return Pointer(point); Expect(units[0]);
            yield return Pointer(Screen(units[1].Position), 1); yield return Pointer(new Vector2(-30, -30)); Expect(units[0]);
        }
        [UnityTest]
        public IEnumerator ModalRallyAndConstructionDoNotToggleSelectedUnits()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            var rally = Object.FindAnyObjectByType<RecruitmentRallyController>();
            rally.Toggle(); yield return Click(units[0].Position); Expect(units[0]);
            rally.Cancel(); yield return Pointer(Screen(units[0].Position));
            var placement = Object.FindAnyObjectByType<WallPlacementController>();
            foreach (var toggle in new System.Action[] {placement.Toggle, placement.ToggleDemolition, placement.ToggleRepair})
            {
                toggle(); Assert.That(placement.IsEditing, Is.True);
                yield return Click(units[0].Position); Expect(units[0]);
                placement.Cancel(); yield return Pointer(Screen(units[0].Position));
            }
            yield return Click(units[1].Position); Expect(units[0], units[1]);
        }
        [UnityTest]
        public IEnumerator InputDisableCancelsGestureAndControllerDisableClearsMembership()
        {
            yield return Click(units[0].Position); yield return Keys(Key.LeftShift);
            yield return Pointer(Screen(units[1].Position), 1); input.enabled = false; yield return null;
            yield return Pointer(Screen(units[1].Position)); input.enabled = true; yield return null; Expect(units[0]);
            commands.enabled = false; yield return null; Expect(); commands.enabled = true; yield return null;
            yield return Click(units[0].Position); Expect(units[0]);
        }
        [UnityTest]
        public IEnumerator DisabledAndDestroyedUnitsArePrunedAndCanBeReselectedAfterEnable()
        {
            yield return Keys(Key.LeftShift); yield return Click(units[0].Position); yield return Click(units[1].Position);
            units[1].enabled = false; yield return null; Expect(units[0]);
            units[1].enabled = true; yield return null; yield return Click(units[1].Position); Expect(units[0], units[1]);
            Object.Destroy(units[1].gameObject); yield return null; yield return null; Expect(units[0]);
        }
        [UnityTest]
        public IEnumerator MovementCommandsReachOnlyFinalSelectionAndSelectionDoesNotCancelOrders()
        {
            yield return Click(units[0].Position); var destination = new Vector2(-15, 2);
            yield return Click(destination, 2); var order = units[0].CommandOrder;
            yield return Keys(Key.LeftShift); yield return Click(units[1].Position);
            Assert.That(units[0].CommandOrder, Is.EqualTo(order));
            yield return Click(units[0].Position); Expect(units[1]);
            var next = new Vector2(-16, 5); yield return Click(next, 2);
            Assert.That(Vector2.Distance(units[1].RequestedDestination, next), Is.LessThan(.02f));
            Assert.That(units[0].CommandOrder, Is.EqualTo(order));
            Assert.That(units[2].HasDestination, Is.False);
        }
        [UnityTest]
        public IEnumerator NewlyTrainedSoldierCanJoinSelectionAndReloadResetsIt()
        {
            var recruitment = Object.FindAnyObjectByType<UnitRecruitment>();
            var original = recruitment.Settings; var settings = Object.Instantiate(original);
            var field = typeof(UnitRecruitment).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
            typeof(RecruitmentSettings).GetField("trainingSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, .1f);
            field.SetValue(recruitment, settings);
            try
            {
                yield return Click(units[0].Position); Assert.That(recruitment.TryTrain(), Is.True);
                float until = Time.time + 3;
                while (recruitment.IsTraining && Time.time < until) yield return null;
                Assert.That(recruitment.IsTraining, Is.False);
                var soldier = recruitment.GetComponentInChildren<UnitCombat>().Unit;
                yield return Keys(Key.RightShift); yield return Click(soldier.Position); Expect(units[0], soldier);
                Assert.That(soldier.HasDestination, Is.False);
            }
            finally { field.SetValue(recruitment, original); Object.Destroy(settings); }
            yield return Keys(); yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Is.Empty);
        }
    }
}
