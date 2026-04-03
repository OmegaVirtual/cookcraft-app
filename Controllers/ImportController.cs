using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Recipes.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System;

namespace Recipes.Controllers
{
    public class ImportController : Controller
    {
        private static List<Recipe> _recipes = new List<Recipe>();
        private readonly string _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");

        public ImportController()
        {
            _recipes = LoadRecipes();
        }

        private List<Recipe> LoadRecipes()
        {
            if (!System.IO.File.Exists(_filePath))
            {
                System.IO.File.WriteAllText(_filePath, "[]");
            }

            var json = System.IO.File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<Recipe>>(json) ?? new List<Recipe>();
        }

        private void SaveRecipes()
        {
            var json = JsonSerializer.Serialize(_recipes, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_filePath, json);
        }

        // GET: Import/Index
        public IActionResult Index()
        {
            return View();
        }

        // POST: Import/UploadTxt
        [HttpPost]
        public IActionResult UploadTxt(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Please upload a valid text file.";
                return RedirectToAction(nameof(Index));
            }

            using var reader = new StreamReader(file.OpenReadStream());
            string content = reader.ReadToEnd();
            var recipe = ParseRecipeFromText(content);

            if (recipe == null)
            {
                TempData["ErrorMessage"] = "Could not parse the uploaded recipe file.";
                return RedirectToAction(nameof(Index));
            }

            _recipes.Add(recipe);
            SaveRecipes();

            TempData["SuccessMessage"] = "Recipe imported successfully!";
            return RedirectToAction(nameof(Index), "Recipes");
        }

        // POST: Import/ManualPaste
        [HttpPost]
        public IActionResult ManualPaste(string pastedText)
        {
            if (string.IsNullOrWhiteSpace(pastedText))
            {
                TempData["ErrorMessage"] = "Please paste valid recipe text.";
                return RedirectToAction(nameof(Index));
            }

            var recipe = ParseRecipeFromText(pastedText);

            if (recipe == null)
            {
                TempData["ErrorMessage"] = "Could not parse the pasted recipe text.";
                return RedirectToAction(nameof(Index));
            }

            _recipes.Add(recipe);
            SaveRecipes();

            TempData["SuccessMessage"] = "Recipe imported successfully!";
            return RedirectToAction(nameof(Index), "Recipes");
        }

        private Recipe ParseRecipeFromText(string text)
        {
            try
            {
                var lines = text.Split('\n').Select(x => x.Trim()).ToList();
                var titleLine = lines.FirstOrDefault(x => x.StartsWith("Title:"));
                var ingredientsStart = lines.FindIndex(x => x.StartsWith("Ingredients:"));
                var instructionsStart = lines.FindIndex(x => x.StartsWith("Instructions:"));

                if (titleLine == null || ingredientsStart == -1 || instructionsStart == -1)
                {
                    return null;
                }

                string title = titleLine.Replace("Title:", "").Trim();
                var ingredients = lines.Skip(ingredientsStart + 1).Take(instructionsStart - ingredientsStart - 1).ToList();
                var instructions = string.Join("\n", lines.Skip(instructionsStart + 1));

                return new Recipe
                {
                    Id = _recipes.Count > 0 ? _recipes.Max(r => r.Id) + 1 : 1,
                    Title = title,
                    ShortDescription = $"Imported recipe with {ingredients.Count} ingredients.",
                    Instructions = instructions,
                    ImageUrl = "/uploads/default-recipe.jpg", // You can assign a default image
                    Allergens = new List<string>() // We can trigger smart allergen detection if needed
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
