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
    public sealed class SelectionStatusTests
    {
        private InputTestFixture fixture;
        private Mouse mouse;
        private Keyboard keyboard;
        private UnitCombat[] allies;
        private UnitCommandController commands;
        private SelectionStatusHUD hud;
        private GameObject panel;
        private UnityEngine.UI.Text label;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("BasicCombat");
            allies = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).Where(c => c.IsPlayerControlled).OrderBy(c => c.name).ToArray();
            BindHUD(); yield return Refresh();
        }
        private void BindHUD()
        {
            commands = Object.FindAnyObjectByType<UnitCommandController>(); hud = Object.FindAnyObjectByType<SelectionStatusHUD>();
            panel = hud.transform.Find("Panel").gameObject; label = panel.GetComponentInChildren<UnityEngine.UI.Text>(true);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; yield return SceneManager.LoadSceneAsync("SampleScene"); fixture.TearDown();
        }
        private IEnumerator Refresh() { yield return new WaitForSecondsRealtime(.13f); }
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
        private IEnumerator SelectAll()
        {
            yield return Keys(Key.LeftShift); foreach (var ally in allies) yield return Click(ally.Unit.Position);
            yield return Keys(); yield return Refresh();
        }
        private void Contains(string value) => StringAssert.Contains(value, label.text);

        [UnityTest]
        public IEnumerator EmptySelectionHidesPanelAndMultiSelectionShowsCombinedHealth()
        {
            Assert.That(panel.activeSelf, Is.False);
            yield return SelectAll(); Assert.That(panel.activeSelf, Is.True); Contains("SELECTED 4");
            Contains($"HEALTH {allies.Sum(c => c.Health)} / {allies.Sum(c => c.MaxHealth)}"); Contains("IDLE 4");
            yield return Click(Vector2.zero); yield return Refresh(); Assert.That(panel.activeSelf, Is.False); Assert.That(label.text, Is.Empty);
        }
        [UnityTest]
        public IEnumerator DamageDeathDisableAndDestroyUpdateTotalsWithoutReselection()
        {
            yield return SelectAll(); allies[0].ReceiveDamage(20); yield return Refresh();
            Contains($"HEALTH {allies.Sum(c => c.Health)} / {allies.Sum(c => c.MaxHealth)}");
            allies[1].ReceiveDamage(int.MaxValue); allies[2].Unit.enabled = false; Object.Destroy(allies[3].gameObject);
            yield return Refresh(); Contains("SELECTED 1"); Contains($"HEALTH {allies[0].Health} / {allies[0].MaxHealth}");
            allies[0].ReceiveDamage(int.MaxValue); yield return Refresh(); Assert.That(panel.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator MixedOrdersCountEachUnitOnceAndStopReturnsThemToIdle()
        {
            yield return SelectAll(); CombatTestScene.AddGuards(); yield return null;
            foreach (var ai in Object.FindObjectsByType<EnemyCombatAI>(FindObjectsSortMode.None)) ai.enabled = false;
            var enemy = Object.FindObjectsByType<UnitCombat>(FindObjectsSortMode.None).First(c => c.Faction == UnitFaction.Hostile);
            allies[0].Unit.MoveTo(new Vector2(-16, 5)); Assert.That(allies[1].Attack(enemy), Is.True);
            var tree = Object.FindObjectsByType<WoodResourceNode>(FindObjectsSortMode.None).First();
            Assert.That(allies[2].GetComponent<WoodGatherer>().Gather(tree), Is.True);
            yield return Refresh(); Contains("MOVE 1  ATTACK 1"); Contains("GATHER 1  IDLE 1");
            yield return Pointer(Screen(Vector2.zero)); yield return Keys(Key.S); yield return Keys(); yield return Refresh();
            Contains("MOVE 0  ATTACK 0"); Contains("GATHER 0  IDLE 4");
        }
        [UnityTest]
        public IEnumerator GroupRecallAndShiftRemovalUpdateSummaryWithoutChangingOrders()
        {
            yield return SelectAll(); yield return Keys(Key.LeftCtrl, Key.Digit1); yield return Keys();
            yield return Click(allies[0].Unit.Position); yield return Refresh(); Contains("SELECTED 1");
            allies[0].Unit.MoveTo(new Vector2(-16, 5)); long order = allies[0].Unit.CommandOrder;
            yield return Keys(Key.Digit1); yield return Keys(); yield return Refresh(); Contains("SELECTED 4"); Contains("MOVE 1");
            Assert.That(allies[0].Unit.CommandOrder, Is.EqualTo(order));
            yield return Keys(Key.LeftShift); yield return Click(allies[1].Unit.Position); yield return Keys(); yield return Refresh(); Contains("SELECTED 3");
        }
        [UnityTest]
        public IEnumerator VisiblePanelBlocksWorldClicksAndKeyboardCommands()
        {
            yield return Click(allies[0].Unit.Position); yield return Refresh(); allies[0].Unit.MoveTo(new Vector2(-16, 5));
            long order = allies[0].Unit.CommandOrder;
            var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)panel.transform).TransformPoint(((RectTransform)panel.transform).rect.center));
            yield return Pointer(point); Assert.That(commands.PointerOverUI(), Is.True);
            yield return Pointer(point, 1); yield return Pointer(point); yield return Pointer(point, 2); yield return Pointer(point);
            yield return Keys(Key.S, Key.Digit9); yield return Keys(); yield return Refresh();
            Assert.That(commands.Selected, Is.EquivalentTo(new[] {allies[0].Unit})); Assert.That(allies[0].Unit.CommandOrder, Is.EqualTo(order)); Contains("SELECTED 1");
        }
        [UnityTest]
        public IEnumerator PausedSimulationStillRefreshesHealthAndSelectionDisplay()
        {
            yield return Click(allies[0].Unit.Position); yield return Refresh(); Time.timeScale = 0;
            allies[0].ReceiveDamage(15); yield return Refresh(); Contains($"HEALTH {allies[0].Health} / {allies[0].MaxHealth}");
            yield return Click(allies[1].Unit.Position); yield return Refresh(); Contains($"HEALTH {allies[1].Health} / {allies[1].MaxHealth}");
        }
        [UnityTest]
        public IEnumerator DisabledHudStopsBlockingAndReenableRestoresCurrentSummary()
        {
            yield return Click(allies[0].Unit.Position); yield return Refresh(); hud.enabled = false;
            Assert.That(panel.activeSelf, Is.False);
            var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)panel.transform).TransformPoint(((RectTransform)panel.transform).rect.center));
            yield return Pointer(point); Assert.That(commands.PointerOverUI(), Is.False);
            yield return Click(allies[1].Unit.Position); hud.enabled = true; yield return Refresh(); Assert.That(panel.activeSelf, Is.True); Contains("SELECTED 1");
            commands.enabled = false; yield return Refresh(); Assert.That(panel.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator SceneRestartHasOneHudAndNoStaleSelection()
        {
            yield return SelectAll(); yield return SceneManager.LoadSceneAsync("BasicCombat"); BindHUD(); yield return Refresh();
            Assert.That(Object.FindObjectsByType<SelectionStatusHUD>(FindObjectsSortMode.None).Length, Is.EqualTo(1)); Assert.That(panel.activeSelf, Is.False);
            // SelectAll leaves the mouse over Test Unit 4. Reload must read this stationary pointer.
            var ally = GameObject.Find("Test Unit 4").GetComponent<UnitCombat>();
            Assert.That(Object.FindAnyObjectByType<CommandInput>().Pointer, Is.EqualTo(Screen(ally.Unit.Position)),
                "Reload must initialize the pointer even when the mouse has not moved.");
            yield return Click(ally.Unit.Position); yield return Refresh(); Contains("SELECTED 1"); Contains($"HEALTH {ally.Health} / {ally.MaxHealth}");
        }
        [UnityTest]
        public IEnumerator NewlyTrainedSoldierIsIncludedWithoutExtraWiring()
        {
            var recruitment = Object.FindAnyObjectByType<UnitRecruitment>(); var original = recruitment.Settings; var settings = Object.Instantiate(original);
            var field = typeof(UnitRecruitment).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
            typeof(RecruitmentSettings).GetField("trainingSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, .1f); field.SetValue(recruitment, settings);
            try
            {
                Assert.That(recruitment.TryTrain(), Is.True); float end = Time.time + 3;
                while (recruitment.IsTraining && Time.time < end) yield return null;
                Assert.That(recruitment.IsTraining, Is.False); var soldier = recruitment.GetComponentInChildren<UnitCombat>();
                yield return Click(soldier.Unit.Position); yield return Refresh(); Contains("SELECTED 1"); Contains($"HEALTH {soldier.Health} / {soldier.MaxHealth}");
                soldier.ReceiveDamage(10); yield return Refresh(); Contains($"HEALTH {soldier.Health} / {soldier.MaxHealth}");
            }
            finally { field.SetValue(recruitment, original); Object.Destroy(settings); }
        }
    }
}
