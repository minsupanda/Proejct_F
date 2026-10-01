using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class DemolitionTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private WallConstruction construction;
        private WallPlacementController placement;
        private EstateStockpile stockpile;
        private UnitCommandController commands;
        private UnitCombat ally;
        private Camera camera;
        private ConstructionSettings testSettings;

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
            ally = Object.FindObjectsByType<UnitCombat>().OrderBy(u => u.name).First();
            camera = Camera.main;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            if (testSettings != null) Object.Destroy(testSettings);
            fixture.TearDown();
        }
        private WallStructure Build(Vector2 point)
        {
            Assert.That(construction.TryBuild(point), Is.EqualTo(PlacementResult.Available));
            return construction.FindDemolitionTarget(point);
        }
        private void UseSettings(string json)
        {
            if (testSettings == null)
            {
                testSettings = Object.Instantiate(construction.Settings);
                typeof(WallConstruction).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(construction, testSettings);
            }
            JsonUtility.FromJsonOverwrite(json, testSettings);
        }
        private IEnumerator Pointer(Vector2 screen, int buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen, buttons = (ushort)buttons });
            yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 point, int buttons = 1)
        {
            Vector2 screen = camera.WorldToScreenPoint(point);
            yield return Pointer(screen); yield return Pointer(screen, buttons); yield return Pointer(screen);
        }
        private UnityEngine.UI.Button Button(string name) => Object.FindAnyObjectByType<ConstructionHUD>().transform.Find(name).GetComponent<UnityEngine.UI.Button>();
        private IEnumerator ClickButton(string name)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, Button(name).GetComponent<RectTransform>().position);
            yield return Pointer(screen); yield return Pointer(screen, 1); yield return Pointer(screen);
        }

        [UnityTest]
        public IEnumerator RefundIsPaidOnceAndCollisionIsRemovedImmediately()
        {
            var wall = Build(new Vector2(-1, 0));
            DemolitionResult nested = DemolitionResult.Available;
            stockpile.Changed += () => nested = construction.TryDemolish(wall);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Available));
            Assert.That(nested, Is.EqualTo(DemolitionResult.Unavailable));
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            Assert.That(wall.gameObject.activeSelf, Is.False);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.NoWall));
            Assert.That(construction.FindDemolitionTarget(new Vector2(-1, 0)), Is.Null);
            Assert.That(construction.TryBuild(new Vector2(-1, 0)), Is.EqualTo(PlacementResult.Available));
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            yield return null;
            Assert.That(Object.FindObjectsByType<WallStructure>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RefundUsesPurchaseSettingsAndRoundsDown()
        {
            UseSettings("{\"wallWoodCost\":17,\"demolitionRefundPercent\":50}");
            var wall = Build(new Vector2(-1, 0));
            Assert.That(wall.RefundWood, Is.EqualTo(8));
            UseSettings("{\"wallWoodCost\":100,\"demolitionRefundPercent\":100}");
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Available));
            Assert.That(stockpile.Wood, Is.EqualTo(71));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroRefundCanRemoveWallAndPercentIsClamped()
        {
            UseSettings("{\"demolitionRefundPercent\":-10}");
            var wall = Build(new Vector2(-1, 0));
            Assert.That(wall.RefundWood, Is.Zero);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Available));
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            UseSettings("{\"demolitionRefundPercent\":150}");
            Assert.That(testSettings.GetDemolitionRefund(int.MaxValue), Is.EqualTo(int.MaxValue));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullStorageAndInvalidGrantsCannotOverflowOrDestroyWall()
        {
            var wall = Build(new Vector2(-1, 0));
            Assert.That(stockpile.TryAddWood(int.MaxValue - stockpile.Wood), Is.True);
            Assert.That(stockpile.TryAddWood(1), Is.False);
            Assert.That(stockpile.TryAddWood(-1), Is.False);
            Assert.That(stockpile.TryAddWood(0), Is.False);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.RefundOverflow));
            Assert.That(wall.isActiveAndEnabled, Is.True);
            Assert.That(stockpile.Wood, Is.EqualTo(int.MaxValue));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyTerrainAndUnownedWallsCannotBeDemolished()
        {
            Assert.That(construction.TryDemolish(null), Is.EqualTo(DemolitionResult.NoWall));
            Assert.That(construction.FindDemolitionTarget(new Vector2(-1, 4)), Is.Null);
            Assert.That(construction.FindDemolitionTarget(ally.Unit.Position), Is.Null);
            Assert.That(construction.FindDemolitionTarget(new Vector2(float.NaN, 0)), Is.Null);
            var other = new GameObject("Unowned wall", typeof(BoxCollider2D), typeof(WallStructure));
            other.transform.position = new Vector2(-2, 0);
            var wall = other.GetComponent<WallStructure>();
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.NoWall));
            Assert.That(construction.FindDemolitionTarget(new Vector2(-2, 0)), Is.Null);
            Assert.That(stockpile.Wood, Is.EqualTo(80));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualRemoveButtonDeletesOneWallPerPressAndPreservesSelection()
        {
            var first = Build(new Vector2(-1, 0));
            var second = Build(new Vector2(-1, 1));
            yield return Click(ally.Unit.Position);
            yield return ClickButton("Remove Wall");
            Assert.That(placement.IsDemolishing, Is.True);
            yield return Pointer(camera.WorldToScreenPoint(first.transform.position));
            Assert.That(placement.DemolitionTarget, Is.EqualTo(first));
            Assert.That(placement.PreviewVisible, Is.True);
            yield return Pointer(camera.WorldToScreenPoint(first.transform.position), 1);
            yield return Pointer(camera.WorldToScreenPoint(second.transform.position), 1);
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            Assert.That(second != null, Is.True, "Holding must not sweep across walls.");
            Assert.That(commands.Selected, Does.Contain(ally.Unit));
            yield return Pointer(camera.WorldToScreenPoint(second.transform.position));
            yield return Click(second.transform.position);
            Assert.That(stockpile.Wood, Is.EqualTo(80));
        }

        [UnityTest]
        public IEnumerator ToolSwitchAndRightCancelDoNotLeakMovement()
        {
            yield return Click(ally.Unit.Position);
            yield return ClickButton("Remove Wall");
            yield return ClickButton("Build Wall");
            Assert.That(placement.IsPlacing, Is.True);
            yield return ClickButton("Remove Wall");
            Assert.That(placement.IsPlacing, Is.False);
            Assert.That(placement.IsDemolishing, Is.True);
            yield return Click(new Vector2(-2, 4), 2);
            Assert.That(placement.IsEditing, Is.False);
            Assert.That(ally.Unit.HasDestination, Is.False);
            yield return Click(new Vector2(-2, 4), 2);
            Assert.That(ally.Unit.HasDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator EscapeDisableAndUIButtonsClearTargetWithoutRemovingAnything()
        {
            var wall = Build(new Vector2(-1, 0));
            yield return ClickButton("Remove Wall");
            yield return Pointer(camera.WorldToScreenPoint(wall.transform.position));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.That(placement.IsEditing, Is.False);
            Assert.That(placement.PreviewVisible, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return ClickButton("Remove Wall");
            yield return ClickButton("Remove Wall");
            Assert.That(placement.IsEditing, Is.False);
            yield return ClickButton("Remove Wall");
            placement.enabled = false;
            Assert.That(placement.IsEditing, Is.False);
            Assert.That(placement.DemolitionTarget, Is.Null);
            Assert.That(stockpile.Wood, Is.EqualTo(70));
            yield return Click(ally.Unit.Position);
            Assert.That(commands.Selected, Does.Contain(ally.Unit));
        }

        [UnityTest]
        public IEnumerator UIBlocksDemolitionOfWallBehindPanel()
        {
            var wall = Build(new Vector2(-8, -6));
            yield return ClickButton("Remove Wall");
            yield return Pointer(camera.WorldToScreenPoint(wall.transform.position));
            Assert.That(commands.PointerOverUI(), Is.True);
            Assert.That(placement.PreviewVisible, Is.False);
            yield return Click(wall.transform.position);
            Assert.That(wall != null, Is.True);
            Assert.That(stockpile.Wood, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator EmptyStockpileStillAllowsRemovalAndRebuilding()
        {
            for (int i = 0; i < 8; i++) Build(new Vector2(-1, i - 4));
            Assert.That(stockpile.Wood, Is.Zero);
            Assert.That(Button("Build Wall").interactable, Is.False);
            Assert.That(Button("Remove Wall").interactable, Is.True);
            yield return ClickButton("Remove Wall");
            yield return Click(new Vector2(-1, 0));
            Assert.That(stockpile.Wood, Is.EqualTo(10));
            yield return ClickButton("Build Wall");
            yield return Click(new Vector2(-1, 0));
            Assert.That(stockpile.Wood, Is.Zero);
            Assert.That(Object.FindObjectsByType<WallStructure>().Length, Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator PauseAndInvasionPreventRemovalAndCloseTool()
        {
            var wall = Build(new Vector2(-1, 0));
            placement.ToggleDemolition(); Time.timeScale = 0;
            yield return null;
            Assert.That(placement.IsEditing, Is.False);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Unavailable));
            Assert.That(Button("Remove Wall").interactable, Is.False);
            Time.timeScale = 1; yield return null;
            Assert.That(Button("Remove Wall").interactable, Is.True);
            yield return ClickButton("Remove Wall");
            Assert.That(placement.IsDemolishing, Is.True);
            Assert.That(Object.FindAnyObjectByType<PortalInvasion>().Begin(), Is.True);
            Assert.That(placement.IsEditing, Is.False);
            Assert.That(construction.TryDemolish(wall), Is.EqualTo(DemolitionResult.Unavailable));
            Assert.That(stockpile.Wood, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator RemovingMiddleWallOpensDirectRouteThroughExistingNavigation()
        {
            foreach (var other in Object.FindObjectsByType<UnitCombat>()) if (other != ally) other.gameObject.SetActive(false);
            ally.transform.position = new Vector2(-4, 0); ally.GetComponent<Rigidbody2D>().position = ally.transform.position;
            Physics2D.SyncTransforms();
            for (int y = -2; y <= 2; y++) Build(new Vector2(-1, y));
            // Let navigation cache the blocked geometry before changing it.
            ally.Unit.MoveTo(new Vector2(3, 0));
            yield return new WaitForFixedUpdate();
            Assert.That(construction.TryDemolish(construction.FindDemolitionTarget(new Vector2(-1, 0))), Is.EqualTo(DemolitionResult.Available));
            float maxY = 0, until = Time.time + 8;
            while (ally.Unit.HasDestination && Time.time < until)
            {
                maxY = Mathf.Max(maxY, Mathf.Abs(ally.Unit.Position.y));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Vector2.Distance(ally.Unit.Position, new Vector2(3, 0)), Is.LessThan(.15f));
            Assert.That(maxY, Is.LessThan(1), "Navigation must use the reopened gap.");
        }

        [UnityTest]
        public IEnumerator SceneReloadClearsModeAndRefundState()
        {
            construction.TryDemolish(Build(new Vector2(-1, 0)));
            Build(new Vector2(-1, 1)); placement.ToggleDemolition();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindAnyObjectByType<EstateStockpile>().Wood, Is.EqualTo(80));
            Assert.That(Object.FindAnyObjectByType<WallPlacementController>().IsEditing, Is.False);
            Assert.That(Object.FindObjectsByType<WallStructure>(), Is.Empty);
        }
    }
}
