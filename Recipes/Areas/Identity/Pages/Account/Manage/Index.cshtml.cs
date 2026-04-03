using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using System.Globalization;

namespace Recipes.Areas.Identity.Pages.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        [TempData]
        public string StatusMessage { get; set; }

        // Profile Data
        public string Username { get; set; }
        public string DietOption { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string ProfilePictureUrl { get; set; }

        // Stats
        public int RecipesAdded { get; set; }
        public int TotalViews { get; set; }
        public Recipe MostViewedRecipe { get; set; }
        public List<Recipe> UserRecipes { get; set; } = new();

        // Level
        public string LevelName { get; set; }
        public int ProgressPercent { get; set; }

        // Achievements
        public bool HasFirstRecipe { get; set; }
        public bool HasFiveDayStreak { get; set; }
        public bool IsPremiumContributor { get; set; }

        // Leaderboard
        public List<(string Username, int Views)> TopUsers { get; set; } = new();
        public int MyRank { get; set; }

        // Weekly Challenge
        public string WeeklyChallenge { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            Username = user.UserName;
            DietOption = user.DietOption;
            RegistrationDate = user.RegistrationDate;
            ProfilePictureUrl = user.ProfilePictureUrl;

            // Load user's recipes (SQL only)
            UserRecipes = await _db.Recipes
                .Where(r => r.ApplicationUserId == user.Id)
                .OrderByDescending(r => r.DateAdded)
                .ToListAsync();

            RecipesAdded = UserRecipes.Count;
            TotalViews = UserRecipes.Sum(r => r.Views);

            // Achievement checks
            HasFirstRecipe = RecipesAdded >= 1;
            HasFiveDayStreak = RecipesAdded >= 5; // simple unlock condition
            IsPremiumContributor = TotalViews >= 100; // simple unlock condition

            // Most viewed recipe (GLOBAL)
            MostViewedRecipe = await _db.Recipes
                .OrderByDescending(r => r.Views)
                .FirstOrDefaultAsync();

            // Leaderboard (Top 3 users by total recipe views)
            var usersGrouped = await _db.Recipes
                .GroupBy(r => r.ApplicationUserId)
                .Select(g => new { UserId = g.Key, Views = g.Sum(x => x.Views) })
                .OrderByDescending(g => g.Views)
                .Take(3)
                .ToListAsync();

            foreach (var item in usersGrouped)
            {
                var usr = await _userManager.FindByIdAsync(item.UserId);
                if (usr != null)
                {
                    TopUsers.Add((usr.UserName, item.Views));
                }
            }

            // Find current user's rank
            var allUsers = await _db.Recipes
                .GroupBy(r => r.ApplicationUserId)
                .Select(g => new { UserId = g.Key, Views = g.Sum(x => x.Views) })
                .OrderByDescending(g => g.Views)
                .ToListAsync();

            MyRank = allUsers.FindIndex(x => x.UserId == user.Id) + 1;

            // Level
            LevelName = CalculateLevel(RecipesAdded);
            ProgressPercent = GetProgress(RecipesAdded);

            // Weekly challenge
            WeeklyChallenge = GetWeeklyChallenge();

            return Page();
        }

        private string CalculateLevel(int recipes)
        {
            if (recipes >= 50) return "👑 Culinary Legend";
            if (recipes >= 20) return "🔥 Sizzling Sorcerer";
            if (recipes >= 10) return "🧂 Flavor Apprentice";
            if (recipes >= 5) return "🥄 Recipe Rookie";
            return "🍳 Kitchen Noob";
        }

        private int GetProgress(int count)
        {
            return count switch
            {
                < 5 => (int)((count / 5f) * 100),
                < 10 => (int)(((count - 5) / 5f) * 100),
                < 20 => (int)(((count - 10) / 10f) * 100),
                < 50 => (int)(((count - 20) / 30f) * 100),
                _ => 100
            };
        }

        private string GetWeeklyChallenge()
        {
            var challenges = new[]
            {
                "🥗 Upload a recipe with 3+ raw ingredients",
                "🌶️ Make a spicy dish",
                "🍋 Create a citrus-based recipe",
                "🥬 Share a vegan meal",
                "🍝 Make a 15-minute dinner",
                "🍲 Share a soup recipe",
                "🍓 Upload a no-sugar dessert"
            };

            int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                DateTime.UtcNow, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            return challenges[week % challenges.Length];
        }
    }
}
