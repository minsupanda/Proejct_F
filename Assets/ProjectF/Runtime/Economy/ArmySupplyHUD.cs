using UnityEngine;

namespace ProjectF.Economy
{
    public sealed class ArmySupplyHUD : MonoBehaviour
    {
        [SerializeField] private ArmySupply supply;
        [SerializeField] private UnityEngine.UI.Text status;
        private int lastRequired;
        private float nextRosterCheck;
        private void OnEnable() { supply.Changed += Refresh; Refresh(); }
        private void OnDisable() { supply.Changed -= Refresh; }
        private void Update()
        {
            if (Time.unscaledTime < nextRosterCheck) return;
            nextRosterCheck = Time.unscaledTime + .25f;
            if (!supply.IsDefenseActive && lastRequired != supply.RequiredFood) Refresh();
        }
        private void Refresh()
        {
            lastRequired = supply.RequiredFood;
            status.text = supply.IsDefenseActive
                ? "ARMY SUPPLY / ATK " + supply.CurrentDamagePercent + "%\nRATIONS " + supply.LastConsumedFood + " / " + supply.LastRequiredFood + " FOOD\nFixed for this defense"
                : "ARMY SUPPLY / NEXT DEFENSE\nNEED " + lastRequired + " / HAVE " + supply.AvailableFood + " FOOD\nExpected ATK " + supply.PreviewDamagePercent + "%";
        }
    }
}
