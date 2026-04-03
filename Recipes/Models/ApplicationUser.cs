using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Recipes.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Display(Name = "Diet Preference")]
        public string? DietOption { get; set; } = "None"; // ✅ Now nullable + default value

        [Display(Name = "Registered On")]
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Profile Picture URL")]
        public string? ProfilePictureUrl { get; set; } = "/uploads/default-profile.jpg"; // ✅ Safe default

        [Display(Name = "Favorite Recipes")]
        public List<int> FavoriteRecipeIds { get; set; } = new();

        [Display(Name = "Grocery Items")]
        public List<string> GroceryItems { get; set; } = new();
    }
}
