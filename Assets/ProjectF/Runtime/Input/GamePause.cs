using System;
using UnityEngine;

namespace ProjectF.Input
{
    /// <summary>Scene-owned pause. Retains orders and restores only the clock it paused.</summary>
    [DisallowMultipleComponent]
    public sealed class GamePause : MonoBehaviour
    {
        [SerializeField] private CommandInput input;
        [SerializeField] private UnitCommandController commands;
        private float resumeScale = 1;
        public bool IsPaused { get; private set; }
        public event Action Changed;

        private void Update()
        {
            if (input.PausePressed) Toggle();
        }
        public void Toggle()
        {
            if (IsPaused) Resume();
            else TryPause();
        }
        public bool TryPause()
        {
            if (!isActiveAndEnabled || IsPaused || Time.timeScale <= 0) return false;
            resumeScale = Time.timeScale;
            IsPaused = true;
            commands.SetPointerCommandsBlocked(this, true);
            Time.timeScale = 0;
            Changed?.Invoke();
            return true;
        }
        public bool Resume()
        {
            if (!IsPaused) return false;
            IsPaused = false;
            // Do not overwrite a clock value another system has already changed.
            if (Time.timeScale == 0) Time.timeScale = resumeScale;
            if (commands != null) commands.SetPointerCommandsBlocked(this, false);
            Changed?.Invoke();
            return true;
        }
        private void OnDisable() => Resume();
    }
}
