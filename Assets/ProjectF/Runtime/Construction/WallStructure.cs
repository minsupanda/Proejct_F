using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Construction
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WallStructure : MonoBehaviour
    {
        private NavigationWorld2D navigation;
        internal WallConstruction Owner { get; private set; }
        public int RefundWood { get; private set; }
        internal bool Retired { get; private set; }
        internal void Initialize(NavigationWorld2D world, WallConstruction owner, int refundWood)
        {
            Initialize(world);
            Owner = owner;
            RefundWood = refundWood;
        }
        public void Initialize(NavigationWorld2D world)
        {
            if (gameObject.activeInHierarchy) throw new System.InvalidOperationException("Initialize walls before activation.");
            navigation = world != null ? world : throw new System.ArgumentNullException(nameof(world));
        }
        private void OnEnable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
        private void OnDisable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
        internal void Retire()
        {
            Retired = true;
            // Remove the collider now; Destroy itself is deferred until the end of the frame.
            gameObject.SetActive(false);
        }
    }
}
