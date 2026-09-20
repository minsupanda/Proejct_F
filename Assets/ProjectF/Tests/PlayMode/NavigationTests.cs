using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectF.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectF.Tests
{
    public sealed class NavigationTests
    {
        private CommandableUnit[] units;
        private NavigationWorld2D navigation;
        private readonly List<CommandableUnit> participants = new List<CommandableUnit>();
        private readonly List<ScriptableObject> temporarySettings = new List<ScriptableObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("InputCamera");
            units = Object.FindObjectsByType<CommandableUnit>().OrderBy(u => u.name).ToArray();
            navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            participants.Clear();
            var demo = GameObject.Find("Navigation Obstacles");
            if (demo != null) demo.SetActive(false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            foreach (var settings in temporarySettings) Object.Destroy(settings);
            temporarySettings.Clear();
        }

        private void ConfigureBody(CommandableUnit unit, float scale, float speed)
        {
            unit.transform.localScale = Vector3.one * scale;
            var settings = ScriptableObject.CreateInstance<LordMovementSettings>();
            temporarySettings.Add(settings);
            typeof(LordMovementSettings).GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(settings, speed);
            typeof(CommandableUnit).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(unit, settings);
            Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty();
        }

        private void Place(params Vector2[] positions)
        {
            for (int i = 0; i < units.Length; i++)
            {
                units[i].gameObject.SetActive(i < positions.Length);
                if (i >= positions.Length) continue;
                units[i].GetComponent<Rigidbody2D>().position = positions[i];
                units[i].transform.position = positions[i];
                participants.Add(units[i]);
            }
            Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty();
        }

        private GameObject Wall(Vector2 position, Vector2 size)
        {
            var go = new GameObject("Navigation Test Wall", typeof(BoxCollider2D));
            go.transform.position = position;
            go.GetComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty();
            return go;
        }

        private void AssertSeparated()
        {
            for (int i = 0; i < participants.Count; i++)
                for (int j = i + 1; j < participants.Count; j++)
                    Assert.That(Vector2.Distance(participants[i].Position, participants[j].Position),
                        Is.GreaterThanOrEqualTo(participants[i].Radius + participants[j].Radius - .015f), "Units overlapped");
        }

        private IEnumerator Finish(float timeout = 8)
        {
            float until = Time.time + timeout;
            while (participants.Any(u => u.HasDestination) && Time.time < until)
            {
                AssertSeparated();
                yield return new WaitForFixedUpdate();
            }
            AssertSeparated();
            foreach (var unit in participants)
            {
                Assert.That(unit.HasDestination, Is.False, $"{unit.name}: {unit.TravelState}, position {unit.Position}, goal {unit.Destination}, safety stops {navigation.SafetyStops}");
                Assert.That(unit.TravelState, Is.EqualTo(UnitTravelState.Arrived));
                Assert.That(Vector2.Distance(unit.Position, unit.Destination), Is.LessThan(.08f));
            }
        }

        [UnityTest]
        public IEnumerator OpposingOrdersYieldAndBothArrive()
        {
            Place(new Vector2(-6, 0), new Vector2(6, 0));
            units[0].MoveTo(new Vector2(6, 0));
            yield return new WaitForSeconds(.12f);
            units[1].MoveTo(new Vector2(-6, 0));
            Assert.That(units[0].CommandOrder, Is.LessThan(units[1].CommandOrder));
            yield return Finish();
            Assert.That(Vector2.Distance(units[0].Position, new Vector2(6, 0)), Is.LessThan(.08f));
            Assert.That(Vector2.Distance(units[1].Position, new Vector2(-6, 0)), Is.LessThan(.08f));
        }

        [UnityTest]
        public IEnumerator FourWayCrossingHasNoOverlap()
        {
            Place(new Vector2(-7, 0), new Vector2(7, 0), new Vector2(0, -7), new Vector2(0, 7));
            foreach (var unit in units) unit.MoveTo(-unit.Position);
            yield return Finish(10);
        }

        [UnityTest]
        public IEnumerator RoutesAroundWallWithBodyClearance()
        {
            Place(new Vector2(-6, 0));
            var wall = Wall(Vector2.zero, new Vector2(2, 6)).GetComponent<Collider2D>();
            units[0].MoveTo(new Vector2(6, 0));
            float maxY = 0, until = Time.time + 8;
            while (units[0].HasDestination && Time.time < until)
            {
                maxY = Mathf.Max(maxY, Mathf.Abs(units[0].Position.y));
                Assert.That(wall.Distance(units[0].GetComponent<Collider2D>()).isOverlapped, Is.False);
                yield return new WaitForFixedUpdate();
            }
            Assert.That(maxY, Is.GreaterThan(3.35f));
            yield return Finish();
        }

        [UnityTest]
        public IEnumerator StationaryUnitIsNotPushed()
        {
            Place(new Vector2(-5, 0), Vector2.zero);
            participants.Remove(units[1]);
            units[0].MoveTo(new Vector2(5, 0));
            yield return Finish();
            Assert.That(units[1].Position.magnitude, Is.LessThan(.02f));
        }

        [UnityTest]
        public IEnumerator NarrowPassageDoesNotDeadlockOpposingOrders()
        {
            Place(new Vector2(-6, 0), new Vector2(6, 0));
            Wall(new Vector2(0, 1.1f), new Vector2(8, .4f));
            Wall(new Vector2(0, -1.1f), new Vector2(8, .4f));
            units[0].MoveTo(new Vector2(6, 0)); units[1].MoveTo(new Vector2(-6, 0));
            yield return Finish(12);
        }

        [UnityTest]
        public IEnumerator SingleLaneRequiresRetreatToPassingSpace()
        {
            Place(new Vector2(-6, 0), new Vector2(6, 0));
            Wall(new Vector2(0, 8), new Vector2(20, 14));
            Wall(new Vector2(0, -8), new Vector2(20, 14));
            units[0].MoveTo(new Vector2(12, 0)); units[1].MoveTo(new Vector2(-12, 0));
            yield return Finish(16);
        }

        [UnityTest]
        public IEnumerator CloseUnitsCanSeparateWithoutFreezingEveryone()
        {
            Place(new Vector2(-.4f, 0), new Vector2(.4f, 0));
            units[0].MoveTo(new Vector2(-4, 0)); units[1].MoveTo(new Vector2(4, 0));
            yield return Finish();
        }

        [UnityTest]
        public IEnumerator RemovedObstacleReopensShortRoute()
        {
            Place(new Vector2(-6, 0));
            var wall = Wall(Vector2.zero, new Vector2(2, 8));
            units[0].MoveTo(new Vector2(6, 0));
            yield return new WaitForSeconds(.6f);
            Object.Destroy(wall);
            navigation.MarkObstaclesDirty();
            yield return Finish(3.3f);
        }

        [UnityTest]
        public IEnumerator SharedGoalGetsDistinctArrivalPositions()
        {
            Place(new Vector2(-4, -4), new Vector2(4, -4), new Vector2(-4, 4), new Vector2(4, 4));
            foreach (var unit in units) unit.MoveTo(Vector2.zero);
            yield return Finish(10);
            foreach (var unit in units) Assert.That(unit.Position.magnitude, Is.LessThan(2));
        }

        [UnityTest]
        public IEnumerator GoalInsideObstacleUsesNearbyFreeGround()
        {
            Place(new Vector2(-5, 0));
            var wall = Wall(Vector2.zero, new Vector2(2, 2)).GetComponent<Collider2D>();
            units[0].MoveTo(Vector2.zero);
            yield return Finish();
            Assert.That(wall.OverlapPoint(units[0].Destination), Is.False);
            Assert.That(units[0].Destination.magnitude, Is.LessThan(3));
        }

        [UnityTest]
        public IEnumerator SealedRouteWaitsSafelyAndRecoversWhenOpened()
        {
            Place(new Vector2(-5, 0));
            var wall = Wall(Vector2.zero, new Vector2(1, 40));
            units[0].MoveTo(new Vector2(5, 0));
            yield return new WaitForSeconds(2);
            Assert.That(units[0].HasDestination, Is.True);
            Assert.That(units[0].Position.x, Is.LessThan(-.87f));
            Vector2 held = units[0].Position;
            yield return new WaitForSeconds(.7f);
            Assert.That(Vector2.Distance(held, units[0].Position), Is.LessThan(.1f));
            Object.Destroy(wall); navigation.MarkObstaclesDirty();
            yield return Finish(4);
        }

        [UnityTest]
        public IEnumerator InfantryKeepsItsNarrowRouteWithLargeCavalryPresent()
        {
            Place(new Vector2(-6, 0), new Vector2(0, 6));
            ConfigureBody(units[1], 2, 8);
            participants.Remove(units[1]);
            Wall(new Vector2(0, 1), new Vector2(12, .6f));
            Wall(new Vector2(0, -1), new Vector2(12, .6f));
            units[0].MoveTo(new Vector2(6, 0));
            float until = Time.time + 2.8f;
            while (units[0].HasDestination && Time.time < until)
            {
                Assert.That(Mathf.Abs(units[0].Position.y), Is.LessThan(.05f));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(units[0].TravelState, Is.EqualTo(UnitTravelState.Arrived));
        }

        [UnityTest]
        public IEnumerator FastLargeCavalryPassesSlowerInfantryWithoutPushing()
        {
            Place(new Vector2(-6, 0), new Vector2(-10, 0));
            ConfigureBody(units[0], 1, 3);
            ConfigureBody(units[1], 1.8f, 8);
            units[0].MoveTo(new Vector2(10, 0)); units[1].MoveTo(new Vector2(12, 0));
            bool overtook = false;
            float until = Time.time + 10;
            while (participants.Any(u => u.HasDestination) && Time.time < until)
            {
                AssertSeparated();
                overtook |= units[0].HasDestination && units[1].Position.x > units[0].Position.x + 1.1f;
                yield return new WaitForFixedUpdate();
            }
            Assert.That(overtook, Is.True);
            yield return Finish();
        }

        [UnityTest]
        public IEnumerator SixtyFourUnitsGatherBeyondOldFourUnitRadius()
        {
            Place(new Vector2(-14, -7));
            for (int row = 0; row < 8; row++)
                for (int column = 0; column < 8; column++)
                {
                    if (row == 0 && column == 0) continue;
                    var copy = Object.Instantiate(units[0], new Vector3(-14 + column * 2, -7 + row * 2, 0), Quaternion.identity);
                    copy.name = "Gather Unit " + participants.Count;
                    participants.Add(copy);
                }
            Vector2 target = new Vector2(9, 0);
            participants.Sort((a, b) => (a.Position - target).sqrMagnitude.CompareTo((b.Position - target).sqrMagnitude));
            foreach (var unit in participants) unit.MoveTo(target);
            yield return Finish(25);
            float maximum = participants.Max(u => Vector2.Distance(u.Position, target));
            Assert.That(maximum, Is.GreaterThan(4));
            Assert.That(maximum, Is.LessThan(5.6f));
            Debug.Log($"Gather stress: 64 units, radius {maximum:F2}, peak plan {navigation.PeakPlanMilliseconds:F2} ms, mean plan {navigation.TotalPlanMilliseconds / navigation.ReplanCount:F2} ms, replans {navigation.ReplanCount}, safety stops {navigation.SafetyStops}");
        }

        [UnityTest]
        public IEnumerator MixedSizeGroupGathersWithoutOverlapping()
        {
            Place(new Vector2(8, 0));
            for (int i = 0; i < 12; i++)
            {
                CommandableUnit unit;
                if (i == 0) unit = units[0];
                else
                {
                    float angle = i * Mathf.PI * 2 / 12;
                    unit = Object.Instantiate(units[0], new Vector3(Mathf.Cos(angle) * 8, Mathf.Sin(angle) * 8, 0), Quaternion.identity);
                    unit.name = "Mixed Gather " + i;
                    participants.Add(unit);
                }
                ConfigureBody(unit, i % 3 == 0 ? 1.8f : 1, i % 3 == 0 ? 8 : 5);
            }
            foreach (var unit in participants) unit.MoveTo(Vector2.zero);
            yield return Finish(15);
            foreach (var unit in participants) Assert.That(unit.Position.magnitude, Is.LessThan(4));
        }

        [UnityTest]
        public IEnumerator ThirtyTwoMixedUnitsCrossFromFourDirections()
        {
            Place(new Vector2(-12, -3));
            for (int group = 0; group < 4; group++)
                for (int i = 0; i < 8; i++)
                {
                    Vector2 p = new Vector2(-12 - i / 4 * 2, -3 + i % 4 * 2);
                    for (int turn = 0; turn < group; turn++) p = new Vector2(-p.y, p.x);
                    CommandableUnit unit;
                    if (group == 0 && i == 0) unit = units[0];
                    else
                    {
                        unit = Object.Instantiate(units[0], p, Quaternion.identity);
                        unit.name = "Cross Unit " + participants.Count;
                        participants.Add(unit);
                    }
                    ConfigureBody(unit, i % 4 == 0 ? 1.6f : 1, i % 4 == 0 ? 8 : 4);
                }
            foreach (var unit in participants) unit.MoveTo(-unit.Position);
            yield return Finish(25);
            Debug.Log($"Mixed crossing: 32 units, peak plan {navigation.PeakPlanMilliseconds:F2} ms, mean plan {navigation.TotalPlanMilliseconds / navigation.ReplanCount:F2} ms, replans {navigation.ReplanCount}, safety stops {navigation.SafetyStops}");
        }

        [UnityTest]
        public IEnumerator SpawnedUnitJoinsAnAlreadyRunningPlanner()
        {
            Place(new Vector2(-6, 0));
            units[0].MoveTo(new Vector2(6, 0));
            yield return new WaitForSeconds(.25f);
            var copy = Object.Instantiate(units[0], new Vector3(3, 4, 0), Quaternion.identity);
            copy.name = "Late Spawn";
            participants.Add(copy);
            copy.MoveTo(new Vector2(6, 4));
            yield return Finish();
        }

        [UnityTest]
        public IEnumerator SixteenOpposingUnitsCompleteWithoutOverlap()
        {
            Place(new Vector2(-8, -7), new Vector2(8, -7));
            for (int row = 1; row < 8; row++)
                for (int side = 0; side < 2; side++)
                {
                    var copy = Object.Instantiate(units[side], new Vector3(side == 0 ? -8 : 8, -7 + row * 2, 0), Quaternion.identity);
                    copy.name = "Stress Unit " + participants.Count;
                    participants.Add(copy);
                }
            Physics2D.SyncTransforms(); navigation.MarkObstaclesDirty();
            foreach (var unit in participants) unit.MoveTo(new Vector2(-unit.Position.x, unit.Position.y));
            yield return Finish(16);
            Debug.Log($"Navigation stress: 16 units, peak plan {navigation.PeakPlanMilliseconds:F2} ms, mean plan {navigation.TotalPlanMilliseconds / navigation.ReplanCount:F2} ms, peak terrain {navigation.PeakTerrainMilliseconds:F2} ms, replans {navigation.ReplanCount}, safety stops {navigation.SafetyStops}");
        }
    }
}
