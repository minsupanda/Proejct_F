using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectF.Input
{
    /// <summary>Owns pointer commands and gameplay shortcuts.</summary>
    public sealed class CommandInput : MonoBehaviour
    {
        private InputActionMap actions;
        public const int ControlGroupCount = 9;
        private InputAction point, select, command, pan, zoom, cancel, additiveSelection, stop, storeGroup;
        private readonly InputAction[] groups = new InputAction[ControlGroupCount];
        private bool focused = true;

        public bool Ready => isActiveAndEnabled && focused;
        public Vector2 Pointer => Ready ? point.ReadValue<Vector2>() : Vector2.zero;
        public bool SelectPressed => Ready && select.WasPressedThisFrame();
        public bool SelectReleased => Ready && select.WasReleasedThisFrame();
        public bool SelectHeld => Ready && select.IsPressed();
        public bool AdditiveSelectionHeld => Ready && additiveSelection.IsPressed();
        public bool CommandPressed => Ready && command.WasPressedThisFrame();
        public bool CommandHeld => Ready && command.IsPressed();
        public bool CancelPressed => Ready && cancel.WasPressedThisFrame();
        public bool StopPressed => Ready && stop.WasPressedThisFrame();
        public bool StoreGroupHeld => Ready && storeGroup.IsPressed();
        /// <summary>Zero-based slot pressed this frame, or -1. Lowest number wins simultaneous presses.</summary>
        public int GroupPressed
        {
            get
            {
                if (Ready)
                    for (int i = 0; i < groups.Length; i++)
                        if (groups[i].WasPressedThisFrame()) return i;
                return -1;
            }
        }
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
            additiveSelection = actions.AddAction("AdditiveSelection", InputActionType.Button, "<Keyboard>/leftShift");
            additiveSelection.AddBinding("<Keyboard>/rightShift");
            stop = actions.AddAction("Stop", InputActionType.Button, "<Keyboard>/s");
            storeGroup = actions.AddAction("StoreGroup", InputActionType.Button, "<Keyboard>/leftCtrl");
            storeGroup.AddBinding("<Keyboard>/rightCtrl");
            for (int i = 0; i < groups.Length; i++)
                groups[i] = actions.AddAction($"Group{i + 1}", InputActionType.Button, $"<Keyboard>/{i + 1}");
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
