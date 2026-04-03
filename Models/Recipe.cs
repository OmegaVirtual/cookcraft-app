using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recipes.Models
{
    [Table("Recipes")]
    public class Recipe
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ShortDescription { get; set; }
        public string Instructions { get; set; }
        public string ImageUrl { get; set; }

        // Optional: Marked as NotMapped if you use EF Core later.
        [NotMapped]
        public List<string> Ingredients { get; set; } = new List<string>();

        [NotMapped]
        public List<string> Allergens { get; set; } = new List<string>();

        // ✅ Foreign key to ApplicationUser (User)
        public string ApplicationUserId { get; set; }

        [ForeignKey("ApplicationUserId")]
        public ApplicationUser ApplicationUser { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
