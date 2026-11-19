using UnityEngine;

namespace ProjectF.Economy
{
    [CreateAssetMenu(menuName = "Project F/Farm Settings")]
    public sealed class FarmSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int woodCost = 30;
        [SerializeField, Min(.1f)] private float harvestSeconds = 8;
        [SerializeField, Min(1)] private int foodPerHarvest = 4;
        public int WoodCost => Mathf.Max(1, woodCost);
        public float HarvestSeconds => Mathf.Max(.1f, harvestSeconds);
        public int FoodPerHarvest => Mathf.Clamp(foodPerHarvest, 1, 100);
    }
}
