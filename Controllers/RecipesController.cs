using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Recipes.Models;
using Recipes.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Claims;

namespace Recipes.Controllers
{
    public class RecipesController : Controller
    {
        private static List<Recipe> _recipes = new List<Recipe>();
        private readonly string _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");
        private readonly UserManager<ApplicationUser> _userManager;

        public RecipesController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _recipes = LoadRecipes();
        }

        private List<Recipe> LoadRecipes()
        {
            if (!System.IO.File.Exists(_filePath))
                System.IO.File.WriteAllText(_filePath, "[]");

            var json = System.IO.File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Recipe>>(json) ?? new List<Recipe>();
        }

        private void SaveRecipes()
        {
            var json = JsonSerializer.Serialize(_recipes, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_filePath, json);
        }

        public IActionResult Index(string search, string sort, int page = 1)
        {
            int pageSize = 6;
            var filtered = _recipes;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();
                filtered = filtered.Where(r =>
                    r.Title.ToLower().Contains(lower) ||
                    r.ShortDescription?.ToLower().Contains(lower) == true ||
                    r.Ingredients?.Any(i => i.ToLower().Contains(lower)) == true
                ).ToList();
            }

            if (sort == "az")
            {
                filtered = filtered.OrderBy(r => r.Title).ToList();
            }

            var totalRecipes = filtered.Count;
            var totalPages = (int)Math.Ceiling(totalRecipes / (double)pageSize);
            var recipesOnPage = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

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

        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(AddRecipeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fill all fields correctly.";
                return RedirectToAction(nameof(Add));
            }

            string imagePath = "/uploads/default-recipe.jpg";
            var file = Request.Form.Files["ImageFile"];
            if (file != null && file.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                file.CopyTo(stream);

                imagePath = "/uploads/" + fileName;
            }

            var ingredientsList = model.IngredientsText?
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToList() ?? new List<string>();

            var userId = _userManager.GetUserId(User);

            var newRecipe = new Recipe
            {
                Id = _recipes.Count > 0 ? _recipes.Max(r => r.Id) + 1 : 1,
                Title = model.Title,
                ShortDescription = model.ShortDescription,
                Instructions = model.Instructions,
                ImageUrl = imagePath,
                Ingredients = ingredientsList,
                Allergens = DetectAllergensFromIngredients(ingredientsList),
                ApplicationUserId = userId
            };

            _recipes.Add(newRecipe);
            SaveRecipes();

            TempData["SuccessMessage"] = "Recipe added successfully!";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult View(int id)
        {
            var recipe = _recipes.FirstOrDefault(r => r.Id == id);
            if (recipe == null) return NotFound();

            ViewBag.NutritionSummary = GetNutrition(recipe.Ingredients);
            return View(recipe);
        }

        [AllowAnonymous]
        public IActionResult ReadOnlyView(int id)
        {
            var recipe = _recipes.FirstOrDefault(r => r.Id == id);
            if (recipe == null) return NotFound();

            ViewBag.NutritionSummary = GetNutrition(recipe.Ingredients);
            return View("ReadOnlyView", recipe);
        }

