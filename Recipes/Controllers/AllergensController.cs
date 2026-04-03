using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Recipes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;

namespace Recipes.Controllers
{
    public class AllergensController : Controller
    {
        private static List<Allergen> _allergens = new();
        private readonly string _recipeFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");
        private readonly UserManager<ApplicationUser> _userManager;

        // ✅ Built-in dictionary for emergency advice
        private static readonly Dictionary<string, string> EmergencyAdvices = new()
        {
            { "Milk", "Strictly avoid all dairy products including milk, cheese, butter, and yogurt. Even trace amounts can cause reactions. Use antihistamines for mild symptoms and epinephrine for severe ones." },
            { "Wheat", "Avoid all wheat-containing foods including bread, pasta, and cereals. Monitor for hives or swelling. Use EpiPen if severe." },
            { "Eggs", "Avoid eggs and foods with albumin. Administer epinephrine if breathing difficulty or throat tightness occurs." },
            { "Peanuts", "Avoid all peanut products. Always carry an EpiPen and use it immediately during a reaction." },
            { "Tree Nuts", "Avoid almonds, walnuts, cashews, and pistachios. Carry emergency medication for potential anaphylaxis." },
            { "Shellfish", "Avoid shrimp, crab, lobster. Reactions can be severe and need immediate medical care." },
            { "Fish", "Avoid all fish products. Symptoms may include hives or difficulty breathing. Use EpiPen and seek help." },
            { "Soybeans", "Avoid soy and soy-derived products. Mild rash can occur but serious reactions need emergency care." },
            { "Lupin", "Avoid lupin flour and seeds. Can cause serious allergic reactions; use epinephrine immediately if exposed." },
            { "Sesame Seeds", "Avoid sesame and sesame oil in sauces and bread. Reactions can escalate quickly; carry EpiPen." },
            { "Barley (Gluten)", "Avoid gluten products including barley and rye. Watch for abdominal pain or vomiting." },
            { "Molluscs", "Avoid clams, oysters, mussels, and squid. Reactions may include respiratory distress; epinephrine needed." },
            { "Celery", "Avoid raw celery and spice blends. Can cause severe reactions. Seek help if swelling or hives appear." },
            { "Mustard", "Avoid mustard and its seeds in sauces and dressings. Carry EpiPen if prone to severe responses." }
        };

        public AllergensController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;

            if (!_allergens.Any())
                PreloadDefaultAllergens();
        }

        // ✅ Load default allergens (used only once)
        private void PreloadDefaultAllergens()
        {
            _allergens = new List<Allergen>
            {
                new() { Id = 1, Name = "Milk", Description = "Milk and dairy products." },
                new() { Id = 2, Name = "Wheat", Description = "Wheat found in bread, pasta, etc." },
                new() { Id = 3, Name = "Eggs", Description = "Eggs used in cakes, sauces, etc." },
                new() { Id = 4, Name = "Peanuts", Description = "Peanut products and peanut oil." },
                new() { Id = 5, Name = "Tree Nuts", Description = "Almonds, walnuts, cashews, etc." },
                new() { Id = 6, Name = "Shellfish", Description = "Shrimp, crab, lobster." },
                new() { Id = 7, Name = "Fish", Description = "Salmon, tuna, cod." },
                new() { Id = 8, Name = "Soybeans", Description = "Soy products like tofu, soy milk." },
                new() { Id = 9, Name = "Lupin", Description = "Lupin flour and seeds." },
                new() { Id = 10, Name = "Sesame Seeds", Description = "Sesame used in breads, hummus." },
                new() { Id = 11, Name = "Barley (Gluten)", Description = "Cereal grains like barley and rye." },
                new() { Id = 12, Name = "Molluscs", Description = "Snails, squid, clams." },
                new() { Id = 13, Name = "Celery", Description = "Common in soups, salads, stocks." },
                new() { Id = 14, Name = "Mustard", Description = "Used in dressings and sauces." }
            };

            // Add emergency advice from dictionary
            foreach (var allergen in _allergens)
            {
                if (EmergencyAdvices.TryGetValue(allergen.Name, out var advice))
                    allergen.EmergencyAdvice = advice;
            }
        }

        // ─── Main Index ─────────────────────────────────────────────
        public IActionResult Index() => View(_allergens);

        // ─── View Recipes linked to allergen ────────────────────────
        public IActionResult ViewRecipes(string allergenName)
        {
            if (string.IsNullOrWhiteSpace(allergenName))
                return NotFound();

            var recipes = LoadRecipes();

            // Match allergens case-insensitively
            var matches = recipes
                .Where(r => r.Allergens != null &&
                            r.Allergens.Any(a => a.Equals(allergenName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ViewBag.AllergenName = allergenName;
            ViewBag.Total = matches.Count;
            return View(matches);
        }

        // ─── Emergency Info Page ─────────────────────────────────────
        public IActionResult EmergencyAction(string allergenName)
        {
            if (string.IsNullOrWhiteSpace(allergenName))
                return NotFound();

            ViewBag.AllergenName = allergenName;
            ViewBag.Advice = EmergencyAdvices.TryGetValue(allergenName, out var advice)
                ? advice
                : "No emergency advice available for this allergen.";

            return View();
        }

        // ─── Allergen Statistics ─────────────────────────────────────
        public IActionResult Statistics()
        {
            var recipes = LoadRecipes();

            var allergenCounts = recipes
                .SelectMany(r => r.Allergens ?? new List<string>())
                .GroupBy(a => a)
                .Select(g => new { Allergen = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            return View(allergenCounts);
        }

        // ─── Trends Chart ─────────────────────────────────────────────
        public IActionResult Trends()
        {
            var recipes = LoadRecipes();

            var result = recipes
                .Where(r => r.Allergens != null)
                .SelectMany(r => r.Allergens)
                .GroupBy(a => a)
                .Select(g => new AllergenChartViewModel
                {
                    Allergen = g.Key,
                    Count = g.Count(),
                    Commonness = g.Count() >= 10 ? "🔥 Very Common" :
                                 g.Count() >= 5 ? "⚠️ Common" :
                                 "🌿 Rare"
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return View("AllergenChart", result);
        }

        // ─── Load Recipes from JSON File ─────────────────────────────
        private List<Recipe> LoadRecipes()
        {
            try
            {
                if (!System.IO.File.Exists(_recipeFilePath))
                    return new List<Recipe>();

                var json = System.IO.File.ReadAllText(_recipeFilePath);
                var recipes = JsonSerializer.Deserialize<List<Recipe>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return recipes ?? new List<Recipe>();
            }
            catch
            {
                return new List<Recipe>();
            }
        }

        // ─── Add new allergen manually ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Allergen name cannot be empty!";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var newAllergen = new Allergen
            {
                Id = _allergens.Count > 0 ? _allergens.Max(x => x.Id) + 1 : 1,
                Name = name,
                Description = description,
                ApplicationUserId = userId
            };

            if (EmergencyAdvices.TryGetValue(name, out var advice))
                newAllergen.EmergencyAdvice = advice;

            _allergens.Add(newAllergen);

            TempData["NewAllergenMessage"] = $"New allergen '{name}' added successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ─── Delete allergen ─────────────────────────────────────────
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var allergen = _allergens.FirstOrDefault(x => x.Id == id);
            if (allergen == null)
                return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (allergen.ApplicationUserId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            _allergens.Remove(allergen);
            return RedirectToAction(nameof(Index));
        }
    }
}
