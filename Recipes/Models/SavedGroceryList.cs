namespace Recipes.Models
{
    public class SavedGroceryList
    {
        public string Name { get; set; }

        public List<GroceryItem> Items { get; set; } = new();
    }
}
