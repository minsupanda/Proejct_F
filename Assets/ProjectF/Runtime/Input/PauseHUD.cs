using UnityEngine;

namespace ProjectF.Input
{
    /// <summary>Pause controls and the modal input shield. GamePause owns the clock.</summary>
    public sealed class PauseHUD : MonoBehaviour
    {
        [SerializeField] private GamePause pause;
        [SerializeField] private UnityEngine.UI.Button pauseButton;
        [SerializeField] private UnityEngine.UI.Button resumeButton;
        [SerializeField] private GameObject overlay;
        private void OnEnable()
        {
            pause.Changed += Refresh;
            pauseButton.onClick.AddListener(OnPause);
            resumeButton.onClick.AddListener(OnResume);
            Refresh();
        }
        private void OnDisable()
        {
            pause.Changed -= Refresh;
            pauseButton.onClick.RemoveListener(OnPause);
            resumeButton.onClick.RemoveListener(OnResume);
            overlay.SetActive(false);
        }
        private void OnPause() => pause.TryPause();
        private void OnResume() => pause.Resume();
        private void Refresh()
        {
            overlay.SetActive(pause.IsPaused);
            pauseButton.gameObject.SetActive(!pause.IsPaused);
        }
    }
}
