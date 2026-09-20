using UnityEngine;

namespace ProjectF.Player
{
    [CreateAssetMenu(menuName = "Project F/Lord Movement Settings")]
    public sealed class LordMovementSettings : ScriptableObject
    {
        [SerializeField, Min(0f), Tooltip("Movement speed in world units per second. 0 stops movement.")]
        private float moveSpeed = 5f;

        public float MoveSpeed => Mathf.Max(0f, moveSpeed);

        private void OnValidate() => moveSpeed = Mathf.Max(0f, moveSpeed);
    }
}
