using UnityEngine;

namespace ProjectF.Combat
{
    [CreateAssetMenu(menuName = "Project F/Lord Growth Settings")]
    public sealed class LordGrowthSettings : ScriptableObject
    {
        [SerializeField, Range(1, 10)] private int pointsPerVictory = 1;
        [SerializeField, Range(1, 10)] private int maximumRank = 3;
        [SerializeField, Range(1, 100)] private int damagePerRank = 2;
        [SerializeField, Range(1, 10)] private int capacityPerRank = 1;
        public int PointsPerVictory => Mathf.Clamp(pointsPerVictory, 1, 10);
        public int MaximumRank => Mathf.Clamp(maximumRank, 1, 10);
        public int DamagePerRank => Mathf.Clamp(damagePerRank, 1, 100);
        public int CapacityPerRank => Mathf.Clamp(capacityPerRank, 1, 10);
    }
}
