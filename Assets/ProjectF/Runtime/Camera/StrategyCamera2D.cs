using ProjectF.Input;
using UnityEngine;

namespace ProjectF.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public sealed class StrategyCamera2D : MonoBehaviour
    {
        [SerializeField] private CommandInput input;
        [SerializeField] private UnitCommandController commands;
        [SerializeField, Min(.1f)] private float minimumSize = 5f;
        [SerializeField, Min(.1f)] private float maximumSize = 14f;
        [SerializeField] private Rect mapBounds = new Rect(-24, -16, 48, 32);
        private Camera view;
        private Vector2 previousPointer;
        private bool panning;

        private void Awake() => view = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (!input.Ready) { panning = false; return; }
            Vector2 pointer = input.Pointer;
            bool canStart = view.pixelRect.Contains(pointer) && !commands.PointerOverUI() && !input.SelectHeld;
            if (input.PanHeld)
            {
                if (panning)
                {
                    Vector3 delta = view.ScreenToWorldPoint(previousPointer) - view.ScreenToWorldPoint(pointer);
                    transform.position += delta;
                }
                else if (canStart) panning = true;
            }
            else panning = false;
            previousPointer = pointer;
            if (canStart && !input.PanHeld && Mathf.Abs(input.Scroll) > .01f)
            {
                Vector3 before = view.ScreenToWorldPoint(pointer);
                float factor = Mathf.Exp(-Mathf.Clamp(input.Scroll / 120f, -10, 10) * .15f);
                view.orthographicSize = Mathf.Clamp(view.orthographicSize * factor, minimumSize, maximumSize);
                transform.position += before - view.ScreenToWorldPoint(pointer);
            }
            ClampPosition();
        }

        private void ClampPosition()
        {
            float halfHeight = view.orthographicSize;
            float halfWidth = halfHeight * view.aspect;
            Vector3 position = transform.position;
            position.x = halfWidth * 2 >= mapBounds.width ? mapBounds.center.x :
                Mathf.Clamp(position.x, mapBounds.xMin + halfWidth, mapBounds.xMax - halfWidth);
            position.y = halfHeight * 2 >= mapBounds.height ? mapBounds.center.y :
                Mathf.Clamp(position.y, mapBounds.yMin + halfHeight, mapBounds.yMax - halfHeight);
            transform.position = position;
        }

        private void OnDisable() => panning = false;
    }
}
