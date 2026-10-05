using System;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Economy
{
    /// <summary>Owns a finite deposit. Transfer reserves wood before notifying stockpile listeners.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class WoodResourceNode : MonoBehaviour
    {
        [SerializeField, Min(1)] private int startingWood = 40;
        [SerializeField] private NavigationWorld2D navigation;
        private BoxCollider2D shape;
        private bool transferring;
        public event Action Changed;
        public int Remaining { get; private set; }
        public bool IsAvailable => isActiveAndEnabled && Remaining > 0 && shape != null && shape.enabled;
        internal Collider2D Shape => shape;
        private void Awake() { shape = GetComponent<BoxCollider2D>(); Remaining = Mathf.Max(1, startingWood); }
        // Scene components may enable their views before this node's Awake runs.
        private void Start() => Changed?.Invoke();
        private void OnEnable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
        private void OnDisable() { if (navigation != null) navigation.MarkObstaclesDirty(); Changed?.Invoke(); }
        public Vector2 ClosestPoint(Vector2 point) => shape.ClosestPoint(point);

        internal int TransferWood(EstateStockpile stockpile, int requested)
        {
            if (!IsAvailable || transferring || stockpile == null || requested <= 0) return 0;
            int amount = Mathf.Min(requested, Remaining);
            if (!stockpile.CanAddWood(amount)) return 0;
            transferring = true;
            try
            {
                Remaining -= amount;
                if (!stockpile.TryAddWood(amount)) { Remaining += amount; return 0; }
                if (Remaining == 0)
                {
                    shape.enabled = false;
                    if (navigation != null) navigation.MarkObstaclesDirty();
                }
                Changed?.Invoke();
                return amount;
            }
            finally { transferring = false; }
        }
    }
}
