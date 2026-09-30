using UnityEngine;

namespace ProjectF.Combat
{
    [CreateAssetMenu(menuName = "Project F/Combat Settings")]
    public sealed class CombatSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(1)] private int attackDamage = 15;
        [SerializeField, Min(.1f)] private float attackInterval = .8f;
        [Tooltip("Extra reach beyond the two units' body radii, in world units.")]
        [SerializeField, Min(.1f)] private float attackReach = .85f;
        [SerializeField, Min(.1f)] private float pursuitInterval = .3f;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int AttackDamage => Mathf.Max(1, attackDamage);
        public float AttackInterval => Mathf.Max(.1f, attackInterval);
        public float AttackReach => Mathf.Max(.1f, attackReach);
        public float PursuitInterval => Mathf.Max(.1f, pursuitInterval);
    }
}
