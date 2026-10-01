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
        private bool lastCanConstruct;
        private void OnEnable()
        {
            stockpile.Changed += Refresh;
            construction.Changed += Refresh;
            placement.Changed += Refresh;
            button.onClick.AddListener(placement.Toggle);
            if (demolishButton != null) demolishButton.onClick.AddListener(placement.ToggleDemolition);
            Refresh();
        }
        private void OnDisable()
        {
            stockpile.Changed -= Refresh;
            construction.Changed -= Refresh;
            placement.Changed -= Refresh;
            button.onClick.RemoveListener(placement.Toggle);
            if (demolishButton != null) demolishButton.onClick.RemoveListener(placement.ToggleDemolition);
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
            string hint = "Build before invasion";
            if (placement.IsDemolishing)
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
