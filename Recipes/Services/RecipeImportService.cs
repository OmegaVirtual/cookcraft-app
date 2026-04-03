using HtmlAgilityPack;
using Recipes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Recipes.Services
{
    public class RecipeImportService : IRecipeImportService
    {
        private readonly HttpClient _http;

        public RecipeImportService(HttpClient http)
        {
            _http = http;

            // Add a common browser User-Agent so sites like BBC Good Food will return full HTML
            if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/114.0.0.0 Safari/537.36"))
            {
                // If parsing fails, ignore—most sites still respond.
            }
        }

        public async Task<Recipe> ImportFromUrlAsync(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return null;

            try
            {
                // 1️⃣ Fetch HTML
                using var response = await _http.GetAsync(sourceUrl);
                if (!response.IsSuccessStatusCode)
                    return null;

                var html = await response.Content.ReadAsStringAsync();

                // 2️⃣ Load into HtmlAgilityPack to find JSON-LD
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var jsonLdNodes = doc.DocumentNode
                    .SelectNodes("//script[@type='application/ld+json']");

                if (jsonLdNodes != null)
                {
                    foreach (var node in jsonLdNodes)
                    {
                        var jsonText = node.InnerText.Trim();
                        if (string.IsNullOrWhiteSpace(jsonText))
                            continue;

                        try
                        {
                            using var docJson = JsonDocument.Parse(jsonText);
                            var root = docJson.RootElement;
                            JsonElement recipeElement = default;

                            // Case A: root is an array
                            if (root.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var el in root.EnumerateArray())
                                {
                                    if (TryExtractRecipeElement(el, out recipeElement))
                                        break;
                                }
                            }
                            // Case B: root is an object
                            else if (root.ValueKind == JsonValueKind.Object)
                            {
                                // If there's an "@graph" field, search inside
                                if (root.TryGetProperty("@graph", out var graphProp) &&
                                    graphProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var el in graphProp.EnumerateArray())
                                    {
                                        if (TryExtractRecipeElement(el, out recipeElement))
                                            break;
                                    }
                                }
                                // Otherwise, check if the root itself is a Recipe
                                else if (TryExtractRecipeElement(root, out recipeElement))
                                {
                                    // recipeElement is set
                                }
                            }

                            // If we found a recipe block, map it
                            if (recipeElement.ValueKind != JsonValueKind.Undefined)
                            {
                                var recipe = MapJsonLdToRecipe(recipeElement, sourceUrl);

                                // ✅ Ensure image always exists
                                if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
                                    recipe.ImageUrl = GetRandomDefaultImage();

                                return recipe;
                            }
                        }
                        catch
                        {
                            continue; // skip malformed JSON-LD
                        }
                    }
                }

                // 3️⃣ Fallback: custom scrape for BBC Good Food
                if (new Uri(sourceUrl).Host.Contains("bbcgoodfood.com", StringComparison.OrdinalIgnoreCase))
                {
                    var recipe = ScrapeBbcGoodFood(html, sourceUrl);

                    // ✅ Ensure default image if missing
                    if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
                        recipe.ImageUrl = GetRandomDefaultImage();

                    return recipe;
                }

                // 4️⃣ If all else fails
                return null;
            }
            catch
            {
                return null;
            }
        }

        // 🔍 Checks if the given JSON element represents a Recipe ("@type":"Recipe")
        private bool TryExtractRecipeElement(JsonElement el, out JsonElement recipeElement)
        {
            recipeElement = default;

            if (el.ValueKind != JsonValueKind.Object)
                return false;

            if (el.TryGetProperty("@type", out var typeProp) &&
                typeProp.ValueKind == JsonValueKind.String &&
                typeProp.GetString().Equals("Recipe", StringComparison.OrdinalIgnoreCase))
            {
                recipeElement = el;
                return true;
            }

            return false;
        }

        // 🧩 Maps the JSON-LD recipe object to our Recipe model
        private Recipe MapJsonLdToRecipe(JsonElement node, string sourceUrl)
        {
            // 1️⃣ Title
            var title = node.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString()!.Trim()
                : "";

            // 2️⃣ ShortDescription
            var shortDesc = node.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                ? descProp.GetString()!.Trim()
                : $"Imported recipe from {sourceUrl}";

            // 3️⃣ Instructions
            string instructions = "";
            if (node.TryGetProperty("recipeInstructions", out var instrProp))
            {
                if (instrProp.ValueKind == JsonValueKind.String)
                {
                    instructions = instrProp.GetString()!.Trim();
                }
                else if (instrProp.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var step in instrProp.EnumerateArray())
                    {
                        if (step.ValueKind == JsonValueKind.String)
                        {
                            sb.AppendLine(step.GetString()!);
                        }
                        else if (step.ValueKind == JsonValueKind.Object &&
                                 step.TryGetProperty("text", out var textProp) &&
                                 textProp.ValueKind == JsonValueKind.String)
                        {
                            sb.AppendLine(textProp.GetString()!);
                        }
                    }
                    instructions = sb.ToString().Trim();
                }
            }

            // 4️⃣ Ingredients
            var ingredients = new List<string>();
            if (node.TryGetProperty("recipeIngredient", out var ingrProp) &&
                ingrProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ingrProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        ingredients.Add(item.GetString()!.Trim());
                }
            }

            // 5️⃣ Image
            string imageUrl = "";
            if (node.TryGetProperty("image", out var imgProp))
            {
                if (imgProp.ValueKind == JsonValueKind.String)
                {
                    imageUrl = imgProp.GetString()!.Trim();
                }
                else if (imgProp.ValueKind == JsonValueKind.Array && imgProp.GetArrayLength() > 0)
                {
                    var firstImage = imgProp.EnumerateArray().First();
                    if (firstImage.ValueKind == JsonValueKind.String)
                        imageUrl = firstImage.GetString()!.Trim();
                }
            }

            var recipe = new Recipe
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Imported Recipe" : title,
                ShortDescription = shortDesc,
                Instructions = instructions,
                ImageUrl = imageUrl,
                Ingredients = ingredients
            };

            // ✅ Add default image if empty
            if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
                recipe.ImageUrl = GetRandomDefaultImage();

            return recipe;
        }

        // 🍳 Custom scraper for BBC Good Food when JSON-LD fails
        private Recipe ScrapeBbcGoodFood(string html, string sourceUrl)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 1️⃣ Title
            var titleNode = doc.DocumentNode
                .SelectSingleNode("//h1[contains(@class,'post-header__title')]");
            var title = titleNode != null
                ? HtmlEntity.DeEntitize(titleNode.InnerText).Trim()
                : "Imported Recipe";

            // 2️⃣ Description
            var shortDesc = $"Imported recipe from {sourceUrl}";

            // 3️⃣ Ingredients
            var ingredients = new List<string>();
            var ingredientNodes = doc.DocumentNode
                .SelectNodes("//span[contains(@class,'ingredients-list__item-name')]");
            if (ingredientNodes != null)
            {
                foreach (var ingr in ingredientNodes)
                {
                    var text = HtmlEntity.DeEntitize(ingr.InnerText).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        ingredients.Add(text);
                }
            }

            // 4️⃣ Instructions
            var instructionsSb = new StringBuilder();
            var methodNodes = doc.DocumentNode.SelectNodes("//li[contains(@class,'method__item')]");
            if (methodNodes != null)
            {
                foreach (var step in methodNodes)
                {
                    var text = HtmlEntity.DeEntitize(step.InnerText).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        instructionsSb.AppendLine(text);
                }
            }
            else
            {
                var fallbackNodes = doc.DocumentNode.SelectNodes("//section[contains(@class,'method')]//p");
                if (fallbackNodes != null)
                {
                    foreach (var p in fallbackNodes)
                    {
                        var text = HtmlEntity.DeEntitize(p.InnerText).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            instructionsSb.AppendLine(text);
                    }
                }
            }
            var instructions = instructionsSb.ToString().Trim();

            // 5️⃣ Image via meta og:image
            string imageUrl = "";
            var imgMeta = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            if (imgMeta != null && imgMeta.Attributes["content"] != null)
            {
                imageUrl = imgMeta.Attributes["content"].Value.Trim();
            }

            var recipe = new Recipe
            {
                Title = title,
                ShortDescription = shortDesc,
                Instructions = instructions,
                ImageUrl = imageUrl,
                Ingredients = ingredients
            };

            // ✅ Assign default image if none
            if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
                recipe.ImageUrl = GetRandomDefaultImage();

            return recipe;
        }

        // 🎨 Helper — get a random default image from /uploads/defaults
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
    }
}
