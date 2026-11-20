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
        // Pending recruitment refunds retain their storage space until deployment or cancellation.
        public int ReservedFoodCapacity { get; private set; }
        public int FoodCapacity => Mathf.Max(1, foodCapacity);
        private void Awake()
        {
            Wood = Mathf.Max(0, startingWood);
            Food = Mathf.Clamp(startingFood, 0, FoodCapacity);
        }
        private void Start() => Changed?.Invoke();
        public bool CanAddFood(int amount) => isActiveAndEnabled && amount > 0 && amount <= FoodCapacity - Food - ReservedFoodCapacity;

        internal bool CanPay(int wood, int food) => isActiveAndEnabled && wood > 0 && food >= 0
            && Wood >= wood && Food >= food;

        internal bool TryPay(int wood, int food, Action<EstatePayment> commit)
        {
            if (!CanPay(wood, food)) return false;
            var payment = new EstatePayment(this, wood, food);
            Wood -= wood; Food -= food; ReservedFoodCapacity += food;
            try { commit(payment); }
            finally { Changed?.Invoke(); }
            return true;
        }

        internal bool CanRefund(EstatePayment payment) => Owns(payment) && CanAddWood(payment.Wood)
            && payment.Food <= FoodCapacity - Food;

        internal bool TryRefund(EstatePayment payment, Action commit)
        {
            if (!CanRefund(payment)) return false;
            payment.Pending = false;
            ReservedFoodCapacity -= payment.Food;
            Wood += payment.Wood; Food += payment.Food;
            try { commit(); }
            finally { Changed?.Invoke(); }
            return true;
        }

        internal void CompletePayment(EstatePayment payment, Action commit = null)
        {
            if (!Owns(payment)) return;
            payment.Pending = false; ReservedFoodCapacity -= payment.Food;
            try { commit?.Invoke(); }
            finally { Changed?.Invoke(); }
        }

        private bool Owns(EstatePayment payment) => payment != null && payment.Owner == this && payment.Pending;
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

    /// <summary>A single purchase receipt, settled only by its issuing stockpile.</summary>
    internal sealed class EstatePayment
    {
        internal EstatePayment(EstateStockpile owner, int wood, int food)
        { Owner = owner; Wood = wood; Food = food; }
        internal EstateStockpile Owner { get; }
        internal int Wood { get; }
        internal int Food { get; }
        internal bool Pending { get; set; } = true;
    }
}
