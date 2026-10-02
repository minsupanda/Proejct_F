using UnityEngine;

namespace ProjectF.Construction
{
    [RequireComponent(typeof(WallStructure))]
    public sealed class WallHealthView : MonoBehaviour
    {
        [SerializeField] private Transform healthFill;
        private WallStructure wall;
        private void Awake() => wall = GetComponent<WallStructure>();
        private void OnEnable() { wall.HealthChanged += Refresh; Refresh(); }
        private void OnDisable() => wall.HealthChanged -= Refresh;
        private void Refresh()
        {
            if (healthFill == null) return;
            float ratio = wall.MaxHealth > 0 ? (float)wall.Health / wall.MaxHealth : 0;
            healthFill.localScale = new Vector3(ratio, 1, 1);
            healthFill.localPosition = new Vector3((ratio - 1) * .5f, 0, -.01f);
        }
    }
}
