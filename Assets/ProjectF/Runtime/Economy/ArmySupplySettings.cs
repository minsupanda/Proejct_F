using System;
using UnityEngine;

namespace ProjectF.Economy
{
    [CreateAssetMenu(menuName = "Project F/Army Supply Settings")]
    public sealed class ArmySupplySettings : ScriptableObject
    {
        [SerializeField, Range(0, 100)] private int foodPerDefender = 1;
        [SerializeField, Range(25, 100)] private int unfedDamagePercent = 75;
        public int FoodPerDefender => Mathf.Clamp(foodPerDefender, 0, 100);
        public int UnfedDamagePercent => Mathf.Clamp(unfedDamagePercent, 25, 100);
        public int RequiredFood(int defenders) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, defenders) * FoodPerDefender);
        public int DamagePercent(int required, int supplied)
        {
            if (required <= 0) return 100;
            return UnfedDamagePercent + (int)((long)(100 - UnfedDamagePercent) * Mathf.Clamp(supplied, 0, required) / required);
        }
    }
}
