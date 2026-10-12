using UnityEngine;
using UnityEngine.UI;

namespace ProjectF.Combat
{
    public sealed class RecruitmentHUD : MonoBehaviour
    {
        [SerializeField] private UnitRecruitment recruitment;
        [SerializeField] private Text status;
        [SerializeField] private Button button;
        [SerializeField] private Text buttonLabel;
        private int seconds = -1;
        private bool preparation;
        private bool canAct;
        private bool CanAct => recruitment.IsTraining ? recruitment.CanCancelTraining : recruitment.CanTrain;
        private void OnEnable() { recruitment.Changed += Refresh; button.onClick.AddListener(OnButtonClicked); Refresh(); }
        private void OnDisable() { recruitment.Changed -= Refresh; button.onClick.RemoveListener(OnButtonClicked); }
        private void OnButtonClicked()
        {
            if (recruitment.IsTraining) recruitment.TryCancelTraining();
            else recruitment.TryTrain();
        }
        private void Update()
        {
            if (seconds != Mathf.CeilToInt(recruitment.RemainingSeconds) || preparation != recruitment.PreparationAvailable
                || (recruitment.IsTraining && canAct != recruitment.CanCancelTraining)) Refresh();
        }
        private void Refresh()
        {
            seconds = Mathf.CeilToInt(recruitment.RemainingSeconds); preparation = recruitment.PreparationAvailable;
            canAct = CanAct; button.interactable = canAct;
            buttonLabel.text = recruitment.IsTraining ? "CANCEL +" + recruitment.CancellationRefund : "TRAIN SOLDIER";
            string hint = "Ready / " + recruitment.Settings.TrainingSeconds.ToString("0.#") + "s";
            if (recruitment.IsTraining)
            {
                if (!preparation) hint = "Paused until preparation";
                else if (recruitment.State == RecruitmentState.ExitBlocked) hint = "Clear the camp exits";
                else if (recruitment.State == RecruitmentState.AtCapacity) hint = "Waiting for army capacity";
                else hint = "Training / " + seconds + "s left";
            }
            else if (!preparation) hint = "Train during preparation";
            else if (recruitment.AlliedCount >= recruitment.Settings.MaximumAllies) hint = "Army capacity reached";
            else if (!recruitment.CanTrain) hint = "Not enough wood";
            status.text = "ARMY " + recruitment.AlliedCount + "/" + recruitment.Settings.MaximumAllies
                + " / " + recruitment.Settings.WoodCost + " WOOD\n" + hint;
        }
    }
}
