using System;

namespace Recipes.Models.ViewModels
{
    public class UserAdminViewModel
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string Username { get; set; } = "";
        public string DietOption { get; set; } = "";
        public DateTime DateJoined { get; set; }
        public int RecipeCount { get; set; }
        public bool IsBanned { get; set; }
    }
}
