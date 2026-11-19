using System;
using UnityEngine;

namespace ProjectF.Economy
{
    /// <summary>Scene-owned resources. Runtime spending never modifies starting resources.</summary>
    public sealed class EstateStockpile : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingWood = 80;
        [SerializeField, Min(0)] private int startingFood;
        [SerializeField, Min(1)] private int foodCapacity = 200;
        public event Action Changed;
        public int Wood { get; private set; }
        public int Food { get; private set; }
        public int FoodCapacity => Mathf.Max(1, foodCapacity);
        private void Awake()
        {
            Wood = Mathf.Max(0, startingWood);
            Food = Mathf.Clamp(startingFood, 0, FoodCapacity);
        }
        private void Start() => Changed?.Invoke();
        public bool CanAddFood(int amount) => isActiveAndEnabled && amount > 0 && amount <= FoodCapacity - Food;
        public bool TryAddFood(int amount) => TryAddFood(amount, null);
        internal bool TryAddFood(int amount, Action commit)
        {
            if (!CanAddFood(amount)) return false;
            Food += amount;
            try { commit?.Invoke(); }
            finally { Changed?.Invoke(); }
            return true;
        }
        public bool TrySpendFood(int amount)
        {
            if (!isActiveAndEnabled || amount <= 0 || Food < amount) return false;
            Food -= amount;
            Changed?.Invoke();
            return true;
        }
        public bool CanAddWood(int amount) => isActiveAndEnabled && amount > 0 && Wood <= int.MaxValue - amount;
        public bool TryAddWood(int amount) => TryAddWood(amount, null);
        // Commit a refund's owning state before observers see the new balance.
        internal bool TryAddWood(int amount, Action commit)
        {
            if (!CanAddWood(amount)) return false;
            Wood += amount;
            try { commit?.Invoke(); }
            finally { Changed?.Invoke(); }
            return true;
        }
        public bool TrySpendWood(int amount) => TrySpendWood(amount, null);
        // Finish a validated purchase before resource observers can react to the new balance.
        internal bool TrySpendWood(int amount, Action commit)
        {
            if (!isActiveAndEnabled || amount <= 0 || Wood < amount) return false;
            Wood -= amount;
            try { commit?.Invoke(); }
            finally { Changed?.Invoke(); }
            return true;
        }
    }
}
