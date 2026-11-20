using UnityEngine;

namespace ProjectF.Combat
{
    [CreateAssetMenu(menuName = "Project F/Recruitment Settings")]
    public sealed class RecruitmentSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int woodCost = 20;
        [SerializeField, Min(0)] private int foodCost = 4;
        [SerializeField, Min(.1f)] private float trainingSeconds = 5;
        [SerializeField, Range(1, 64)] private int maximumAllies = 8;
        public int WoodCost => Mathf.Max(1, woodCost);
        public int FoodCost => Mathf.Max(0, foodCost);
        public float TrainingSeconds => Mathf.Max(.1f, trainingSeconds);
        public int MaximumAllies => Mathf.Clamp(maximumAllies, 1, 64);
    }
}
