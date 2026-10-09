using UnityEngine;
using UnityEngine.UI;

namespace ProjectF.Combat
{
    public sealed class RecruitmentRallyHUD : MonoBehaviour
    {
        [SerializeField] private UnitRecruitment recruitment;
        [SerializeField] private RecruitmentRallyController controller;
        [SerializeField] private Button setButton, clearButton;
        [SerializeField] private Text setLabel, status;
        [SerializeField] private GameObject recruitmentStatus;
        private bool preparation;
        private void OnEnable()
        {
            recruitment.Changed += Refresh; controller.Changed += Refresh;
            setButton.onClick.AddListener(controller.Toggle); clearButton.onClick.AddListener(controller.Clear); Refresh();
        }
        private void OnDisable()
        {
            recruitment.Changed -= Refresh; controller.Changed -= Refresh;
            setButton.onClick.RemoveListener(controller.Toggle); clearButton.onClick.RemoveListener(controller.Clear);
            if (recruitmentStatus != null) recruitmentStatus.SetActive(true);
        }
        private void Update() { if (preparation != recruitment.PreparationAvailable) Refresh(); }
        private void Refresh()
        {
            // Scene roots can be torn down in either order during restart or unload.
            if (recruitment == null || controller == null || setButton == null || clearButton == null || status == null || setLabel == null) return;
            preparation = recruitment.PreparationAvailable;
            setButton.interactable = controller.isActiveAndEnabled && preparation;
            clearButton.interactable = controller.isActiveAndEnabled && preparation && recruitment.HasRallyPoint;
            setLabel.text = controller.IsChoosing ? "CANCEL RALLY" : "SET RALLY";
            status.gameObject.SetActive(controller.IsChoosing);
            if (recruitmentStatus != null) recruitmentStatus.SetActive(!controller.IsChoosing);
            if (controller.IsChoosing)
                status.text = (!controller.PreviewVisible || controller.Result == RallyPointResult.Available ? "Click open ground"
                    : controller.Result == RallyPointResult.OutsideMap ? "Outside map" : "Blocked ground") + "\nRMB / Esc: cancel";
        }
    }
}
