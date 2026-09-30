using UnityEngine;

namespace ProjectF.Combat
{
    /// <summary>Prototype health and strike visuals; gameplay never depends on this component.</summary>
    [RequireComponent(typeof(UnitCombat))]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] private Transform healthFill;
        [SerializeField] private LineRenderer strike;
        private UnitCombat combat;
        private float hideStrikeAt;

        private void Awake() => combat = GetComponent<UnitCombat>();
        // Other components may not have run Awake when our first OnEnable fires.
        private void Start() => RefreshHealth();
        private void OnEnable()
        {
            combat.HealthChanged += RefreshHealth;
            combat.Attacked += ShowStrike;
            RefreshHealth();
            if (strike != null) strike.enabled = false;
        }
        private void OnDisable()
        {
            combat.HealthChanged -= RefreshHealth;
            combat.Attacked -= ShowStrike;
            if (strike != null) strike.enabled = false;
        }
        private void RefreshHealth()
        {
            if (healthFill == null) return;
            float ratio = combat.MaxHealth > 0 ? (float)combat.Health / combat.MaxHealth : 0;
            healthFill.localScale = new Vector3(ratio, 1, 1);
            healthFill.localPosition = new Vector3((ratio - 1) * .5f, 0, -.01f);
        }
        private void ShowStrike(UnitCombat victim)
        {
            if (strike == null || victim == null) return;
            strike.SetPosition(0, new Vector3(transform.position.x, transform.position.y, -.3f));
            strike.SetPosition(1, new Vector3(victim.transform.position.x, victim.transform.position.y, -.3f));
            strike.enabled = true;
            hideStrikeAt = Time.time + .12f;
        }
        private void LateUpdate()
        {
            if (strike != null && strike.enabled && Time.time >= hideStrikeAt) strike.enabled = false;
        }
    }
}
