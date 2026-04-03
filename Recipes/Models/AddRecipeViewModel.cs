using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Recipes.Models
{
    public class AddRecipeViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        public string Title { get; set; } = string.Empty;

        // ❌ ShortDescription removed completely

        [Required(ErrorMessage = "Instructions are required.")]
        public string Instructions { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please add at least one ingredient.")]
        [Display(Name = "Ingredients (one per line)")]
        public string IngredientsText { get; set; } = string.Empty;

        // For image uploads
        [Display(Name = "Upload Image")]
        public IFormFile? ImageFile { get; set; }

        // Used for showing existing image in edit mode
        public string? ExistingImageUrl { get; set; }

        // Optional field if you ever want to keep the URL manually
        public string? ImageUrl { get; set; }

        // Optional category
        [Display(Name = "Category")]
        public string? Category { get; set; }
    }
}