        public IActionResult Edit(int id)
        {
            var recipe = _recipes.FirstOrDefault(r => r.Id == id);
            if (recipe == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (recipe.ApplicationUserId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            var viewModel = new AddRecipeViewModel
            {
                Title = recipe.Title,
                ShortDescription = recipe.ShortDescription,
                Instructions = recipe.Instructions,
                IngredientsText = string.Join("\n", recipe.Ingredients)
            };

            ViewBag.RecipeId = id;
            ViewBag.RecipeOwnerId = recipe.ApplicationUserId; // ✅ Added

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, AddRecipeViewModel model)
        {
            var recipe = _recipes.FirstOrDefault(r => r.Id == id);
            if (recipe == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (recipe.ApplicationUserId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.RecipeId = id;
                return View(model);
            }

            recipe.Title = model.Title;
            recipe.ShortDescription = model.ShortDescription;
            recipe.Instructions = model.Instructions;
            recipe.Ingredients = model.IngredientsText?
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToList() ?? new List<string>();
            recipe.Allergens = DetectAllergensFromIngredients(recipe.Ingredients);

            var file = Request.Form.Files["ImageFile"];
            if (file != null && file.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                file.CopyTo(stream);

                recipe.ImageUrl = "/uploads/" + fileName;
            }

            SaveRecipes();
            TempData["SuccessMessage"] = "Recipe updated successfully!";
            return RedirectToAction("View", new { id = recipe.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var recipe = _recipes.FirstOrDefault(r => r.Id == id);
            if (recipe == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (recipe.ApplicationUserId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            _recipes.Remove(recipe);
            SaveRecipes();

            TempData["SuccessMessage"] = "Recipe deleted.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Favorite(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (user.FavoriteRecipeIds == null)
                user.FavoriteRecipeIds = new List<int>();

            if (user.FavoriteRecipeIds.Contains(id))
                user.FavoriteRecipeIds.Remove(id);
            else
                user.FavoriteRecipeIds.Add(id);

            await _userManager.UpdateAsync(user);
            return Json(new { success = true, favorites = user.FavoriteRecipeIds });
        }

        private List<string> DetectAllergensFromIngredients(List<string> ingredients)
        {
            var allergenMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "milk", "Milk" }, { "cheese", "Milk" }, { "butter", "Milk" }, { "cream", "Milk" },
                { "yoghurt", "Milk" }, { "yogurt", "Milk" }, { "custard", "Milk" }, { "whey", "Milk" },
                { "casein", "Milk" }, { "lactose", "Milk" }, { "dairy milk", "Milk" }, { "cheddar", "Milk" }, { "emmental", "Milk" },
                { "egg", "Eggs" }, { "mayonnaise", "Eggs" }, { "meringue", "Eggs" }, { "quiche", "Eggs" },
                { "peanut", "Peanuts" }, { "satay", "Peanuts" }, { "peanut butter", "Peanuts" }, { "reese", "Peanuts" },
                { "walnut", "Tree Nuts" }, { "almond", "Tree Nuts" }, { "cashew", "Tree Nuts" },
                { "pecan", "Tree Nuts" }, { "hazelnut", "Tree Nuts" }, { "pistachio", "Tree Nuts" },
                { "nutella", "Tree Nuts" }, { "marzipan", "Tree Nuts" },
                { "shrimp", "Shellfish" }, { "prawn", "Shellfish" }, { "crab", "Shellfish" }, { "lobster", "Shellfish" },
                { "scampi", "Shellfish" }, { "mollusc", "Molluscs" }, { "clam", "Molluscs" }, { "snail", "Molluscs" },
                { "squid", "Molluscs" }, { "octopus", "Molluscs" },
                { "fish", "Fish" }, { "salmon", "Fish" }, { "tuna", "Fish" }, { "haddock", "Fish" }, { "cod", "Fish" },
                { "anchovy", "Fish" }, { "mackerel", "Fish" }, { "fish sauce", "Fish" }, { "fish fingers", "Fish" },
                { "wheat", "Wheat" }, { "barley", "Barley (Gluten)" }, { "rye", "Barley (Gluten)" },
                { "oats", "Barley (Gluten)" }, { "malt", "Barley (Gluten)" }, { "bread", "Wheat" },
                { "flour", "Wheat" }, { "pasta", "Wheat" }, { "couscous", "Wheat" },
                { "soy", "Soybeans" }, { "soybean", "Soybeans" }, { "tofu", "Soybeans" }, { "edamame", "Soybeans" },
                { "soya", "Soybeans" }, { "miso", "Soybeans" },
                { "mustard", "Mustard" }, { "mustard seed", "Mustard" }, { "celery", "Celery" }, { "celeriac", "Celery" },
                { "sesame", "Sesame Seeds" }, { "tahini", "Sesame Seeds" }, { "burger bun", "Sesame Seeds" },
                { "lupin", "Lupin" },
                { "sulphite", "Sulphites" }, { "sulfite", "Sulphites" }, { "wine vinegar", "Sulphites" },
                { "dried fruit", "Sulphites" }, { "preservative e220", "Sulphites" },
                { "mcvitie", "Wheat" }, { "warburtons", "Wheat" }, { "mr kipling", "Eggs" },
                { "cadbury", "Milk" }, { "galaxy", "Milk" }, { "kitkat", "Milk" }, { "maltesers", "Milk" },
                { "jelly baby", "Gelatin" }, { "haribo", "Gelatin" }, { "rich tea", "Wheat" }, { "digestive", "Wheat" },
                { "hobnob", "Oats" }, { "twiglets", "Barley (Gluten)" }
            };

            var detected = new HashSet<string>();
            foreach (var ingredient in ingredients)
            {
                foreach (var pair in allergenMap)
                {
                    if (ingredient.ToLower().Contains(pair.Key.ToLower()))
                        detected.Add(pair.Value);
                }
            }

            return detected.ToList();
        }

        private object GetNutrition(List<string> ingredients)
        {
            var nutrition = new { Calories = 0, Protein = 0, Carbs = 0, Fat = 0 };

            if (ingredients != null)
            {
                foreach (var ingredient in ingredients)
                {
                    foreach (var key in NutritionDatabase.Keys)
                    {
                        if (ingredient.ToLower().Contains(key))
                        {
                            nutrition = new
                            {
                                Calories = nutrition.Calories + NutritionDatabase[key].Calories,
                                Protein = nutrition.Protein + NutritionDatabase[key].Protein,
                                Carbs = nutrition.Carbs + NutritionDatabase[key].Carbs,
                                Fat = nutrition.Fat + NutritionDatabase[key].Fat
                            };
                        }
                    }
                }
            }

            return nutrition;
        }

        private Dictionary<string, (int Calories, int Protein, int Carbs, int Fat)> NutritionDatabase = new()
        {
            { "milk", (42, 3, 5, 1) },
            { "chicken", (165, 31, 0, 4) },
            { "rice", (130, 2, 28, 0) },
            { "egg", (155, 13, 1, 11) },
            { "cheese", (402, 25, 1, 33) },
            { "bread", (265, 9, 49, 3) },
            { "beef", (250, 26, 0, 15) },
            { "fish", (206, 22, 0, 12) },
            { "potato", (77, 2, 17, 0) },
            { "butter", (717, 1, 0, 81) },
            { "tomato", (18, 1, 4, 0) },
            { "onion", (40, 1, 9, 0) },
            { "garlic", (149, 6, 33, 0) }
        };
    }
}
