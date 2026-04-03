using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Recipes.Models;
using System.Collections.Generic;
using System.Text.Json;
using System.Globalization;

namespace Recipes.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            ILogger<IndexModel> logger)
        {
            _userManager = userManager;
            _environment = environment;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public string Username { get; set; }
        public string DietOption { get; set; }
        public DateTime RegistrationDate { get; set; }
        public int RecipeCount { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string Level { get; set; }
        public string Badge { get; set; }
        public int ProgressPercent { get; set; }
        public List<(string Username, int Count)> TopUsers { get; set; } = new();
        public int MyRank { get; set; }
        public string WeeklyChallenge { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            Username = user.UserName;
            DietOption = user.DietOption;
            RegistrationDate = user.RegistrationDate;
            ProfilePictureUrl = user.ProfilePictureUrl;

            var allRecipes = LoadAllRecipes();
            RecipeCount = allRecipes.Count(r => r.ApplicationUserId == user.Id);
            Level = CalculateLevel(RecipeCount);
            Badge = GetBadge(Level);
            ProgressPercent = GetProgress(RecipeCount);

            TopUsers = await GetLeaderboard(allRecipes);
            MyRank = GetUserRank(user.Id, TopUsers);

            WeeklyChallenge = GetWeeklyChallenge();

            return Page();
        }

        private string GetWeeklyChallenge()
        {
            var challenges = new List<string>
            {
                "🥬 Dare: Create a fully green vegetable stir-fry!",
                "🍲 Dare: Make a soup with no salt added!",
                "🥗 Dare: Upload a recipe with 3+ raw ingredients.",
                "🌾 Dare: Post a whole grain–only breakfast dish.",
                "🍠 Dare: Share a healthy sweet potato-based recipe.",
                "🌰 Dare: Make a protein-packed vegetarian dish!",
                "🍋 Dare: Add a citrus fruit to your next creation.",
                "🥒 Dare: Include cucumber, kale or spinach today.",
                "🍓 Dare: Make a no-sugar snack using only fruits.",
                "🌶️ Dare: Cook a meal with anti-inflammatory spices!",
                "🫛 Dare: Add at least 2 legumes in one recipe.",
                "🥥 Dare: Make a tropical bowl with only fresh food.",
                "🫐 Dare: Create a breakfast high in antioxidants.",
                "🥑 Dare: Use only healthy fats like avocado or olive oil.",
                "🥕 Dare: Upload a vitamin-A rich meal (e.g. carrots)."
            };

            int week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                DateTime.UtcNow, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            string selected = challenges[week % challenges.Count];
            return $"🍏 This week's challenge: {selected}";
        }

        private List<Recipe> LoadAllRecipes()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");
            if (!System.IO.File.Exists(filePath)) return new List<Recipe>();

            var json = System.IO.File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<Recipe>>(json) ?? new List<Recipe>();
        }

        private async Task<List<(string Username, int Count)>> GetLeaderboard(List<Recipe> allRecipes)
        {
            var grouped = allRecipes
                .GroupBy(r => r.ApplicationUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(3)
                .ToList();

            var result = new List<(string Username, int Count)>();
            foreach (var entry in grouped)
            {
                var user = await _userManager.FindByIdAsync(entry.UserId);
                if (user != null)
                {
                    result.Add((user.UserName, entry.Count));
                }
            }
            return result;
        }

        private int GetUserRank(string myId, List<(string Username, int Count)> topUsers)
        {
            var allRecipes = LoadAllRecipes();
            var grouped = allRecipes
                .GroupBy(r => r.ApplicationUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            for (int i = 0; i < grouped.Count; i++)
            {
                if (grouped[i].UserId == myId)
                    return i + 1;
            }

            return 0;
        }

        private string CalculateLevel(int count)
        {
            if (count >= 50) return "Culinary Overlord";
            if (count >= 20) return "Sizzling Sorcerer";
            if (count >= 10) return "Flavor Apprentice";
            if (count >= 5) return "Recipe Rookie";
            return "Kitchen Noob";
        }

        private string GetBadge(string level)
        {
            return level switch
            {
                "Kitchen Noob" => "🍳 Kitchen Noob",
                "Recipe Rookie" => "🥄 Recipe Rookie",
                "Flavor Apprentice" => "🧂 Flavor Apprentice",
                "Sizzling Sorcerer" => "🔥 Sizzling Sorcerer",
                "Culinary Overlord" => "👨‍🍳 Culinary Overlord",
                _ => "🏅 Mystery Chef"
            };
        }

        private int GetProgress(int count)
        {
            return count switch
            {
                < 5 => (int)((count / 5.0) * 100),
                < 10 => (int)(((count - 5) / 5.0) * 100),
                < 20 => (int)(((count - 10) / 10.0) * 100),
                < 50 => (int)(((count - 20) / 30.0) * 100),
                _ => 100
            };
        }
    }
}
