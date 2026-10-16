using UnityEngine;

namespace ProjectF.Combat
{
    public sealed class LordGrowthHUD : MonoBehaviour
    {
        [SerializeField] private LordGrowth growth;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private UnityEngine.UI.Text martialLabel, leadershipLabel;
        [SerializeField] private UnityEngine.UI.Button martialButton, leadershipButton;
        private bool canInvest;
        private void OnEnable()
        {
            growth.Changed += Refresh;
            martialButton.onClick.AddListener(UpgradeMartial);
            leadershipButton.onClick.AddListener(UpgradeLeadership);
            Refresh();
        }
        private void OnDisable()
        {
            growth.Changed -= Refresh;
            martialButton.onClick.RemoveListener(UpgradeMartial);
            leadershipButton.onClick.RemoveListener(UpgradeLeadership);
        }
        private void Update() { if (canInvest != growth.CanInvest) Refresh(); }
        private void UpgradeMartial() => growth.TryUpgrade(LordDiscipline.Martial);
        private void UpgradeLeadership() => growth.TryUpgrade(LordDiscipline.Leadership);
        private void Refresh()
        {
            canInvest = growth.CanInvest;
            status.text = "LORD GROWTH / POINTS " + growth.Points
                + "\nLORD ATK +" + growth.DamageBonus + " / ARMY +" + growth.CapacityBonus
                + "\n" + (canInvest ? growth.Points > 0 ? "Spend 1 point per upgrade" : "Repel invasions to earn points"
                    : "Invest when safe / lord alive");
            martialLabel.text = "MARTIAL " + growth.MartialRank + "/" + growth.Settings.MaximumRank + " / ATK +" + growth.Settings.DamagePerRank;
            leadershipLabel.text = "LEADERSHIP " + growth.LeadershipRank + "/" + growth.Settings.MaximumRank + " / ARMY +" + growth.Settings.CapacityPerRank;
            martialButton.interactable = growth.CanUpgrade(LordDiscipline.Martial);
            leadershipButton.interactable = growth.CanUpgrade(LordDiscipline.Leadership);
        }
    }
}
