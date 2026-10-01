using System.Reflection;
using ProjectF.Combat;
using ProjectF.Invasion;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Tests
{
    internal static class CombatTestScene
    {
        // Preserve the original three-opponent regression setup using the production prefab.
        internal static void AddGuards()
        {
            var invasion = Object.FindAnyObjectByType<PortalInvasion>();
            var prefab = (UnitCombat)typeof(PortalInvasion).GetField("enemyPrefab", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(invasion);
            var navigation = Object.FindAnyObjectByType<NavigationWorld2D>();
            for (int i = 0; i < 3; i++)
            {
                var guard = Object.Instantiate(prefab, new Vector3(i == 2 ? 7 : 4, i == 0 ? 1 : i == 1 ? -2 : -.5f, 0), Quaternion.identity);
                guard.name = "Enemy " + (i + 1);
                guard.GetComponent<CommandableUnit>().InitializeNavigation(navigation);
                guard.gameObject.SetActive(true);
            }
        }
    }
}
