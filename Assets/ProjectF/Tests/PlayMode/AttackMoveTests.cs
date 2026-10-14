using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectF.Combat;
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
    public sealed class AttackMoveTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private UnitCombat ally, enemy;
        private AttackMoveOrder order;
        private AttackMoveSettings settings;
        private readonly Vector2 destination = new Vector2(0, -7);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Place(UnitCombat unit, Vector2 point)
        {
            unit.Unit.StopCommand(); unit.transform.position = point; unit.GetComponent<Rigidbody2D>().position = point; Physics2D.SyncTransforms();
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup(); mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            ally = GameObject.Find("Lord").GetComponent<UnitCombat>(); order = ally.GetComponent<AttackMoveOrder>();
            settings = Object.Instantiate(Get<AttackMoveSettings>(order, "settings")); Set(order, "settings", settings);
            Place(ally, new Vector2(-8, -7)); CombatTestScene.AddGuards(); enemy = GameObject.Find("Enemy 1").GetComponent<UnitCombat>();
            foreach (var other in Object.FindObjectsByType<EnemyCombatAI>())
            {
                other.enabled = false;
                if (other.gameObject != enemy.gameObject) other.gameObject.SetActive(false);
            }
            Place(enemy, new Vector2(-4, -7));
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var pause = Object.FindAnyObjectByType<GamePause>(); if (pause != null) pause.Resume(); Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("SampleScene"); Object.Destroy(settings); fixture.TearDown();
        }
        private IEnumerator Until(Func<bool> predicate, float seconds = 8)
        {
            float end = Time.realtimeSinceStartup + seconds; while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(predicate(), Is.True, "Attack move scenario timed out.");
        }
        private IEnumerator Pointer(Vector2 point, ushort buttons = 0)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = buttons }); yield return null; yield return null;
        }
        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator AcquiresEnemyDealsDamageThenResumesOriginalDestination()
        {
            Assert.That(order.Begin(destination), Is.True); yield return Until(() => ally.Target == enemy);
            Assert.That(order.Destination, Is.EqualTo(destination)); yield return Until(() => enemy.Health < enemy.MaxHealth);
            enemy.ReceiveDamage(int.MaxValue); yield return Until(() => ally.Target == null && ally.Unit.HasDestination);
            Assert.That(ally.Unit.RequestedDestination, Is.EqualTo(destination)); yield return Until(() => !order.IsActive);
            Assert.That(Vector2.Distance(ally.Unit.Position, destination), Is.LessThan(1));
        }
        [UnityTest]
        public IEnumerator ActualChordPreservesSelectionAndShowsAdvanceCount()
        {
            enemy.gameObject.SetActive(false); Place(ally, new Vector2(-6, 1));
            var inputDestination = new Vector2(-2, 5);
            var point = (Vector2)Camera.main.WorldToScreenPoint(ally.Unit.Position);
            yield return Pointer(point, 1); yield return Pointer(point); yield return Keys(Key.A);
            point = Camera.main.WorldToScreenPoint(inputDestination); yield return Pointer(point, 2); yield return Pointer(point); yield return Keys();
            Assert.That(order.IsActive, Is.True); Assert.That(order.Destination.x, Is.EqualTo(inputDestination.x).Within(.01));
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Does.Contain(ally.Unit));
            yield return new WaitForSecondsRealtime(.13f);
            StringAssert.Contains("ADVANCE 1", Object.FindAnyObjectByType<SelectionStatusHUD>().GetComponentInChildren<UnityEngine.UI.Text>().text);
            yield return Keys(Key.S); Assert.That(order.IsActive, Is.False); Assert.That(ally.Unit.HasDestination, Is.False);
        }
        [UnityTest]
        public IEnumerator NormalMoveNeverAcquiresAnEnemy()
        {
            ally.Unit.MoveTo(destination); yield return new WaitForSeconds(.3f);
            Assert.That(order.IsActive, Is.False); Assert.That(ally.Target, Is.Null); Assert.That(order.DetectionScanCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator VisibleNearestHostileWinsAndSolidWallBlocksDetection()
        {
            var second = Object.Instantiate(enemy.gameObject); second.name = "Second threat";
            var other = second.GetComponent<UnitCombat>(); Place(other, new Vector2(-3, -7));
            // The clone retains its scene navigation reference; keep its enemy AI disabled.
            order.Begin(destination); yield return Until(() => ally.Target != null); Assert.That(ally.Target, Is.SameAs(enemy));
            ally.Unit.StopCommand(); Place(ally, new Vector2(-8, -7));
            var wall = new GameObject("Sight test wall"); wall.transform.position = new Vector2(-6, -7); wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 4);
            Physics2D.SyncTransforms(); order.Begin(new Vector2(-14, -7)); yield return new WaitForSeconds(.4f);
            Assert.That(ally.Target, Is.Null); Object.Destroy(wall);
        }
        [UnityTest]
        public IEnumerator MoveDirectAttackAndGatherReplaceAdvance()
        {
            order.Begin(destination); yield return Until(() => ally.Target == enemy);
            ally.Unit.MoveTo(new Vector2(-12, -7)); Assert.That(order.IsActive, Is.False); Assert.That(ally.Target, Is.Null);
            order.Begin(destination); ally.Attack(enemy); Assert.That(order.IsActive, Is.False); Assert.That(ally.Target, Is.SameAs(enemy));
            order.Begin(destination); var tree = Object.FindAnyObjectByType<WoodResourceNode>();
            Assert.That(ally.GetComponent<WoodGatherer>().Gather(tree), Is.True); Assert.That(order.IsActive, Is.False); Assert.That(ally.Target, Is.Null);
        }
        [UnityTest]
        public IEnumerator DestroyedTargetAndLeashEscapeResumeRoute()
        {
            order.Begin(destination); yield return Until(() => ally.Target == enemy); Place(enemy, new Vector2(15, -7));
            yield return Until(() => ally.Target == null); Assert.That(ally.Unit.RequestedDestination, Is.EqualTo(destination));
            Place(enemy, ally.Unit.Position + Vector2.right * 3); yield return Until(() => ally.Target == enemy);
            Object.Destroy(enemy.gameObject); yield return Until(() => ally.Target == null);
            Assert.That(order.IsActive, Is.True); Assert.That(ally.Unit.RequestedDestination, Is.EqualTo(destination));
        }
        [UnityTest]
        public IEnumerator StalledPursuitYieldsToOriginalRoute()
        {
            Set(settings, "stalledSeconds", .3f); ally.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            order.Begin(destination); yield return Until(() => ally.Target == enemy);
            yield return Until(() => ally.Target == null, 2);
            Assert.That(order.IsActive, Is.True); Assert.That(ally.Unit.RequestedDestination, Is.EqualTo(destination));
        }
        [UnityTest]
        public IEnumerator ArrivalEndsScanningAndInactiveOrdersDoNoWork()
        {
            enemy.gameObject.SetActive(false); order.Begin(new Vector2(-9, -7)); yield return Until(() => !order.IsActive);
            int scans = order.DetectionScanCount; yield return new WaitForSeconds(.3f); Assert.That(order.DetectionScanCount, Is.EqualTo(scans));
            order.Begin(destination); yield return new WaitForSeconds(.6f); Assert.That(order.DetectionScanCount, Is.InRange(1, 4));
        }
        [UnityTest]
        public IEnumerator PauseKeepsAdvanceAndStopsScanningAndMovement()
        {
            enemy.gameObject.SetActive(false); order.Begin(destination); yield return new WaitForSeconds(.1f);
            var pause = Object.FindAnyObjectByType<GamePause>(); pause.TryPause(); yield return null;
            int scans = order.DetectionScanCount; Vector2 position = ally.Unit.Position; yield return new WaitForSecondsRealtime(.3f);
            Assert.That(order.IsActive, Is.True); Assert.That(order.DetectionScanCount, Is.EqualTo(scans)); Assert.That(ally.Unit.Position, Is.EqualTo(position));
            Assert.That(order.Begin(Vector2.zero), Is.False); pause.Resume(); yield return new WaitForSeconds(.2f);
            Assert.That(Vector2.Distance(ally.Unit.Position, position), Is.GreaterThan(.01f));
        }
        [UnityTest]
        public IEnumerator InvalidOrdersAndDisableCannotLeaveAutomaticCombatRunning()
        {
            Assert.That(order.Begin(new Vector2(float.NaN, 0)), Is.False); Assert.That(order.Begin(new Vector2(float.PositiveInfinity, 0)), Is.False);
            order.Begin(destination); yield return Until(() => ally.Target == enemy); order.enabled = false;
            Assert.That(order.IsActive, Is.False); Assert.That(ally.Target, Is.Null); Assert.That(ally.Unit.HasDestination, Is.False);
            order.enabled = true; Assert.That(order.IsActive, Is.False); ally.ReceiveDamage(int.MaxValue);
            Assert.That(order.Begin(destination), Is.False);
        }
        [UnityTest]
        public IEnumerator NewRecruitAndSceneReloadHaveFreshAttackMoveOrders()
        {
            enemy.gameObject.SetActive(false); var recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); Assert.That(recruitment.TryTrain(), Is.True);
            yield return Until(() => !recruitment.IsTraining);
            var recruited = recruitment.GetComponentInChildren<AttackMoveOrder>(); Assert.That(recruited, Is.Not.Null); Assert.That(recruited.IsActive, Is.False);
            Assert.That(recruited.Begin(new Vector2(-18, -6)), Is.True);
            yield return SceneManager.LoadSceneAsync("BasicCombat"); yield return null;
            Assert.That(Object.FindObjectsByType<AttackMoveOrder>().Length, Is.EqualTo(4));
            Assert.That(Object.FindObjectsByType<AttackMoveOrder>().All(a => !a.IsActive), Is.True);
        }
        [UnityTest]
        public IEnumerator UiAndConstructionToolRejectAttackMoveChord()
        {
            enemy.gameObject.SetActive(false); Place(ally, new Vector2(-6, 1)); var point = (Vector2)Camera.main.WorldToScreenPoint(ally.Unit.Position);
            yield return Pointer(point, 1); yield return Pointer(point); yield return Keys(Key.A);
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected, Does.Contain(ally.Unit));
            var button = (RectTransform)Object.FindAnyObjectByType<PauseHUD>().transform.Find("Pause");
            point = RectTransformUtility.WorldToScreenPoint(null, button.TransformPoint(button.rect.center)); yield return Pointer(point, 2); yield return Pointer(point);
            Assert.That(order.IsActive, Is.False);
            Object.FindAnyObjectByType<ProjectF.Construction.WallPlacementController>().Toggle();
            point = Camera.main.WorldToScreenPoint(new Vector2(-2, 5)); yield return Pointer(point, 2); yield return Pointer(point);
            Assert.That(order.IsActive, Is.False);
        }
        [UnityTest]
        public IEnumerator AdditionalSelectionReceivesOneSharedAttackMoveDestination()
        {
            enemy.gameObject.SetActive(false); Place(ally, new Vector2(-6, 1));
            var second = GameObject.Find("Test Unit 2").GetComponent<UnitCombat>();
            var point = (Vector2)Camera.main.WorldToScreenPoint(ally.Unit.Position);
            yield return Pointer(point, 1); yield return Pointer(point); yield return Keys(Key.LeftShift);
            point = Camera.main.WorldToScreenPoint(second.Unit.Position); yield return Pointer(point, 1); yield return Pointer(point);
            Assert.That(Object.FindAnyObjectByType<UnitCommandController>().Selected.Count, Is.EqualTo(2));
            yield return Keys(Key.A); point = Camera.main.WorldToScreenPoint(new Vector2(-2, 5));
            yield return Pointer(point, 2); yield return Pointer(point); yield return Keys();
            var other = second.GetComponent<AttackMoveOrder>();
            Assert.That(order.IsActive && other.IsActive, Is.True); Assert.That(other.Destination, Is.EqualTo(order.Destination));
        }
    }
}
