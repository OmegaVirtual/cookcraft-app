using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using Recipes.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Recipes.Controllers
{
    [Authorize]
    public class RecipesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly string _jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");

        public RecipesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ─────────────────────────────────────────────
        // VULGAR WORD FILTER
        // ─────────────────────────────────────────────

        // 🔥 List of vulgar words (extend anytime)
        private static readonly string[] BannedWords = new[]
        {
    "pussy","dick","penis","vagina","cunt","fuck","shit",
    "balls","bollocks","motherfucker","whore","slut","bitch",
    "ass","arse","dildo","sex","jerk off","fucker"
};

        // Check text for banned words
        private bool ContainsVulgarWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.ToLower();

            foreach (var word in BannedWords)
            {
                if (text.Contains(word))
                    return true;
            }

            return false;
        }


        // ─── INDEX ─────────────────────────────────────────────
        [AllowAnonymous]
        public async Task<IActionResult> Index(string search, string sort, string category, int page = 1)
        {
            ViewData["CurrentFilter"] = search;
            ViewData["SortOrder"] = sort;
            ViewData["SelectedCategory"] = category;

            var dbRecipes = await _db.Recipes.Include(r => r.ApplicationUser).ToListAsync();

            if (!string.IsNullOrWhiteSpace(category) && category != "All Recipes")
            {
                dbRecipes = dbRecipes
                    .Where(r => !string.IsNullOrEmpty(r.Category) &&
                                r.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                dbRecipes = dbRecipes
                    .Where(r =>
                        (r.Title != null && r.Title.ToLower().Contains(lower)) ||
                        (r.Ingredients != null && r.Ingredients.Any(i => i.ToLower().Contains(lower))))
                    .ToList();
            }

            dbRecipes = sort switch
            {
                "latest" => dbRecipes.OrderByDescending(r => r.DateAdded).ToList(),
                _ => dbRecipes.OrderBy(r => r.Title).ToList()
            };

            int pageSize = 6;
            var totalRecipes = dbRecipes.Count;
            var totalPages = (int)Math.Ceiling(totalRecipes / (double)pageSize);
            var recipesOnPage = dbRecipes.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var viewModel = new RecipeListViewModel
            {
                Recipes = recipesOnPage,
                CurrentPage = page,
                TotalPages = totalPages,
                SearchTerm = search,
                SortOrder = sort
            };

            return View(viewModel);
        }

        // ─── ADD ─────────────────────────────────────────────
        public IActionResult Add() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddRecipeViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill all fields correctly.";
                return RedirectToAction(nameof(Add));
            }

            string imagePath;
            var file = Request.Form.Files["ImageFile"];

            if (file != null && file.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                await using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
                imagePath = "/uploads/" + fileName;
            }
            else
            {
                imagePath = GetRandomDefaultImage();
            }

            var ingredientsList = model.IngredientsText?
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .ToList() ?? new List<string>();

            var newRecipe = new Recipe
            {
                Title = model.Title,
                Instructions = model.Instructions,
                ImageUrl = imagePath,
                Ingredients = ingredientsList,
                Allergens = DetectAllergensFromIngredients(ingredientsList),
                ApplicationUserId = user.Id,
                DateAdded = DateTime.UtcNow,
                CreatedAt = DateTime.Now,
                Category = AutoAssignCategory(model.Title, null, ingredientsList)
            };

            // ─── VULGARITY CHECK ───
            var combinedTextAdd =
                $"{newRecipe.Title} {newRecipe.Instructions} {string.Join(" ", newRecipe.Ingredients)}";

            newRecipe.FlaggedForReview = ContainsVulgarWords(combinedTextAdd);

            Console.WriteLine($"🧠 Auto-assigned category for '{newRecipe.Title}': {newRecipe.Category}");

            _db.Recipes.Add(newRecipe);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recipe added successfully!";
            return RedirectToAction(nameof(Index));
        }


        // ─── FIND RECIPES BY INGREDIENTS ─────────────────────────────
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> PickIngredients()
        {
            var recipes = await _db.Recipes.ToListAsync();

            var allIngredients = recipes
                .Where(r => r.Ingredients != null)
                .SelectMany(r => r.Ingredients)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            if (!allIngredients.Any() && System.IO.File.Exists(_jsonFilePath))
            {
                var json = await System.IO.File.ReadAllTextAsync(_jsonFilePath);
                var jsonRecipes = JsonSerializer.Deserialize<List<Recipe>>(json);
                if (jsonRecipes != null)
                    allIngredients = jsonRecipes
                        .SelectMany(r => r.Ingredients ?? new List<string>())
                        .Where(i => !string.IsNullOrWhiteSpace(i))
                        .Distinct()
                        .OrderBy(i => i)
                        .ToList();
            }

            var vm = new IngredientPickerViewModel
            {
                AllIngredients = allIngredients,
                SelectedIngredients = Array.Empty<string>(),
                MatchingRecipes = new List<Recipe>()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PickIngredients(IngredientPickerViewModel model)
        {
            if (model.SelectedIngredients == null || !model.SelectedIngredients.Any())
            {
                ModelState.AddModelError("", "Please select at least one ingredient.");

                var recipesReload = await _db.Recipes.ToListAsync();
                model.AllIngredients = recipesReload
                    .Where(r => r.Ingredients != null)
                    .SelectMany(r => r.Ingredients)
                    .Where(i => !string.IsNullOrWhiteSpace(i))
                    .Distinct()
                    .OrderBy(i => i)
                    .ToList();

                return View(model);
            }

            var allRecipes = await _db.Recipes.ToListAsync();
            var selectedLower = model.SelectedIngredients
                .Select(i => i.Trim().ToLower())
                .ToList();

            // Only recipes that contain *all* selected ingredients
            var matching = allRecipes
                .Where(r =>
                    r.Ingredients != null &&
                    selectedLower.All(sel =>
                        r.Ingredients.Any(ing => ing.ToLower().Contains(sel))
                    )
                )
                .ToList();

            model.AllIngredients = allRecipes
                .Where(r => r.Ingredients != null)
                .SelectMany(r => r.Ingredients)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            model.MatchingRecipes = matching;

            return View(model);
        }

        // ─── VIEW RECIPE ─────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("View/{id}")]
        public async Task<IActionResult> View(int id)
        {
            var recipe = await _db.Recipes
                .Include(r => r.ApplicationUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            // JSON fallback
            if (recipe == null && System.IO.File.Exists(_jsonFilePath))
            {
                var json = await System.IO.File.ReadAllTextAsync(_jsonFilePath);
                var jsonRecipes = JsonSerializer.Deserialize<List<Recipe>>(json);
                if (jsonRecipes != null)
                    recipe = jsonRecipes.FirstOrDefault(r => r.Id == id);
            }

            if (recipe == null)
                return NotFound();

            // Count view
            if (recipe.Id > 0 && recipe.ApplicationUserId != null)
            {
                recipe.Views += 1;
                await _db.SaveChangesAsync();
            }

            ViewBag.NutritionSummary = GetNutrition(recipe.Ingredients);
            return View(recipe);
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT RECIPE (GET)
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return NotFound();

            // Allow if admin OR owner
            if (!User.IsInRole("Admin") && recipe.ApplicationUserId != user.Id)
                return Unauthorized();


            recipe.Ingredients = !string.IsNullOrWhiteSpace(recipe.IngredientsJson)
                ? JsonSerializer.Deserialize<List<string>>(recipe.IngredientsJson)
                : new List<string>();

            return View(recipe);
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT RECIPE (POST)
        // ─────────────────────────────────────────────────────────────
        // ─────────────────────────────────────────────────────────────
        // EDIT RECIPE (POST)
        // ─────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Recipe model)
        {
            var user = await _userManager.GetUserAsync(User);
            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == model.Id);

            if (recipe == null)
                return NotFound();

            if (!User.IsInRole("Admin") && recipe.ApplicationUserId != user.Id)
                return Unauthorized();


            // Update basic fields
            recipe.Title = model.Title;
            recipe.Instructions = model.Instructions;

            // 🔥 FIXED INGREDIENT HANDLING (correct way)
            var ingRaw = Request.Form["IngredientsText"];
            recipe.Ingredients = ingRaw
                .ToString()
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .ToList();

            recipe.IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients);

            // Recalculate allergens & category
            recipe.Allergens = DetectAllergensFromIngredients(recipe.Ingredients);
            recipe.Category = AutoAssignCategory(recipe.Title, null, recipe.Ingredients);


            // 🚨 NEW: VULGARITY CHECK
            var combinedTextEdit =
                $"{recipe.Title} {recipe.Instructions} {string.Join(" ", recipe.Ingredients)}";

            recipe.FlaggedForReview = ContainsVulgarWords(combinedTextEdit);


            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recipe updated successfully!";
            return RedirectToAction("Details", new { id = recipe.Id });
        }


        // ─────────────────────────────────────────────────────────────
        // DELETE RECIPE (GET)
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return NotFound();

            if (!User.IsInRole("Admin") && recipe.ApplicationUserId != user.Id)
                return Unauthorized();



            return View(recipe);
        }

        // ─────────────────────────────────────────────────────────────
        // DELETE RECIPE (POST)
        // ─────────────────────────────────────────────────────────────
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return NotFound();

            if (!User.IsInRole("Admin") && recipe.ApplicationUserId != user.Id)
                return Unauthorized();


            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recipe deleted successfully!";
            return RedirectToAction("Index");
        }

        // ─────────────────────────────────────────────
        // UTILITIES (UNCHANGED)
        // ─────────────────────────────────────────────

        private List<string> DetectAllergensFromIngredients(List<string> ingredients)
        {
            if (ingredients == null || ingredients.Count == 0)
                return new List<string>();

            var ingredientText = string.Join(" ", ingredients).ToLower();

            var allergenGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
    {
        { "Milk", new List<string> { "milk","cheese","butter","cream","yogurt","mozzarella","cheddar","parmesan","whey","casein","kefir" }},
        { "Eggs", new List<string> { "egg","eggs","mayonnaise","mayo","aioli" }},
        { "Fish", new List<string> { "fish","salmon","cod","sea bass","tuna","mackerel","anchovy","haddock","trout","sardine","pollock","hake" }},
        { "Shellfish (Crustaceans)", new List<string> { "shrimp","prawns","crab","lobster","langoustine","crayfish","krill" }},
        { "Molluscs", new List<string> { "mussels","mussel","oyster","clams","clam","scallop","octopus","squid","calamari" }},
        { "Gluten", new List<string> { "wheat","flour","pasta","bread","breadcrumbs","couscous","noodles","semolina","bulgur","barley","rye","spelt" }},
        { "Peanuts", new List<string> { "peanut","groundnut","satay" }},
        { "Tree Nuts", new List<string> { "walnut","almond","cashew","pecan","hazelnut","pistachio","macadamia","nut","pesto" }},
        { "Soy", new List<string> { "soy","soya","tofu","edamame","soybean","miso","tempeh" }},
        { "Sesame", new List<string> { "sesame","tahini" }},
        { "Mustard", new List<string> { "mustard","dijon","wholegrain mustard" }},
        { "Celery", new List<string> { "celery","celeriac" }},
        { "Lupin", new List<string> { "lupin","lupine" }},
        { "Sulphites", new List<string> { "sulphite","sulfite","preservative e220","e220","e221","e222","e223" }},
    };

            var detected = new HashSet<string>();

            foreach (var allergen in allergenGroups)
            {
                foreach (var keyword in allergen.Value)
                {
                    if (ingredientText.Contains(keyword.ToLower()))
                    {
                        detected.Add(allergen.Key);
                        break;
                    }
                }
            }

            return detected.ToList();
        }

        private string AutoAssignCategory(string title, string description, List<string> ingredients)
        {
            var text = (title + " " + description + " " + string.Join(" ", ingredients ?? new List<string>())).ToLower();

            string[] healthy = {
                "salad","grilled","avocado","spinach","broccoli","quinoa","oat","oats","fruit","vegetable",
                "low fat","vegan","vegetarian","yogurt","chickpea","tofu","lentil","zucchini","bean",
                "wholegrain","smoothie","baked","fresh","greens","protein","light","roasted","healthy",
                "steamed","low calorie","cucumber","tomato","leafy","clean","detox","fit","nuts",
                "seeds","air fried","raw","low sugar","fiber","herb","olive oil","lemon","salmon",
                "chicken breast","granola","pancake","pancakes","porridge","muffin","muffins","toast","overnight oats"
            };

            string[] quick = {
                "quick","easy","minute","no fuss","simple","traybake","stir fry","one pot","fast","rapid","speedy",
                "express","microwave","minimal","lazy","bowl","wrap","toast","sandwich","pasta","omelette","scramble",
                "instant","no cook","pan","snack","lunchbox","weeknight","sheet pan","ready","fast dinner","easy meal",
                "rice","ramen","noodle","taco","wrap","stirfry"
            };

            string[] comfort = {
                "lasagna","pie","stew","casserole","roast","mash","pudding","soup","burger","sausage","meatball",
                "bake","macaroni","gravy","chili","cheese","butter","cream","bacon","potato","rich","slow cooked",
                "chocolate","bbq","fried","crispy","creamy","stewed","pork","meatloaf","shepherd","ragu","curry",
                "dumpling","tart","bread","toastie","pasta sauce","cheesy","pasta bake"
            };

            bool ContainsAny(string[] words) => words.Any(k => text.Contains(k));

            if (ContainsAny(healthy)) return "Healthy Bites";
            if (ContainsAny(quick)) return "Quick & Easy";
            if (ContainsAny(comfort)) return "Comfort Cravings";

            return "Comfort Cravings";
        }

        private string GetRandomDefaultImage()
        {
            var defaultsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "defaults");
            if (!Directory.Exists(defaultsFolder))
                return "/uploads/default-recipe.jpg";

            var defaultImages = Directory.GetFiles(defaultsFolder, "default*.jpg");
            if (defaultImages.Length == 0)
                return "/uploads/default-recipe.jpg";

            var random = new Random(Guid.NewGuid().GetHashCode());
            var randomImage = defaultImages[random.Next(defaultImages.Length)];
            return "/uploads/defaults/" + Path.GetFileName(randomImage);
        }

        private object GetNutrition(List<string> ingredients)
        {
            return new { Calories = 0, Protein = 0, Carbs = 0, Fat = 0 };
        }

        // ─── Details ─────────────────────────────────────────────
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
                return NotFound();

            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null)
                return NotFound();

            if (!string.IsNullOrWhiteSpace(recipe.IngredientsJson))
            {
                try
                {
                    recipe.Ingredients = System.Text.Json.JsonSerializer
                        .Deserialize<List<string>>(recipe.IngredientsJson) ?? new List<string>();
                }
                catch
                {
                    recipe.Ingredients = new List<string>();
                }
            }

            return View(recipe);
        }
    }
}
