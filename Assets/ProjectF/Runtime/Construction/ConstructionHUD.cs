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
        private void OnEnable()
        {
            stockpile.Changed += Refresh;
            construction.Changed += Refresh;
            placement.Changed += Refresh;
            button.onClick.AddListener(placement.Toggle);
            Refresh();
        }
        private void OnDisable()
        {
            stockpile.Changed -= Refresh;
            construction.Changed -= Refresh;
            placement.Changed -= Refresh;
            button.onClick.RemoveListener(placement.Toggle);
        }
        private void Refresh()
        {
            button.interactable = placement.IsPlacing || (construction.CanConstruct && construction.CanAfford);
            buttonLabel.text = placement.IsPlacing ? "CANCEL BUILD" : "BUILD WALL";
            string hint = "Build before invasion";
            if (placement.IsPlacing)
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
    }
}
