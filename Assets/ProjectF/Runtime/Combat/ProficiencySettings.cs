using UnityEngine;

namespace ProjectF.Combat
{
    [CreateAssetMenu(menuName = "Project F/Proficiency Settings")]
    public sealed class ProficiencySettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int trainedHits = 5;
        [SerializeField, Min(2)] private int veteranHits = 15;
        [SerializeField, Min(0)] private int trainedDamageBonus = 2;
        [SerializeField, Min(0)] private int veteranDamageBonus = 4;
        public int TrainedHits => Mathf.Clamp(trainedHits, 1, 9999);
        public int VeteranHits => Mathf.Clamp(veteranHits, TrainedHits + 1, 10000);
        public int TrainedDamageBonus => Mathf.Clamp(trainedDamageBonus, 0, 1000);
        public int VeteranDamageBonus => Mathf.Clamp(veteranDamageBonus, TrainedDamageBonus, 1000);
    }
}
