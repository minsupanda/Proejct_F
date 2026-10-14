using UnityEngine;

namespace ProjectF.Combat
{
    [CreateAssetMenu(menuName = "Project F/Attack Move Settings")]
    public sealed class AttackMoveSettings : ScriptableObject
    {
        [SerializeField, Min(.1f)] private float detectionRadius = 6;
        [SerializeField, Min(.1f)] private float pursuitRadius = 8;
        [SerializeField, Min(.05f)] private float scanInterval = .25f;
        [SerializeField, Min(.1f)] private float stalledSeconds = 3;
        [SerializeField, Min(.1f)] private float recoverySeconds = 1;
        public float DetectionRadius => Mathf.Max(.1f, detectionRadius);
        public float PursuitRadius => Mathf.Max(DetectionRadius, pursuitRadius);
        public float ScanInterval => Mathf.Max(.05f, scanInterval);
        public float StalledSeconds => Mathf.Max(.1f, stalledSeconds);
        public float RecoverySeconds => Mathf.Max(.1f, recoverySeconds);
    }
}
