using ProjectF.Economy;
using UnityEngine;

namespace ProjectF.Construction
{
    public sealed class ConstructionHUD : MonoBehaviour
    {
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private WallConstruction construction;
        [SerializeField] private WallPlacementController placement;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private UnityEngine.UI.Text buttonLabel;
        [SerializeField] private UnityEngine.UI.Button demolishButton;
        [SerializeField] private UnityEngine.UI.Text demolishLabel;
        [SerializeField] private UnityEngine.UI.Button repairButton;
        [SerializeField] private UnityEngine.UI.Text repairLabel;
        private bool lastCanConstruct;
        private void OnEnable()
        {
            stockpile.Changed += Refresh;
            construction.Changed += Refresh;
            placement.Changed += Refresh;
            button.onClick.AddListener(placement.Toggle);
            if (demolishButton != null) demolishButton.onClick.AddListener(placement.ToggleDemolition);
            if (repairButton != null) repairButton.onClick.AddListener(placement.ToggleRepair);
            Refresh();
        }
        private void OnDisable()
        {
            stockpile.Changed -= Refresh;
            construction.Changed -= Refresh;
            placement.Changed -= Refresh;
            button.onClick.RemoveListener(placement.Toggle);
            if (demolishButton != null) demolishButton.onClick.RemoveListener(placement.ToggleDemolition);
            if (repairButton != null) repairButton.onClick.RemoveListener(placement.ToggleRepair);
        }
        private void Refresh()
        {
            lastCanConstruct = construction.CanConstruct;
            button.interactable = placement.IsPlacing || (construction.CanConstruct && construction.CanAfford);
            buttonLabel.text = placement.IsPlacing ? "CANCEL BUILD" : "BUILD WALL";
            if (demolishButton != null)
            {
                demolishButton.interactable = placement.IsDemolishing || construction.CanConstruct;
                demolishLabel.text = placement.IsDemolishing ? "CANCEL REMOVE" : "REMOVE WALL";
            }
            if (repairButton != null)
            {
                repairButton.interactable = placement.IsRepairing || construction.CanConstruct;
                repairLabel.text = placement.IsRepairing ? "CANCEL REPAIR" : "REPAIR WALL";
            }
            string hint = "Build before invasion";
            if (placement.IsRepairing)
            {
                if (placement.RepairTarget == null) hint = "Point at a damaged wall / Esc: cancel";
                else if (placement.RepairStatus == RepairResult.AlreadyHealthy) hint = "Wall is already at full health";
                else
                {
                    hint = "HP " + placement.RepairTarget.Health + "/" + placement.RepairTarget.MaxHealth
                        + " / Repair " + placement.RepairCost + " wood";
                    hint += placement.RepairStatus == RepairResult.InsufficientWood ? " / Not enough" : " / LMB";
                }
            }
            else if (placement.IsDemolishing)
            {
                if (placement.DemolitionStatus == DemolitionResult.RefundOverflow) hint = "Wood storage full";
                else if (placement.DemolitionTarget != null)
                    hint = "Remove: +" + placement.DemolitionTarget.RefundWood + " wood / LMB";
                else hint = "Point at a built wall / Esc: cancel";
            }
            else if (placement.IsPlacing)
            {
                switch (placement.Result)
                {
                    case PlacementResult.Available: hint = "LMB: build  /  RMB or Esc: cancel"; break;
                    case PlacementResult.OutsideTerritory: hint = "Outside estate boundary"; break;
                    case PlacementResult.Occupied: hint = "Blocked by a unit, wall or terrain"; break;
                    case PlacementResult.InsufficientWood: hint = "Not enough wood"; break;
                    default: hint = "Construction unavailable"; break;
                }
            }
            else if (!construction.CanAfford) hint = "Not enough wood";
            status.text = "WOOD " + stockpile.Wood + "  /  WALL " + construction.Settings.WallWoodCost + "  /  " + hint;
        }
        private void Update()
        {
            // Time.timeScale has no change event. Refresh text only when availability changes.
            if (lastCanConstruct != construction.CanConstruct) Refresh();
        }
    }
}
