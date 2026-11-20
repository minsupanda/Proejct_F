using ProjectF.Construction;
using UnityEngine;

namespace ProjectF.Economy
{
    public sealed class FarmHUD : MonoBehaviour
    {
        [SerializeField] private EstateStockpile stockpile;
        [SerializeField] private WallConstruction construction;
        [SerializeField] private WallPlacementController placement;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private UnityEngine.UI.Text buttonLabel;
        [SerializeField] private UnityEngine.UI.Button button;
        private bool lastCanConstruct;
        private void OnEnable()
        {
            stockpile.Changed += Refresh;
            construction.Changed += Refresh;
            placement.Changed += Refresh;
            button.onClick.AddListener(placement.ToggleFarm);
            Refresh();
        }
        private void OnDisable()
        {
            stockpile.Changed -= Refresh;
            construction.Changed -= Refresh;
            placement.Changed -= Refresh;
            button.onClick.RemoveListener(placement.ToggleFarm);
        }
        private void Update() { if (lastCanConstruct != construction.CanConstruct) Refresh(); }
        private void Refresh()
        {
            lastCanConstruct = construction.CanConstruct;
            var settings = construction.FarmSettings;
            button.interactable = placement.IsPlacingFarm || (lastCanConstruct && construction.CanAffordFarm);
            buttonLabel.text = placement.IsPlacingFarm ? "CANCEL FARM" : "BUILD FARM";
            string hint = !lastCanConstruct ? "Harvest during preparation"
                : !stockpile.CanAddFood(settings.FoodPerHarvest) ? "Storage full / harvest held"
                : "Build farms to produce food";
            if (stockpile.ReservedFoodCapacity > 0)
                hint = "Refund space held: " + stockpile.ReservedFoodCapacity;
            status.text = "FOOD " + stockpile.Food + " / " + stockpile.FoodCapacity
                + "\nFARM " + settings.WoodCost + " WOOD / +" + settings.FoodPerHarvest + " FOOD / " + settings.HarvestSeconds.ToString("0.#") + "s"
                + "\n" + hint;
        }
    }
}
