using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipes.Models
{
    public class Allergen
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Allergen name is required.")]
        public string Name { get; set; }

        public string Description { get; set; } // 🆕 Explanation about allergen

        public string ApplicationUserId { get; set; }

        [ForeignKey("ApplicationUserId")]
        public ApplicationUser ApplicationUser { get; set; }

    }
}
