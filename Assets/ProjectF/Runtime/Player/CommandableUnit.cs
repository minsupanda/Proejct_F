using UnityEngine;

namespace ProjectF.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class CommandableUnit : MonoBehaviour
    {
        [SerializeField] private LordMovementSettings settings;
        [SerializeField] private GameObject selectionVisual;
        [SerializeField] private Transform destinationVisual;
        [SerializeField] private NavigationWorld2D navigation;
        private Rigidbody2D body;
        private CircleCollider2D circle;
        public bool IsSelected { get; private set; }
        public bool HasDestination { get; private set; }
        public Vector2 Destination { get; private set; }
        public Vector2 RequestedDestination { get; private set; }
        public long CommandOrder { get; private set; }
        public UnitTravelState TravelState { get; private set; }
        public float MoveSpeed => settings != null ? settings.MoveSpeed : 0;
        public float Radius => circle != null ? circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)) : .38f;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            circle = GetComponent<CircleCollider2D>();
            if (settings == null)
            {
                Debug.LogError("Unit movement settings are missing.", this);
                enabled = false;
            }
            SetSelected(false);
            if (destinationVisual != null) destinationVisual.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (navigation != null) navigation.Register(this);
            else { Debug.LogError("Unit navigation world is missing.", this); enabled = false; }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionVisual != null) selectionVisual.SetActive(selected);
        }

        public void MoveTo(Vector2 destination)
        {
            if (!isActiveAndEnabled) return;
            Destination = destination;
            RequestedDestination = destination;
            CommandOrder = navigation.NewOrder();
            HasDestination = true;
            TravelState = UnitTravelState.Waiting;
            if (destinationVisual != null)
            {
                destinationVisual.position = new Vector3(destination.x, destination.y, .45f);
                destinationVisual.gameObject.SetActive(true);
            }
        }

        internal void SetResolvedDestination(Vector2 destination)
        {
            Destination = destination;
            if (destinationVisual != null) destinationVisual.position = new Vector3(destination.x, destination.y, .45f);
        }

        internal void ApplyNavigationVelocity(Vector2 velocity) { if (body != null) body.linearVelocity = velocity; }
        internal void SetTravelState(UnitTravelState state) => TravelState = state;
        internal void Arrive() { Stop(); TravelState = UnitTravelState.Arrived; }
        internal void RejectDestination() { Stop(); TravelState = UnitTravelState.NoPath; }

        private void Stop()
        {
            HasDestination = false;
            TravelState = UnitTravelState.Idle;
            if (body != null) body.linearVelocity = Vector2.zero;
            if (destinationVisual != null) destinationVisual.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (navigation != null) navigation.Unregister(this);
            Stop(); SetSelected(false);
        }
    }
}
