using UnityEngine;

namespace ProjectF.Invasion
{
    [CreateAssetMenu(menuName = "Project F/Invasion Settings")]
    public sealed class InvasionSettings : ScriptableObject
    {
        [SerializeField, Range(1, 32)] private int unitCount = 3;
        [SerializeField, Range(1, 10)] private int waveCount = 1;
        [SerializeField, Range(0, 32)] private int additionalUnitsPerWave = 1;
        [SerializeField, Min(0)] private float preparationSeconds = 3;
        [SerializeField, Min(.1f)] private float spawnInterval = 1.5f;
        [SerializeField, Min(.1f)] private float blockedRetryInterval = .5f;
        public int UnitCount => Mathf.Clamp(unitCount, 1, 32);
        public int WaveCount => Mathf.Clamp(waveCount, 1, 10);
        public int AdditionalUnitsPerWave => Mathf.Clamp(additionalUnitsPerWave, 0, 32);
        public float PreparationSeconds => Mathf.Max(0, preparationSeconds);
        public float SpawnInterval => Mathf.Max(.1f, spawnInterval);
        public float BlockedRetryInterval => Mathf.Max(.1f, blockedRetryInterval);
    }
}
