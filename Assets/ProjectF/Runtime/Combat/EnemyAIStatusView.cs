using UnityEngine;

namespace ProjectF.Combat
{
    [RequireComponent(typeof(EnemyCombatAI))]
    public sealed class EnemyAIStatusView : MonoBehaviour
    {
        [SerializeField] private TextMesh label;
        private EnemyCombatAI ai;
        private void Awake() => ai = GetComponent<EnemyCombatAI>();
        private void OnEnable() { ai.StateChanged += Refresh; Refresh(ai.State); }
        private void OnDisable() => ai.StateChanged -= Refresh;
        private void Refresh(EnemyAIState state)
        {
            if (label == null) return;
            switch (state)
            {
                case EnemyAIState.Guarding: label.text = "GUARD"; label.color = new Color(.7f, .85f, .8f); break;
                case EnemyAIState.Engaging: label.text = "ATTACK"; label.color = new Color(1, .6f, .35f); break;
                case EnemyAIState.Returning: label.text = "RETURN"; label.color = new Color(.5f, .8f, 1); break;
                case EnemyAIState.Advancing: label.text = "ADVANCE"; label.color = new Color(.9f, .55f, 1); break;
            }
        }
    }
}
