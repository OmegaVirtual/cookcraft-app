// ✅ 1. AllergenChartViewModel.cs (in Models folder)
namespace Recipes.Models
{
    public class AllergenChartViewModel
    {
        public string Allergen { get; set; }
        public int Count { get; set; }
        public string Commonness { get; set; }  // "🔥 Very Common", "⚠️ Common", "❄️ Rare"
    }
}
