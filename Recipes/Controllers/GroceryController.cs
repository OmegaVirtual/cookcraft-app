using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using Recipes.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace Recipes.Controllers
{
    public class GroceryController : Controller
    {
        private static List<GroceryItem> _groceryItems = new();
        private static Dictionary<string, List<GroceryItem>> _savedLists = new();

        private readonly ApplicationDbContext _db;
        private readonly string _jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");

        public GroceryController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult List(string searchTerm, string categoryFilter)
        {
            var items = _groceryItems;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                items = items.Where(x =>
                    x.Name.ToLower().Contains(searchTerm.ToLower()) ||
                    x.Category.ToLower().Contains(searchTerm.ToLower()))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter) && categoryFilter != "All")
            {
                items = items.Where(x =>
                    x.Category.Equals(categoryFilter, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            ViewBag.AllListNames = _savedLists.Keys.ToList();
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(string name, string category, int quantity, string note)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Item name cannot be empty!";
                return RedirectToAction(nameof(List));
            }

            var newItem = new GroceryItem
            {
                Id = _groceryItems.Count > 0 ? _groceryItems.Max(x => x.Id) + 1 : 1,
                Name = name,
                Category = string.IsNullOrWhiteSpace(category) ? "Other" : category,
                Quantity = quantity > 0 ? quantity : 1,
                Note = note,
                IsBought = false
            };

            _groceryItems.Add(newItem);
            TempData["SuccessMessage"] = "Item added successfully!";
            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        public IActionResult ToggleBought(int id)
        {
            var item = _groceryItems.FirstOrDefault(x => x.Id == id);
            if (item != null) item.IsBought = !item.IsBought;
            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var item = _groceryItems.FirstOrDefault(x => x.Id == id);
            if (item != null) _groceryItems.Remove(item);
            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        public IActionResult SaveList(string listName)
        {
            if (!string.IsNullOrWhiteSpace(listName))
            {
                var copied = _groceryItems.Select(i => new GroceryItem
                {
                    Id = i.Id,
                    Name = i.Name,
                    Category = i.Category,
                    Quantity = i.Quantity,
                    Note = i.Note,
                    IsBought = false
                }).ToList();

                _savedLists[listName] = copied;
                TempData["SuccessMessage"] = $"List '{listName}' saved!";
            }
            else TempData["ErrorMessage"] = "Please enter a valid list name to save.";

            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        public IActionResult LoadList(string listName)
        {
            if (!string.IsNullOrWhiteSpace(listName) && _savedLists.ContainsKey(listName))
            {
                var loaded = _savedLists[listName]
                    .Select(i => new GroceryItem
                    {
                        Id = i.Id,
                        Name = i.Name,
                        Category = i.Category,
                        Quantity = i.Quantity,
                        Note = i.Note,
                        IsBought = false
                    }).ToList();

                for (int i = 0; i < loaded.Count; i++) loaded[i].Id = i + 1;

                _groceryItems = loaded;
                TempData["SuccessMessage"] = $"List '{listName}' loaded!";
            }
            else TempData["ErrorMessage"] = "Selected list not found.";

            return RedirectToAction(nameof(List));
        }

        // ✅ FIXED: Now shows a page instead of blank 404
        [HttpGet]
        public IActionResult Generate()
        {
            var model = new ShoppingListRequestViewModel();
            return View("GenerateForm", model); // Looks for Views/Grocery/GenerateForm.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Generate(ShoppingListRequestViewModel model)
        {
            if (!ModelState.IsValid || model.AgeGroups == null || model.AgeGroups.Count == 0)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["ErrorMessage"] = "Invalid form: " + string.Join("; ", errors);
                return RedirectToAction("Generate");
            }

            var groceryList = GetSmartGroceryList(model);
            var vm = new GeneratedGroceryItemViewModel();

            foreach (var gi in groceryList)
            {
                int calories = 0, protein = 0;
                if (!string.IsNullOrWhiteSpace(gi.Note))
                {
                    var trimmed = gi.Note.Replace("Est. ", "")
                                         .Replace(" kcal / ", "|")
                                         .Replace("g protein per unit", "");
                    var parts = trimmed.Split('|');
                    if (parts.Length == 2)
                    {
                        int.TryParse(parts[0], out calories);
                        int.TryParse(parts[1], out protein);
                    }
                }

                vm.Items.Add(new GeneratedGroceryItem
                {
                    Name = gi.Name,
                    Category = gi.Category,
                    Quantity = gi.Quantity,
                    CaloriesPerUnit = calories,
                    ProteinPerUnit = protein
                });
            }

            return View("GeneratedList", vm);
        }

        private List<GroceryItem> GetSmartGroceryList(ShoppingListRequestViewModel model)
        {
            var foodMap = new Dictionary<string, List<(string Name, string Category, int Calories, int Protein)>>()
            {
                ["Fruit"] = new()
                {
                    ("Apple", "Fruit", 95, 0),
                    ("Banana", "Fruit", 105, 1),
                    ("Orange", "Fruit", 62, 1),
                    ("Grapes", "Fruit", 62, 1),
                    ("Pear", "Fruit", 102, 1)
                },
                ["Veg"] = new()
                {
                    ("Carrot", "Vegetable", 25, 1),
                    ("Broccoli", "Vegetable", 50, 4),
                    ("Spinach", "Vegetable", 23, 3),
                    ("Tomato", "Vegetable", 22, 1),
                    ("Cucumber", "Vegetable", 16, 1)
                },
                ["Meat"] = new()
                {
                    ("Chicken Breast", "Meat", 165, 31),
                    ("Beef Steak", "Meat", 250, 26),
                    ("Pork Chop", "Meat", 242, 27),
                    ("Turkey", "Meat", 135, 29),
                    ("Lamb", "Meat", 294, 25)
                },
                ["Grain"] = new()
                {
                    ("Brown Rice", "Grain", 215, 5),
                    ("Whole Bread", "Grain", 80, 3),
                    ("Pasta", "Grain", 200, 7),
                    ("Quinoa", "Grain", 120, 4),
                    ("Oats", "Grain", 150, 6)
                },
                ["Dairy"] = new()
                {
                    ("Milk", "Dairy", 150, 8),
                    ("Yogurt", "Dairy", 100, 5),
                    ("Cheese", "Dairy", 113, 7),
                    ("Butter", "Dairy", 102, 0),
                    ("Cream", "Dairy", 52, 0)
                }
            };

            var totals = new Dictionary<string, double>
            {
                { "Fruit", 0 },
                { "Veg", 0 },
                { "Meat", 0 },
                { "Grain", 0 },
                { "Dairy", 0 }
            };

            foreach (var age in model.AgeGroups)
            {
                int days = model.DurationInDays;
                switch (age.ToLower())
                {
                    case "baby":
                        totals["Fruit"] += 1 * days;
                        totals["Veg"] += 1 * days;
                        totals["Meat"] += 0.5 * days;
                        totals["Grain"] += 0.5 * days;
                        totals["Dairy"] += 1 * days;
                        break;
                    case "child":
                        totals["Fruit"] += 2 * days;
                        totals["Veg"] += 2 * days;
                        totals["Meat"] += 1 * days;
                        totals["Grain"] += 2 * days;
                        totals["Dairy"] += 2 * days;
                        break;
                    case "senior":
                        totals["Fruit"] += 2 * days;
                        totals["Veg"] += 2 * days;
                        totals["Meat"] += 1.5 * days;
                        totals["Grain"] += 2 * days;
                        totals["Dairy"] += 2 * days;
                        break;
                    default:
                        totals["Fruit"] += 3 * days;
                        totals["Veg"] += 3 * days;
                        totals["Meat"] += 2 * days;
                        totals["Grain"] += 3 * days;
                        totals["Dairy"] += 2 * days;
                        break;
                }
            }

            var resultList = new List<GroceryItem>();

            foreach (var category in totals.Keys)
            {
                if (!foodMap.ContainsKey(category))
                    continue;

                var categoryTotal = totals[category];
                if (categoryTotal <= 0)
                    continue;

                var options = foodMap[category];
                int pickCount = options.Count >= 5 ? 5 : options.Count;
                int perItemQty = (int)System.Math.Ceiling(categoryTotal / (double)pickCount);

                for (int i = 0; i < pickCount; i++)
                {
                    var itemInfo = options[i];
                    resultList.Add(new GroceryItem
                    {
                        Name = itemInfo.Name,
                        Category = itemInfo.Category,
                        Quantity = perItemQty,
                        Note = $"Est. {itemInfo.Calories} kcal / {itemInfo.Protein}g protein per unit",
                        IsBought = false
                    });
                }
            }

            return resultList;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddGeneratedItems(GeneratedGroceryItemViewModel vm)
        {
            if (vm.Items != null && vm.Items.Any())
            {
                foreach (var gen in vm.Items)
                {
                    var newItem = new GroceryItem
                    {
                        Id = _groceryItems.Count > 0 ? _groceryItems.Max(x => x.Id) + 1 : 1,
                        Name = gen.Name,
                        Category = gen.Category,
                        Quantity = gen.Quantity,
                        Note = $"Est. {gen.CaloriesPerUnit} kcal / {gen.ProteinPerUnit}g protein per unit",
                        IsBought = false
                    };
                    _groceryItems.Add(newItem);
                }

                TempData["SuccessMessage"] = $"{vm.Items.Count} items added to your grocery list!";
            }
            else
            {
                TempData["ErrorMessage"] = "No items to add.";
            }

            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearAll()
        {
            _groceryItems.Clear();
            TempData["SuccessMessage"] = "All items have been removed from your grocery list.";
            return RedirectToAction(nameof(List));
        }

        [HttpPost]
        public async Task<JsonResult> AddRecipeIngredients(int id)
        {
            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null && System.IO.File.Exists(_jsonFilePath))
            {
                var json = await System.IO.File.ReadAllTextAsync(_jsonFilePath);
                var jsonRecipes = JsonSerializer.Deserialize<List<Recipe>>(json);
                recipe = jsonRecipes?.FirstOrDefault(r => r.Id == id);
            }

            if (recipe == null)
                return Json(new { success = false });

            var stopWords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "tbsp","tsp","cup","cups","g","kg","oz","ml","l","grams","pinch","cloves",
                "bunch","small","large","hot","red","fresh","finely","chopped","crushed","peeled",
                "cut","leaves","to","serve","plus","extra","natural","and"
            };

            foreach (var line in recipe.Ingredients)
            {
                var tokens = line
                    .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.TrimEnd(',', '.'))
                    .ToArray();

                var captured = new List<string>();
                bool started = false;

                for (int i = 0; i < tokens.Length; i++)
                {
                    var tk = tokens[i].Trim();
                    var lowerTk = tk.ToLower();

                    if (!started)
                    {
                        bool isNumeric = lowerTk.Any(char.IsDigit);
                        if (isNumeric || stopWords.Contains(lowerTk))
                            continue;
                        started = true;
                    }

                    if (started && stopWords.Contains(lowerTk))
                        break;

                    if (started)
                        captured.Add(tk);
                }

                var product = captured.Count > 0
                    ? string.Join(" ", captured)
                    : line.Trim();

                if (!_groceryItems.Any(x => x.Name.Trim().Equals(product, System.StringComparison.OrdinalIgnoreCase)))
                {
                    var newItem = new GroceryItem
                    {
                        Id = _groceryItems.Count > 0 ? _groceryItems.Max(x => x.Id) + 1 : 1,
                        Name = product,
                        Category = "Other",
                        Quantity = 1,
                        Note = "",
                        IsBought = false
                    };
                    _groceryItems.Add(newItem);
                }
            }

            return Json(new { success = true });
        }
    }
}
