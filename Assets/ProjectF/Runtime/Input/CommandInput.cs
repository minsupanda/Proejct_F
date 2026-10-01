using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectF.Input
{
    /// <summary>Owns pointer commands and the shared cancel shortcut.</summary>
    public sealed class CommandInput : MonoBehaviour
    {
        private InputActionMap actions;
        private InputAction point, select, command, pan, zoom, cancel;
        private bool focused = true;

        public bool Ready => isActiveAndEnabled && focused;
        public Vector2 Pointer => Ready ? point.ReadValue<Vector2>() : Vector2.zero;
        public bool SelectPressed => Ready && select.WasPressedThisFrame();
        public bool SelectReleased => Ready && select.WasReleasedThisFrame();
        public bool SelectHeld => Ready && select.IsPressed();
        public bool CommandPressed => Ready && command.WasPressedThisFrame();
        public bool CommandHeld => Ready && command.IsPressed();
        public bool CancelPressed => Ready && cancel.WasPressedThisFrame();
        public bool PanHeld => Ready && pan.IsPressed();
        public float Scroll => Ready ? zoom.ReadValue<Vector2>().y : 0f;

        private void Awake()
        {
            actions = new InputActionMap("MouseCommands");
            point = actions.AddAction("Pointer", InputActionType.PassThrough, "<Mouse>/position");
            select = actions.AddAction("Select", InputActionType.Button, "<Mouse>/leftButton");
            command = actions.AddAction("MoveCommand", InputActionType.Button, "<Mouse>/rightButton");
            pan = actions.AddAction("Pan", InputActionType.Button, "<Mouse>/middleButton");
            zoom = actions.AddAction("Zoom", InputActionType.PassThrough, "<Mouse>/scroll");
            cancel = actions.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable() { if (focused) actions.Enable(); }
        private void OnDisable() => actions.Disable();
        private void OnDestroy() => actions.Dispose();
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (value && isActiveAndEnabled) actions.Enable();
            else actions.Disable();
        }
    }
}
