using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectF.Input;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectF.Tests
{
    public sealed class MouseCommandTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private Camera camera;
        private UnitCommandController commands;
        private CommandInput input;
        private CommandableUnit[] units;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture();
            fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("InputCamera");
            camera = Camera.main;
            commands = Object.FindAnyObjectByType<UnitCommandController>();
            input = Object.FindAnyObjectByType<CommandInput>();
            units = Object.FindObjectsByType<CommandableUnit>().OrderBy(u => u.name).ToArray();
            Assert.That(units.Length, Is.EqualTo(4));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            fixture?.TearDown();
        }

        private Vector2 Screen(Vector2 world) => camera.WorldToScreenPoint(world);
        private IEnumerator State(Vector2 position, int buttons = 0, float scroll = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)buttons, scroll = new Vector2(0, scroll) });
            // Two dynamic updates also allow camera LateUpdate to complete before assertions.
            yield return null;
            yield return null;
        }
        private IEnumerator Click(Vector2 world, int button = 1)
        {
            yield return State(Screen(world), button);
            yield return State(Screen(world));
        }
        private IEnumerator SelectAll(bool reverse = false)
        {
            var a = Screen(new Vector2(-3, -3));
            var b = Screen(new Vector2(3, 2));
            yield return State(reverse ? b : a, 1);
            yield return State(reverse ? a : b, 1);
            Assert.That(commands.IsDragging, Is.True);
            yield return State(reverse ? a : b);
            Assert.That(commands.Selected.Count, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator ClickSelectsOneAndEmptyClickClears()
        {
            yield return Click(units[0].Position);
            Assert.That(commands.Selected, Is.EquivalentTo(new[] { units[0] }));
            Assert.That(units[0].IsSelected, Is.True);
            yield return Click(units[1].Position);
            Assert.That(commands.Selected, Is.EquivalentTo(new[] { units[1] }));
            Assert.That(units[0].IsSelected, Is.False);
            yield return Click(new Vector2(7, 4));
            Assert.That(commands.Selected, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DragSelectsBothDirections()
        {
            yield return SelectAll();
            yield return Click(new Vector2(7, 4));
            yield return SelectAll(true);
        }

        [UnityTest]
        public IEnumerator DragSelectionIncludesNewlySpawnedUnits()
        {
            var copy = Object.Instantiate(units[0], new Vector3(0, -.5f, 0), Quaternion.identity);
            copy.name = "New selectable unit";
            yield return State(Screen(new Vector2(-3, -3)), 1);
            yield return State(Screen(new Vector2(3, 2)), 1);
            yield return State(Screen(new Vector2(3, 2)));
            Assert.That(commands.Selected.Count, Is.EqualTo(5));
            Assert.That(commands.Selected, Does.Contain(copy));
            yield return Click(new Vector2(8, 4), 2);
            Assert.That(Vector2.Distance(copy.RequestedDestination, new Vector2(8, 4)), Is.LessThan(.02f));
        }

        [UnityTest]
        public IEnumerator NoSelectionAndKeyboardCannotMoveUnits()
        {
            var before = units.Select(u => u.Position).ToArray();
            yield return Click(new Vector2(8, 4), 2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.S, Key.D, Key.UpArrow, Key.RightArrow));
            yield return new WaitForSeconds(.25f);
            for (int i = 0; i < units.Length; i++)
            {
                Assert.That(units[i].HasDestination, Is.False);
                Assert.That(Vector2.Distance(before[i], units[i].Position), Is.LessThan(.01f));
            }
        }

        [UnityTest]
        public IEnumerator SelectedUnitMovesArrivesAndCameraStaysIndependent()
        {
            var unit = units[0];
            var stationary = units.Skip(1).Select(u => u.Position).ToArray();
            Vector3 cameraStart = camera.transform.position;
            Vector2 target = unit.Position + Vector2.up * 4;
            yield return Click(unit.Position);
            yield return Click(target, 2);
            Assert.That(unit.HasDestination, Is.True);
            yield return new WaitForSeconds(1.2f);
            Assert.That(Vector2.Distance(unit.Position, target), Is.LessThan(.08f));
            Assert.That(unit.HasDestination, Is.False);
            Assert.That(unit.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude, Is.LessThan(.001f));
            Assert.That(Vector3.Distance(cameraStart, camera.transform.position), Is.LessThan(.001f));
            for (int i = 1; i < units.Length; i++)
                Assert.That(Vector2.Distance(stationary[i - 1], units[i].Position), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator GroupConvergesOnClickedPointAndArrives()
        {
            yield return SelectAll();
            yield return Click(new Vector2(8, 4), 2);
            foreach (var unit in units) Assert.That(Vector2.Distance(unit.RequestedDestination, new Vector2(8, 4)), Is.LessThan(.02f));
            yield return new WaitForSeconds(4);
            foreach (var unit in units)
            {
                Assert.That(Vector2.Distance(unit.Position, unit.Destination), Is.LessThan(.08f), unit.name);
                Assert.That(unit.HasDestination, Is.False, unit.name);
                Assert.That(Vector2.Distance(unit.Position, new Vector2(8, 4)), Is.LessThan(1.6f));
            }
        }

        [UnityTest]
        public IEnumerator ReissuingOrderReplacesTargetAndDeselectDoesNotCancel()
        {
            var unit = units[0];
            yield return Click(unit.Position);
            yield return Click(new Vector2(-8, 5), 2);
            yield return Click(new Vector2(-6, -5), 2);
            Assert.That(Vector2.Distance(unit.Destination, new Vector2(-6, -5)), Is.LessThan(.02f));
            yield return Click(new Vector2(8, 4));
            Assert.That(commands.Selected, Is.Empty);
            Assert.That(unit.HasDestination, Is.True);
            yield return new WaitForSeconds(2);
            Assert.That(Vector2.Distance(unit.Position, new Vector2(-6, -5)), Is.LessThan(.08f));
        }

        [UnityTest]
        public IEnumerator WheelZoomAnchorsCursorAndClamps()
        {
            Vector2 pointer = Screen(new Vector2(3, 2));
            Vector3 before = camera.ScreenToWorldPoint(pointer);
            float initial = camera.orthographicSize;
            yield return State(pointer, 0, 120);
            Assert.That(camera.orthographicSize, Is.LessThan(initial));
            Assert.That(Vector3.Distance(before, camera.ScreenToWorldPoint(pointer)), Is.LessThan(.01f));
            yield return State(pointer, 0, 12000);
            Assert.That(camera.orthographicSize, Is.EqualTo(5).Within(.01f));
            yield return State(pointer, 0, -12000);
            yield return State(pointer, 0, -12000);
            Assert.That(camera.orthographicSize, Is.EqualTo(14).Within(.01f));
        }

        [UnityTest]
        public IEnumerator MiddleDragPansWithoutMovingUnits()
        {
            var positions = units.Select(u => u.Position).ToArray();
            Vector2 center = new Vector2(UnityEngine.Screen.width * .5f, UnityEngine.Screen.height * .5f);
            Vector3 before = camera.transform.position;
            yield return State(center, 4);
            yield return State(center + new Vector2(70, 35), 4);
            yield return State(center + new Vector2(70, 35));
            Assert.That(camera.transform.position.x, Is.LessThan(before.x));
            Assert.That(camera.transform.position.y, Is.LessThan(before.y));
            for (int i = 0; i < units.Length; i++) Assert.That(units[i].Position, Is.EqualTo(positions[i]));
            Assert.That(commands.Selected, Is.Empty);
        }

        [UnityTest]
        public IEnumerator GroupTargetsStayInsideBoundaryAndIdleUnitsDoNotDrift()
        {
            var idleBody = units[0].GetComponent<Rigidbody2D>();
            idleBody.linearVelocity = Vector2.right * 3;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(idleBody.linearVelocity, Is.EqualTo(Vector2.zero));
            yield return SelectAll();
            yield return State(Screen(Vector2.zero), 0, -12000);
            yield return Click(new Vector2(23.7f, 13.5f), 2);
            foreach (var unit in units)
            {
                Assert.That(unit.HasDestination, Is.True);
                Assert.That(unit.Destination.x, Is.InRange(-23f, 23f));
                Assert.That(unit.Destination.y, Is.InRange(-15f, 15f));
            }
        }

        [UnityTest]
        public IEnumerator FocusLossCancelsDragAndDisableStopsUnit()
        {
            yield return State(Screen(new Vector2(-3, -3)), 1);
            yield return State(Screen(new Vector2(3, 2)), 1);
            Assert.That(commands.IsDragging, Is.True);
            input.SendMessage("OnApplicationFocus", false);
            yield return null;
            Assert.That(commands.IsDragging, Is.False);
            yield return State(Screen(new Vector2(3, 2)));
            input.SendMessage("OnApplicationFocus", true);
            yield return Click(units[0].Position);
            yield return Click(new Vector2(-6, 5), 2);
            Assert.That(units[0].HasDestination, Is.True);
            units[0].enabled = false;
            yield return null;
            Assert.That(units[0].GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(units[0].HasDestination, Is.False);
        }
    }
}
