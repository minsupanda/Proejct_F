using UnityEngine;

namespace ProjectF.Economy
{
    [RequireComponent(typeof(WoodResourceNode))]
    public sealed class WoodResourceView : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        [SerializeField] private GameObject foliage;
        private WoodResourceNode node;
        private void Awake() => node = GetComponent<WoodResourceNode>();
        private void OnEnable() { node.Changed += Refresh; Refresh(); }
        private void OnDisable() => node.Changed -= Refresh;
        private void Refresh()
        {
            if (label != null) label.text = node.Remaining > 0 ? "WOOD " + node.Remaining : "DEPLETED";
            if (foliage != null) foliage.SetActive(node.Remaining > 0);
        }
    }
}
