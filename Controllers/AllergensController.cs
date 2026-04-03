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
        private static List<Allergen> _allergens = new List<Allergen>();
        private readonly string _recipeFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly Dictionary<string, string> EmergencyAdvices = new()
        {
            { "Milk", "Strictly avoid all dairy products including milk, cheese, butter, and yogurt. Even trace amounts can cause reactions. For mild symptoms, antihistamines may help. If you experience difficulty breathing, swelling, or anaphylaxis, administer epinephrine immediately and seek emergency medical attention." },
            { "Wheat", "Avoid all wheat-containing foods including bread, pasta, cereals, and processed snacks. Always check food labels. In case of ingestion, monitor for hives, swelling, or respiratory symptoms. Use an EpiPen for severe reactions and call emergency services." },
            { "Eggs", "Avoid eggs in all forms, including baked goods and processed foods that may contain albumin. Egg allergies can cause rapid-onset anaphylaxis. Administer epinephrine at the first sign of breathing difficulty or throat tightness and seek medical attention immediately." },
            { "Peanuts", "Peanut allergies are among the most severe and can lead to fatal anaphylaxis. Avoid all forms of peanuts and products that may contain traces. Always carry an EpiPen and administer it without delay if symptoms like wheezing, dizziness, or swelling occur. Call 911 immediately." },
            { "Tree Nuts", "Avoid all tree nuts such as almonds, walnuts, cashews, and pistachios. Reactions may be life-threatening and can escalate rapidly. Carry emergency medication and use epinephrine if any respiratory symptoms, facial swelling, or gastrointestinal distress occurs." },
            { "Shellfish", "Avoid all types of shellfish, including shrimp, crab, and lobster. Shellfish reactions can escalate quickly and may not respond to antihistamines. Administer epinephrine and call emergency services at the first sign of trouble." },
            { "Fish", "Steer clear of all fish products, especially in restaurants where cross-contamination is common. Symptoms may include hives, nausea, or difficulty breathing. Use an EpiPen and seek immediate medical assistance if symptoms intensify." },
            { "Soybeans", "Avoid soy in all forms, including soy sauce, tofu, and processed foods labeled with soy derivatives. Mild symptoms like rashes may occur, but serious reactions require epinephrine and emergency intervention." },
            { "Lupin", "Lupin flour is found in some baked goods and may trigger serious allergic reactions. If exposed, monitor for shortness of breath or swelling. Immediate administration of epinephrine is recommended, followed by professional care." },
            { "Sesame Seeds", "Avoid sesame seeds and sesame oil, which may appear in dressings, sauces, or baked goods. Sesame allergies are rising and can lead to severe reactions. Always carry epinephrine and use it at the first sign of a systemic reaction." },
            { "Barley (Gluten)", "Barley contains gluten and should be strictly avoided by individuals with gluten allergies or celiac disease. Though reactions are less commonly anaphylactic, monitor for abdominal pain or vomiting. Seek medical advice if symptoms worsen." },
            { "Molluscs", "Avoid molluscs such as clams, oysters, mussels, and squid. Reactions can occur within minutes and include respiratory distress or gastrointestinal symptoms. Epinephrine is critical for severe responses. Contact emergency services." },
            { "Celery", "Celery can cause severe allergic reactions, especially when raw. It may also be hidden in spice blends. In case of exposure, watch for hives or breathing issues. Use emergency medication and seek medical support if symptoms persist or escalate." },
            { "Mustard", "Mustard allergies can lead to dangerous reactions, including throat swelling and difficulty breathing. Avoid mustard seeds, sauces, and prepared foods. Always carry an EpiPen and administer it immediately during a reaction, then call for help." }
        };

        public AllergensController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;

            if (!_allergens.Any())
                PreloadDefaultAllergens();
        }

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
        }

        public IActionResult Index() => View(_allergens);

        public IActionResult ViewRecipes(string allergenName)
        {
            if (string.IsNullOrWhiteSpace(allergenName))
                return NotFound();

            var recipes = LoadRecipes();
            var matches = recipes.Where(r => r.Allergens?.Contains(allergenName) == true).ToList();

            ViewBag.AllergenName = allergenName;
            return View(matches);
        }

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

        private List<Recipe> LoadRecipes()
        {
            if (!System.IO.File.Exists(_recipeFilePath))
                return new List<Recipe>();

            var json = System.IO.File.ReadAllText(_recipeFilePath);
            return JsonSerializer.Deserialize<List<Recipe>>(json) ?? new List<Recipe>();
        }

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

            _allergens.Add(newAllergen);
            TempData["NewAllergenMessage"] = $"New allergen '{name}' added successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var allergen = _allergens.FirstOrDefault(x => x.Id == id);
            if (allergen == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (allergen.ApplicationUserId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            _allergens.Remove(allergen);
            return RedirectToAction(nameof(Index));
        }
    }
}
