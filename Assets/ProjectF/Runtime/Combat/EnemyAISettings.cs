using UnityEngine;

namespace ProjectF.Combat
{
    [CreateAssetMenu(menuName = "Project F/Enemy AI Settings")]
    public sealed class EnemyAISettings : ScriptableObject
    {
        [SerializeField, Min(.5f)] private float detectionRadius = 5f;
        [Tooltip("Maximum distance from the guard's starting position, for both guard and target.")]
        [SerializeField, Min(1)] private float leashRadius = 8f;
        [SerializeField, Min(.1f)] private float thinkInterval = .25f;
        [SerializeField, Min(.1f)] private float lostSightDelay = 2f;
        [SerializeField, Min(.5f)] private float stalledPursuitTimeout = 4f;
        [SerializeField, Min(.1f)] private float returnTolerance = .6f;
        [SerializeField, Min(.1f)] private float returnRetryInterval = 1f;
        [SerializeField, Min(.1f)] private float recoveryDelay = 1f;
        [Tooltip("Check for the first wall on the forward line to the invasion destination.")]
        [SerializeField, Min(.5f)] private float wallDetectionDistance = 4f;
        [SerializeField, Min(.5f)] private float wallRetryDelay = 4f;

        public float DetectionRadius => Mathf.Max(.5f, detectionRadius);
        public float LeashRadius => Mathf.Max(DetectionRadius, leashRadius);
        public float ThinkInterval => Mathf.Max(.1f, thinkInterval);
        public float LostSightDelay => Mathf.Max(ThinkInterval, lostSightDelay);
        public float StalledPursuitTimeout => Mathf.Max(ThinkInterval, stalledPursuitTimeout);
        public float ReturnTolerance => Mathf.Clamp(returnTolerance, .1f, DetectionRadius * .5f);
        public float ReturnRetryInterval => Mathf.Max(ThinkInterval, returnRetryInterval);
        public float RecoveryDelay => Mathf.Max(ThinkInterval, recoveryDelay);
        public float WallDetectionDistance => Mathf.Max(.5f, wallDetectionDistance);
        public float WallRetryDelay => Mathf.Max(ThinkInterval, wallRetryDelay);
    }
}
