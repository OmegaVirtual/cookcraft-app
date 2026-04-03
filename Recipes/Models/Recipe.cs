using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Recipes.Models
{
    [Table("Recipes")]
    public class Recipe
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string ShortDescription { get; set; } = string.Empty;

        // ✅ Add public Description property (alias for ShortDescription)
        [NotMapped]
        public string Description
        {
            get => ShortDescription;
            set => ShortDescription = value;
        }

        public string Instructions { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        // 🧩 Optional category (used for sorting/filtering)
        public string? Category { get; set; }

        // 🔹 Backing field stored in DB
        public string IngredientsJson { get; set; } = "[]";

        // ⚠️ Not mapped to DB — used only at runtime
        [NotMapped]
        public List<string> Ingredients
        {
            get
            {
                if (string.IsNullOrWhiteSpace(IngredientsJson))
                    return new List<string>();

                try
                {
                    return JsonSerializer.Deserialize<List<string>>(IngredientsJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set
            {
                IngredientsJson = JsonSerializer.Serialize(value ?? new List<string>());
            }
        }

        // ⚠️ Not mapped to DB — runtime-only allergen list
        [NotMapped]
        public List<string> Allergens { get; set; } = new List<string>();

        // 👤 Recipe owner
        public string? ApplicationUserId { get; set; }

        [ForeignKey(nameof(ApplicationUserId))]
        public ApplicationUser? ApplicationUser { get; set; }

        // 📅 Timestamps
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ⭐ NEW: Popularity — view count
        public int Views { get; set; } = 0;

        // ⭐ NEW: Popularity — likes
        public int Likes { get; set; } = 0;

        // ⭐ NEW: Flagged for vulgar content
        public bool FlaggedForReview { get; set; } = false;

        public Recipe()
        {
            // Ensure JSON is always valid
            IngredientsJson ??= "[]";


        }
    }
}
