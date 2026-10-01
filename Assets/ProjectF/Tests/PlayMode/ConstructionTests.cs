using System;
using System.Collections;
using System.Linq;
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
    public sealed class ConstructionTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private WallConstruction construction;
        private WallPlacementController placement;
        private EstateStockpile stockpile;
        private UnitCommandController commands;
        private PortalInvasion invasion;
        private UnitCombat ally;
        private Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            construction = Object.FindAnyObjectByType<WallConstruction>();
            placement = Object.FindAnyObjectByType<WallPlacementController>();
            stockpile = Object.FindAnyObjectByType<EstateStockpile>();
            commands = Object.FindAnyObjectByType<UnitCommandController>();
            invasion = Object.FindAnyObjectByType<PortalInvasion>();
            ally = Object.FindObjectsByType<UnitCombat>().OrderBy(u => u.name).First();
            camera = Camera.main;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            fixture.TearDown();
        }
        private IEnumerator Pointer(Vector2 screen, int buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)buttons });
            yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 world, int buttons = 1)
        {
            var screen = (Vector2)camera.WorldToScreenPoint(world);
            yield return Pointer(screen, buttons); yield return Pointer(screen);
        }
        private IEnumerator BuildButton()
        {
            var button = Object.FindAnyObjectByType<ConstructionHUD>().GetComponentInChildren<UnityEngine.UI.Button>();
            var screen = RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().position);
            yield return Pointer(screen); yield return Pointer(screen, 1); yield return Pointer(screen);
        }
        private void PlaceAlly(Vector2 point)
        {
            ally.transform.position = point; ally.GetComponent<Rigidbody2D>().position = point;
            Physics2D.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator SuccessfulBuildSnapsAndChargesExactlyOnce()
        {
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            Assert.That(construction.TryBuild(new Vector2(-1.1f, .1f)), Is.EqualTo(PlacementResult.Available));
            var wall = Object.FindAnyObjectByType<WallStructure>();
            Assert.That((Vector2)wall.transform.position, Is.EqualTo(new Vector2(-1, 0)));
            Assert.That(wall.GetComponent<BoxCollider2D>().isTrigger, Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Occupied));
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            Assert.That(Object.FindObjectsByType<WallStructure>().Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OccupiedTerrainUnitsAndTerritoryEdgesDoNotSpendWood()
        {
            var obstacle = new GameObject("Construction obstacle", typeof(BoxCollider2D));
            obstacle.transform.position = new Vector3(-1, 4, 0);
            Assert.That(construction.TryBuild(new Vector2(-1, 4)), Is.EqualTo(PlacementResult.Occupied));
            Assert.That(construction.TryBuild(ally.Unit.Position), Is.EqualTo(PlacementResult.Occupied));
            Assert.That(construction.TryBuild(new Vector2(1, 0)), Is.EqualTo(PlacementResult.OutsideTerritory));
            Assert.That(construction.TryBuild(new Vector2(-12, 0)), Is.EqualTo(PlacementResult.OutsideTerritory));
            Assert.That(construction.TryBuild(new Vector2(float.NaN, 0)), Is.EqualTo(PlacementResult.OutsideTerritory));
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            Assert.That(Object.FindObjectsByType<WallStructure>(), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExhaustedResourcesPreventNinthWallAndNeverGoNegative()
        {
            for (int i = 0; i < 8; i++) Assert.That(construction.TryBuild(new Vector2(-1, i - 4)), Is.EqualTo(PlacementResult.Available));
            Assert.That(stockpile.Wood, Is.Zero);
            Assert.That(construction.TryBuild(new Vector2(-1, 4)), Is.EqualTo(PlacementResult.InsufficientWood));
            Assert.That(stockpile.TrySpendWood(-10), Is.False);
            Assert.That(stockpile.TrySpendWood(0), Is.False);
            Assert.That(stockpile.TrySpendWood(int.MaxValue), Is.False);
            Assert.That(stockpile.Wood, Is.Zero);
            Assert.That(Object.FindObjectsByType<WallStructure>().Length, Is.EqualTo(8));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseButtonBuildsWithoutChangingSelectionAndHoldingDoesNotPaint()
        {
            yield return Click(ally.Unit.Position);
            yield return BuildButton();
            Assert.That(placement.IsPlacing, Is.True);
            yield return Pointer(camera.WorldToScreenPoint(new Vector2(-1, 0)));
            Assert.That(placement.PreviewVisible, Is.True);
            Assert.That(placement.Result, Is.EqualTo(PlacementResult.Available));
            yield return Pointer(camera.WorldToScreenPoint(new Vector2(-1, 0)), 1);
            yield return Pointer(camera.WorldToScreenPoint(new Vector2(-1, 1)), 1);
            Assert.That(Object.FindObjectsByType<WallStructure>().Length, Is.EqualTo(1));
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            Assert.That(commands.Selected, Does.Contain(ally.Unit));
            yield return Pointer(camera.WorldToScreenPoint(new Vector2(-1, 1)));
        }

        [UnityTest]
        public IEnumerator RightClickCancelsWithoutLeakingMoveOrderAndNormalCommandsResume()
        {
            yield return Click(ally.Unit.Position);
            yield return BuildButton();
            yield return Click(new Vector2(-1, 3), 2);
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(ally.Unit.HasDestination, Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            yield return Click(new Vector2(-2, 4), 2);
            Assert.That(ally.Unit.HasDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator EscapeAndComponentDisableBothReleaseConstructionInput()
        {
            yield return Click(ally.Unit.Position);
            yield return BuildButton();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.That(placement.IsPlacing, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return BuildButton();
            placement.enabled = false;
            yield return null;
            Assert.That(placement.PreviewVisible, Is.False);
            yield return Click(new Vector2(-2, 4), 2);
            Assert.That(ally.Unit.HasDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator ReleasingOnePointerOwnerDoesNotReleaseAnother()
        {
            yield return Click(ally.Unit.Position);
            var otherTool = new object();
            commands.SetPointerCommandsBlocked(otherTool, true);
            placement.Toggle(); placement.Cancel();
            yield return Click(new Vector2(-2, 4), 2);
            Assert.That(ally.Unit.HasDestination, Is.False);
            commands.SetPointerCommandsBlocked(otherTool, false);
            yield return null; yield return null;
            yield return Click(new Vector2(-2, 4), 2);
            Assert.That(ally.Unit.HasDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator MovingUnitInvalidatesPreviouslyGreenPreviewAtCommit()
        {
            yield return BuildButton();
            yield return Pointer(camera.WorldToScreenPoint(new Vector2(-1, 0)));
            Assert.That(placement.Result, Is.EqualTo(PlacementResult.Available));
            PlaceAlly(new Vector2(-1, 0));
            yield return Click(new Vector2(-1, 0));
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            Assert.That(Object.FindObjectsByType<WallStructure>(), Is.Empty);
            Assert.That(placement.Result, Is.EqualTo(PlacementResult.Occupied));
        }

        [UnityTest]
        public IEnumerator UIPointerHidesPreviewAndClickingCancelDoesNotBuildBehindPanel()
        {
            yield return BuildButton();
            yield return Pointer(camera.WorldToScreenPoint(new Vector2(-1, 0)));
            Assert.That(placement.PreviewVisible, Is.True);
            yield return BuildButton();
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(placement.PreviewVisible, Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            Assert.That(Object.FindObjectsByType<WallStructure>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator PauseAndInvasionStartPreventConstructionAndCloseMode()
        {
            yield return BuildButton();
            Time.timeScale = 0;
            yield return null;
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Unavailable));
            Assert.That(placement.IsPlacing, Is.False);
            Time.timeScale = 1;
            yield return BuildButton();
            Assert.That(invasion.Begin(), Is.True);
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Unavailable));
            Assert.That(stockpile.Wood, Is.EqualTo(80));
        }

        [UnityTest]
        public IEnumerator ConstructedWallsForceNavigationAroundSolidGeometry()
        {
            foreach (var other in Object.FindObjectsByType<UnitCombat>()) if (other != ally) other.gameObject.SetActive(false);
            PlaceAlly(new Vector2(-4, 0));
            for (int y = -2; y <= 2; y++) Assert.That(construction.TryBuild(new Vector2(-1, y)), Is.EqualTo(PlacementResult.Available));
            ally.Unit.MoveTo(new Vector2(3, 0));
            float largestY = 0, until = Time.time + 8;
            while (ally.Unit.HasDestination && Time.time < until)
            {
                largestY = Mathf.Max(largestY, Mathf.Abs(ally.Unit.Position.y));
                Assert.That(!(Mathf.Abs(ally.Unit.Position.x + 1) < .7f && Mathf.Abs(ally.Unit.Position.y) < 2.7f),
                    Is.True, "Unit crossed the constructed wall.");
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Vector2.Distance(ally.Unit.Position, new Vector2(3, 0)), Is.LessThan(.15f));
            Assert.That(largestY, Is.GreaterThan(2.7f));
        }

        [UnityTest]
        public IEnumerator SceneRestartResetsResourcesAndRemovesConstructedWalls()
        {
            construction.TryBuild(new Vector2(-1, 0));
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            yield return null;
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
            Assert.That(Object.FindObjectsByType<WallStructure>(), Is.Empty);
            Assert.That(Object.FindAnyObjectByType<WallPlacementController>().IsPlacing, Is.False);
        }
    }
}
