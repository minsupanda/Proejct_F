using UnityEngine;

namespace ProjectF.Construction
{
    [CreateAssetMenu(menuName = "Project F/Construction Settings")]
    public sealed class ConstructionSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int wallWoodCost = 10;
        [SerializeField, Min(1)] private float gridSize = 1;
        [SerializeField] private Rect territory = new Rect(-12, -7, 12, 14);
        public int WallWoodCost => Mathf.Max(1, wallWoodCost);
        public float GridSize => Mathf.Max(1, gridSize);
        public Rect Territory => territory;
        public Vector2 Snap(Vector2 point) => new Vector2(Mathf.Round(point.x / GridSize), Mathf.Round(point.y / GridSize)) * GridSize;
    }
}
