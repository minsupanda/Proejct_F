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
        public bool TrySpendWood(int amount)
        {
            if (!isActiveAndEnabled || amount <= 0 || Wood < amount) return false;
            Wood -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
