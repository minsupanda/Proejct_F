using UnityEngine;

namespace ProjectF.Construction
{
    [CreateAssetMenu(menuName = "Project F/Construction Settings")]
    public sealed class ConstructionSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int wallWoodCost = 10;
        [SerializeField, Min(1)] private float gridSize = 1;
        [SerializeField, Range(0, 100)] private int demolitionRefundPercent = 100;
        [SerializeField, Min(1)] private int wallMaxHealth = 60;
        [SerializeField] private Rect territory = new Rect(-12, -7, 12, 14);
        public int WallWoodCost => Mathf.Max(1, wallWoodCost);
        public float GridSize => Mathf.Max(1, gridSize);
        public Rect Territory => territory;
        public int DemolitionRefundPercent => Mathf.Clamp(demolitionRefundPercent, 0, 100);
        public int WallMaxHealth => Mathf.Max(1, wallMaxHealth);
        public int GetDemolitionRefund(int paidWood) => (int)((long)Mathf.Max(0, paidWood) * DemolitionRefundPercent / 100);
        public Vector2 Snap(Vector2 point) => new Vector2(Mathf.Round(point.x / GridSize), Mathf.Round(point.y / GridSize)) * GridSize;
    }
}
