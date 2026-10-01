using ProjectF.Player;
using UnityEngine;

namespace ProjectF.Construction
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WallStructure : MonoBehaviour
    {
        private NavigationWorld2D navigation;
        public void Initialize(NavigationWorld2D world)
        {
            if (gameObject.activeInHierarchy) throw new System.InvalidOperationException("Initialize walls before activation.");
            navigation = world != null ? world : throw new System.ArgumentNullException(nameof(world));
        }
        private void OnEnable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
        private void OnDisable() { if (navigation != null) navigation.MarkObstaclesDirty(); }
    }
}
