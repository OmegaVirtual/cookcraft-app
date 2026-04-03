using System.ComponentModel.DataAnnotations;

namespace Recipes.Models
{
    public class GroceryItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Item name is required.")]
        public string Name { get; set; }

        public string Category { get; set; }

        public bool IsBought { get; set; }

        [Range(1, 100, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        [MaxLength(250)]
        public string? Note { get; set; }

        [MaxLength(100)]
        public string ListName { get; set; } = "Default";
    }
}
