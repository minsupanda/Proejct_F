using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProjectF.Combat;
using ProjectF.Construction;
using ProjectF.Economy;
using ProjectF.Input;
using ProjectF.Invasion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectF.Tests
{
    public sealed class GamePauseTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private GamePause pause;
        private UnitCombat ally;
        private UnitRecruitment recruitment;
        private PortalInvasion invasion;
        private EstateStockpile stockpile;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            pause = Object.FindAnyObjectByType<GamePause>(); ally = GameObject.Find("Lord").GetComponent<UnitCombat>();
            recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); invasion = Object.FindAnyObjectByType<PortalInvasion>(); stockpile = Object.FindAnyObjectByType<EstateStockpile>();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pause != null) pause.Resume(); Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene"); fixture.TearDown();
        }
        private IEnumerator KeyState(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
        }
        private IEnumerator Pointer(Vector2 point, ushort buttons = 0, float scroll = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState {position = point, buttons = buttons, scroll = new Vector2(0, scroll)});
            yield return null; yield return null;
        }
        private IEnumerator Click(Vector2 point, ushort buttons = 1)
        {
            yield return Pointer(point); yield return Pointer(point, buttons); yield return Pointer(point);
        }
        private IEnumerator Until(Func<bool> predicate, float timeout = 8)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(predicate(), Is.True, "Pause integration scenario timed out.");
        }
        [UnityTest]
        public IEnumerator SpaceTogglesOncePerPressAndHoldDoesNotRepeat()
        {
            yield return KeyState(Key.Space); Assert.That(pause.IsPaused, Is.True); Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(.2f); Assert.That(pause.IsPaused, Is.True);
            yield return KeyState(); yield return KeyState(Key.Space); Assert.That(pause.IsPaused, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator ActualButtonsAndHudReenableHaveOneListener()
        {
            var hud = Object.FindAnyObjectByType<PauseHUD>();
            for (int i = 0; i < 3; i++) { hud.enabled = false; hud.enabled = true; }
            var button = (RectTransform)hud.transform.Find("Pause");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.TransformPoint(button.rect.center)));
            Assert.That(pause.IsPaused, Is.True); Assert.That(hud.transform.Find("Overlay").gameObject.activeSelf, Is.True);
            button = (RectTransform)hud.transform.Find("Overlay/Resume");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.TransformPoint(button.rect.center)));
            Assert.That(pause.IsPaused, Is.False); Assert.That(hud.transform.Find("Overlay").gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator MovementAndSelectionSurvivePauseAndWorldCommandsAreBlocked()
        {
            yield return Click(Camera.main.WorldToScreenPoint(ally.transform.position));
            ally.Unit.MoveTo(new Vector2(-16, 5)); yield return new WaitForSeconds(.15f);
            pause.TryPause(); yield return null; Vector2 position = ally.Unit.Position; long order = ally.Unit.CommandOrder;
            yield return Click(Camera.main.WorldToScreenPoint(new Vector2(-6, -4)), 2);
            yield return KeyState(Key.S); yield return KeyState(); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(ally.Unit.Position, Is.EqualTo(position)); Assert.That(ally.Unit.CommandOrder, Is.EqualTo(order));
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Does.Contain(ally.Unit));
            pause.Resume(); yield return new WaitForSeconds(.2f); Assert.That(Vector2.Distance(position, ally.Unit.Position), Is.GreaterThan(.01f));
        }
        [UnityTest]
        public IEnumerator TrainingAndRefundAreSuspendedThenContinue()
        {
            Assert.That(recruitment.TryTrain(), Is.True); yield return new WaitForSeconds(.1f);
            pause.TryPause(); yield return null; float remaining = recruitment.RemainingSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(recruitment.RemainingSeconds, Is.EqualTo(remaining)); Assert.That(recruitment.TryCancelTraining(), Is.False);
            Assert.That(stockpile.Wood, Is.EqualTo(60)); Assert.That(invasion.DefenderCount, Is.EqualTo(4));
            pause.Resume(); yield return new WaitForSeconds(.1f); Assert.That(recruitment.RemainingSeconds, Is.LessThan(remaining));
            Assert.That(recruitment.TryCancelTraining(), Is.True); Assert.That(stockpile.Wood, Is.EqualTo(80));
        }
        [UnityTest]
        public IEnumerator GatheringTargetAndCycleAreRetainedWithoutResourceProgress()
        {
            var tree = Object.FindObjectsByType<WoodResourceNode>().OrderBy(t => t.name).First();
            Vector2 point = (Vector2)tree.transform.position + Vector2.right * 1.25f;
            ally.transform.position = point; ally.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms();
            var gatherer = ally.GetComponent<WoodGatherer>(); Assert.That(gatherer.Gather(tree), Is.True);
            yield return Until(() => gatherer.State == GatheringState.Gathering);
            yield return new WaitForSeconds(.3f); pause.TryPause(); yield return null;
            int wood = stockpile.Wood, available = tree.Remaining;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(stockpile.Wood, Is.EqualTo(wood)); Assert.That(tree.Remaining, Is.EqualTo(available)); Assert.That(gatherer.Target, Is.SameAs(tree));
            pause.Resume(); yield return Until(() => stockpile.Wood > wood, 3);
        }
        [UnityTest]
        public IEnumerator InvasionCountdownAndSpawningStopUntilResume()
        {
            Assert.That(invasion.Begin(), Is.True); yield return new WaitForSeconds(.1f); pause.TryPause(); yield return null;
            float remaining = invasion.PreparationRemaining; yield return new WaitForSecondsRealtime(.2f);
            Assert.That(invasion.PreparationRemaining, Is.EqualTo(remaining)); Assert.That(invasion.SpawnedCount, Is.Zero);
            pause.Resume(); yield return Until(() => invasion.SpawnedCount == 1);
            pause.TryPause(); int count = invasion.SpawnedCount; yield return new WaitForSecondsRealtime(.2f);
            Assert.That(invasion.SpawnedCount, Is.EqualTo(count)); pause.Resume(); yield return Until(() => invasion.SpawnedCount > count);
        }
        [UnityTest]
        public IEnumerator MeleeDamageAndTargetAreRetainedDuringPause()
        {
            CombatTestScene.AddGuards(); var enemy = GameObject.Find("Enemy 1").GetComponent<UnitCombat>();
            foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>()) ai.enabled = false;
            Vector2 point = (Vector2)enemy.transform.position + Vector2.left;
            ally.transform.position = point; ally.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms();
            Assert.That(ally.Attack(enemy), Is.True); yield return Until(() => enemy.Health < enemy.MaxHealth);
            pause.TryPause(); yield return null; int health = enemy.Health; yield return new WaitForSecondsRealtime(.3f);
            Assert.That(enemy.Health, Is.EqualTo(health)); Assert.That(ally.Target, Is.SameAs(enemy));
            pause.Resume(); yield return Until(() => enemy.Health < health);
        }
        [UnityTest]
        public IEnumerator EditingToolsCloseAndEconomyActionsStayBlocked()
        {
            var placement = Object.FindAnyObjectByType<WallPlacementController>(); placement.Toggle(); Assert.That(placement.IsEditing, Is.True);
            pause.TryPause(); yield return null; Assert.That(placement.IsEditing, Is.False);
            Assert.That(recruitment.TryTrain(), Is.False); Assert.That(invasion.Begin(), Is.False);
            Assert.That(Object.FindAnyObjectByType<WallConstruction>().CanConstruct, Is.False);
            pause.Resume(); var rally = Object.FindAnyObjectByType<RecruitmentRallyController>(); rally.Toggle(); Assert.That(rally.IsChoosing, Is.True);
            pause.TryPause(); yield return null; Assert.That(rally.IsChoosing, Is.False); Assert.That(stockpile.Wood, Is.EqualTo(80));
        }
        [UnityTest]
        public IEnumerator CameraPanAndZoomAreFrozenEvenIfPanStartedBeforePause()
        {
            var camera = Camera.main; var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            yield return Pointer(point, 4); pause.TryPause(); yield return null;
            Vector3 position = camera.transform.position; float size = camera.orthographicSize;
            yield return Pointer(point + new Vector2(120, 80), 4, 120); yield return Pointer(point, 0, 120);
            Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.orthographicSize, Is.EqualTo(size));
        }
        [UnityTest]
        public IEnumerator DisableRestoresOwnedScaleAndExternalClockIsNotOverwritten()
        {
            Time.timeScale = .5f; Assert.That(pause.TryPause(), Is.True); Assert.That(pause.TryPause(), Is.False);
            pause.enabled = false; Assert.That(Time.timeScale, Is.EqualTo(.5f)); Assert.That(pause.IsPaused, Is.False);
            Assert.That(pause.TryPause(), Is.False); pause.enabled = true;
            Time.timeScale = 0; Assert.That(pause.TryPause(), Is.False); Assert.That(pause.Resume(), Is.False); Assert.That(Time.timeScale, Is.Zero);
            Time.timeScale = 1; pause.TryPause(); Time.timeScale = .75f; pause.Resume(); Assert.That(Time.timeScale, Is.EqualTo(.75f)); yield return null;
        }
        [UnityTest]
        public IEnumerator SceneReloadWhilePausedRestoresClockAndNewHud()
        {
            pause.TryPause(); yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            pause = Object.FindAnyObjectByType<GamePause>(); Assert.That(pause.IsPaused, Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GamePause>().Length, Is.EqualTo(1));
            yield return KeyState(Key.Space); Assert.That(pause.IsPaused, Is.True);
        }
    }
}
