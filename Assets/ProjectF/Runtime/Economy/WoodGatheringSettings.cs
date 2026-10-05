using UnityEngine;

namespace ProjectF.Economy
{
    [CreateAssetMenu(menuName = "Project F/Wood Gathering Settings")]
    public sealed class WoodGatheringSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int woodPerCycle = 5;
        [SerializeField, Min(.1f)] private float cycleSeconds = 2f;
        [SerializeField, Min(.2f)] private float gatheringReach = .8f;
        [SerializeField, Min(.1f)] private float approachRetrySeconds = .5f;
        [SerializeField, Min(1)] private float stalledSeconds = 6f;
        public int WoodPerCycle => Mathf.Max(1, woodPerCycle);
        public float CycleSeconds => Mathf.Max(.1f, cycleSeconds);
        public float GatheringReach => Mathf.Max(.2f, gatheringReach);
        public float ApproachRetrySeconds => Mathf.Max(.1f, approachRetrySeconds);
        public float StalledSeconds => Mathf.Max(1, stalledSeconds);
    }
}
