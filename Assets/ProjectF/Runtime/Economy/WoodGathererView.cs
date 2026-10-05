using UnityEngine;

namespace ProjectF.Economy
{
    [RequireComponent(typeof(WoodGatherer))]
    public sealed class WoodGathererView : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        private WoodGatherer gatherer;
        private void Awake() => gatherer = GetComponent<WoodGatherer>();
        private void OnEnable() { gatherer.Changed += Refresh; Refresh(); }
        private void OnDisable() { gatherer.Changed -= Refresh; if (label != null) label.text = ""; }
        private void Refresh()
        {
            if (label == null) return;
            switch (gatherer.State)
            {
                case GatheringState.Moving: label.text = "TO WOOD"; break;
                case GatheringState.Gathering: label.text = "GATHER"; break;
                case GatheringState.Blocked: label.text = "NO PATH"; break;
                case GatheringState.StorageFull: label.text = "WOOD FULL"; break;
                default: label.text = ""; break;
            }
        }
    }
}
