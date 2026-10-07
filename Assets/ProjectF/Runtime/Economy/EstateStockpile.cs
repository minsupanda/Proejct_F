using System;
using UnityEngine;

namespace ProjectF.Economy
{
    /// <summary>Scene-owned resources. Runtime spending never modifies starting resources.</summary>
    public sealed class EstateStockpile : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingWood = 80;
        public event Action Changed;
        public int Wood { get; private set; }
        private void Awake() => Wood = Mathf.Max(0, startingWood);
        private void Start() => Changed?.Invoke();
        public bool CanAddWood(int amount) => isActiveAndEnabled && amount > 0 && Wood <= int.MaxValue - amount;
        public bool TryAddWood(int amount)
        {
            if (!CanAddWood(amount)) return false;
            Wood += amount;
            Changed?.Invoke();
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
