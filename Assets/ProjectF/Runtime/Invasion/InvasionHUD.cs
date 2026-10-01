using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectF.Invasion
{
    /// <summary>Presentation only. Wave rules remain in PortalInvasion.</summary>
    public sealed class InvasionHUD : MonoBehaviour
    {
        [SerializeField] private PortalInvasion invasion;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private UnityEngine.UI.Button action;
        [SerializeField] private UnityEngine.UI.Text actionLabel;
        private int seconds = -1;
        private bool loading;
        private void OnEnable() { invasion.Changed += Refresh; action.onClick.AddListener(OnAction); Refresh(); }
        private void OnDisable() { invasion.Changed -= Refresh; action.onClick.RemoveListener(OnAction); }
        private void Update()
        {
            if (invasion.State != InvasionState.Preparing) return;
            int value = Mathf.CeilToInt(invasion.PreparationRemaining);
            if (value != seconds) { seconds = value; Refresh(); }
        }
        private void Refresh()
        {
            action.interactable = !loading && (invasion.State == InvasionState.Ready || IsTerminal);
            actionLabel.text = IsTerminal ? "RESTART" : "START INVASION";
            if (invasion.ExitBlocked) { status.text = "PORTAL EXIT BLOCKED  /  Waiting for a clear exit"; return; }
            switch (invasion.State)
            {
                case InvasionState.Ready: status.text = "FIRST INVASION  /  Position your units, then open the portal"; break;
                case InvasionState.Preparing: status.text = "PORTAL OPENING  /  " + Mathf.CeilToInt(invasion.PreparationRemaining) + "s"; break;
                case InvasionState.Spawning:
                case InvasionState.Fighting: status.text = "FIRST INVASION  /  Arrived " + invasion.SpawnedCount + "/" + invasion.TotalCount + "  /  Alive " + invasion.ActiveCount; break;
                case InvasionState.Repelled: status.text = "INVASION REPELLED  /  All portal raiders defeated"; break;
                case InvasionState.Defeated: status.text = "DEFEAT  /  All allied units lost"; break;
                case InvasionState.Cancelled: status.text = "INVASION CANCELLED"; break;
            }
        }
        private bool IsTerminal => invasion.State == InvasionState.Repelled || invasion.State == InvasionState.Defeated || invasion.State == InvasionState.Cancelled;
        private void OnAction()
        {
            if (loading || Time.timeScale <= 0) return;
            if (!IsTerminal) { invasion.Begin(); return; }
            loading = true;
            Refresh();
            SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
        }
    }
}
