using Microsoft.AspNetCore.Mvc;
using Recipes.Models;
using Recipes.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Recipes.Controllers
{
    public class GroceryController : Controller
    {
        private static List<GroceryItem> _groceryItems = new();
        private static Dictionary<string, List<GroceryItem>> _savedLists = new();

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

        // ✅ GET: Show form
        [HttpGet]
        public IActionResult Generate()
        {
            return View(new ShoppingListRequestViewModel());
        }

        // ✅ POST: Process form
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

            var list = GetSmartGroceryList(model);
            return View("GeneratedList", list);
        }

        private List<GroceryItem> GetSmartGroceryList(ShoppingListRequestViewModel model)
        {
            var foodMap = new Dictionary<string, List<(string Name, string Category, int Calories, int Protein)>>()
            {
                ["Fruit"] = new() { ("Apple", "Fruit", 95, 0), ("Banana", "Fruit", 105, 1) },
                ["Veg"] = new() { ("Carrot", "Vegetable", 25, 1), ("Broccoli", "Vegetable", 50, 4) },
                ["Protein"] = new() { ("Chicken Breast", "Meat", 165, 31), ("Beans", "Legume", 120, 8) },
                ["Grain"] = new() { ("Brown Rice", "Grain", 215, 5), ("Whole Bread", "Grain", 80, 3) },
                ["Dairy"] = new() { ("Milk", "Dairy", 150, 8), ("Yogurt", "Dairy", 100, 5) }
            };

            var totals = new Dictionary<string, double> {
                { "Fruit", 0 }, { "Veg", 0 }, { "Protein", 0 }, { "Grain", 0 }, { "Dairy", 0 }
            };

            foreach (var age in model.AgeGroups)
            {
                int d = model.DurationInDays;
                switch (age.ToLower())
                {
                    case "baby":
                        totals["Fruit"] += 1 * d;
                        totals["Veg"] += 1 * d;
                        totals["Protein"] += 0.5 * d;
                        totals["Grain"] += 0.5 * d;
                        totals["Dairy"] += 1 * d;
                        break;
                    case "child":
                        totals["Fruit"] += 2 * d;
                        totals["Veg"] += 2 * d;
                        totals["Protein"] += 1 * d;
                        totals["Grain"] += 2 * d;
                        totals["Dairy"] += 2 * d;
                        break;
                    case "senior":
                        totals["Fruit"] += 2 * d;
                        totals["Veg"] += 2 * d;
                        totals["Protein"] += 1.5 * d;
                        totals["Grain"] += 2 * d;
                        totals["Dairy"] += 2 * d;
                        break;
                    default:
                        totals["Fruit"] += 3 * d;
                        totals["Veg"] += 3 * d;
                        totals["Protein"] += 2 * d;
                        totals["Grain"] += 3 * d;
                        totals["Dairy"] += 2 * d;
                        break;
                }
            }

            var list = new List<GroceryItem>();
            foreach (var category in totals.Keys)
            {
                if (foodMap.ContainsKey(category))
                {
                    var item = foodMap[category].First();
                    list.Add(new GroceryItem
                    {
                        Name = item.Name,
                        Category = item.Category,
                        Quantity = (int)System.Math.Ceiling(totals[category]),
                        Note = $"Est. {item.Calories} kcal / {item.Protein}g protein per unit",
                        IsBought = false
                    });
                }
            }

            return list;
        }
    }
}
