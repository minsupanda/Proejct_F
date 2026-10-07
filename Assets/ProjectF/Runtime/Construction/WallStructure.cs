using System;
using ProjectF.Combat;
using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Construction
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WallStructure : MonoBehaviour
    {
        private NavigationWorld2D navigation;
        private BoxCollider2D shape;
        public event Action HealthChanged;
        public event Action Died;
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsAlive => Health > 0 && !Retired && isActiveAndEnabled && shape != null && shape.enabled;
        internal Collider2D Shape => shape;
        internal WallConstruction Owner { get; private set; }
        public int PaidWood { get; private set; }
        public int MaximumRefundWood { get; private set; }
        // Integer arithmetic keeps rounding stable even at the largest supported costs.
        public int RefundWood => MaxHealth > 0 ? (int)((long)MaximumRefundWood * Health / MaxHealth) : 0;
        public int RepairWoodCost => MaxHealth > 0 && Health < MaxHealth
            ? (int)(((long)PaidWood * (MaxHealth - Health) + MaxHealth - 1) / MaxHealth) : 0;
        internal bool Retired { get; private set; }
        internal void Initialize(NavigationWorld2D world, WallConstruction owner, int refundWood, int paidWood)
        {
            Initialize(world);
            Owner = owner;
            MaximumRefundWood = refundWood;
            PaidWood = paidWood;
            MaxHealth = Health = owner.Settings.WallMaxHealth;
        }
        public void Initialize(NavigationWorld2D world)
        {
            if (gameObject.activeInHierarchy) throw new System.InvalidOperationException("Initialize walls before activation.");
            navigation = world != null ? world : throw new System.ArgumentNullException(nameof(world));
        }
        private void Awake() => shape = GetComponent<BoxCollider2D>();
        public Vector2 ClosestPoint(Vector2 point) => shape.ClosestPoint(point);
        /// <summary>Only hostile units can damage walls. Null represents environmental damage.</summary>
        public void ReceiveDamage(int damage, UnitCombat source = null)
        {
            if (!IsAlive || damage <= 0 || (source != null && !source.CanAttackWall(this))) return;
            Health = Mathf.Max(0, Health - damage);
            HealthChanged?.Invoke();
            if (Health != 0) return;
            Retire();
            Died?.Invoke();
            // Combat destruction never goes through the demolition/refund transaction.
            Destroy(gameObject);
        }
        private void OnEnable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
        internal void RestoreFullHealth()
        {
            Health = MaxHealth;
            HealthChanged?.Invoke();
        }
        private void OnDisable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
        internal void Retire()
        {
            Retired = true;
            // Remove the collider now; Destroy itself is deferred until the end of the frame.
            gameObject.SetActive(false);
        }
    }
}
