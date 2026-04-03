using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using Recipes.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Recipes.Controllers
{
    [Authorize]
    public class ImportController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RewriteRecipeService _rewriteService;

        private readonly string _defaultsFolder;
        private readonly string _importFolder;

        public ImportController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory,
            RewriteRecipeService rewriteService)
        {
            _db = db;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _rewriteService = rewriteService;

            _defaultsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "defaults");
            _importFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "imported");

            if (!Directory.Exists(_importFolder))
                Directory.CreateDirectory(_importFolder);
        }

        // ───────────────────────────────────────────────
        // IMPORT PAGE
        // ───────────────────────────────────────────────
        [HttpGet("/Import")]
        public IActionResult Index() => View();

        // ───────────────────────────────────────────────
        // IMPORT RECIPE
        // ───────────────────────────────────────────────
        // ───────────────────────────────────────────────
        // IMPORT RECIPE
        // ───────────────────────────────────────────────
        [HttpPost("/Import/ImportFromUrl")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportFromUrl(string recipeUrl)
        {
            if (string.IsNullOrWhiteSpace(recipeUrl))
            {
                TempData["ErrorMessage"] = "Please enter a valid recipe URL.";
                return RedirectToAction(nameof(Index));
            }

            // Fetch HTML
            string html = await FetchHtml(recipeUrl);
            if (string.IsNullOrWhiteSpace(html))
            {
                TempData["ErrorMessage"] = "Unable to fetch the webpage content.";
                return RedirectToAction(nameof(Index));
            }

            // Extract basic data
            string title = ExtractTitleFromWebPage(html) ?? ExtractTitleFromUrl(recipeUrl);
            string host = new Uri(recipeUrl).Host.Replace("www.", "");

            var ingredients = ExtractIngredients(html);
            var instructions = ExtractInstructions(html);
            string imageUrl = ExtractImageUrl(html, recipeUrl);


            if (ingredients.Count == 0)
                ingredients.Add("No ingredients detected.");

            if (string.IsNullOrWhiteSpace(instructions))
                instructions = "No instructions detected.";

            // Create base recipe object
            var importedRecipe = new Recipe
            {
                Title = title,
                ShortDescription = "Imported from external source",
                Ingredients = ingredients,
                IngredientsJson = JsonSerializer.Serialize(ingredients),
                Instructions = instructions,
                ImageUrl = imageUrl,
                Allergens = new List<string>(),
                DateAdded = DateTime.UtcNow,
                CreatedAt = DateTime.Now
            };

            // ⭐ LOCAL REWRITE SECTION — TITLE + INSTRUCTIONS ONLY ⭐

            // 1️⃣ Rewrite ONLY the title to make it unique
            importedRecipe.Title = _rewriteService.RewriteTitle(importedRecipe.Title);

            // 2️⃣ Rewrite ONLY the cooking method/instructions (change wording to avoid plagiarism)
            importedRecipe.Instructions = _rewriteService.RewriteText(importedRecipe.Instructions);

            // 3️⃣ DO NOT rewrite ingredients (critical for accuracy)
            // importedRecipe.Ingredients stays exactly as imported


            // ⭐ MISSING RETURN BLOCK — REQUIRED FOR COMPILATION ⭐

            // Save to database
            await SaveImportedRecipe(importedRecipe);

            TempData["SuccessMessage"] = $"Recipe '{importedRecipe.Title}' imported successfully!";
            return RedirectToAction("Index", "Recipes");
        }


        // ───────────────────────────────────────────────────────────────
        // PREVIEW IMPORT
        // ───────────────────────────────────────────────────────────────
        [HttpPost("/Import/PreviewFromUrl")]
        public async Task<IActionResult> PreviewFromUrl([FromBody] JsonElement body)
        {
            try
            {
                if (!body.TryGetProperty("recipeUrl", out var urlElement))
                    return Json(new { success = false, message = "Invalid request." });

                string url = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(url))
                    return Json(new { success = false, message = "Enter a valid recipe URL." });

                string html = await FetchHtml(url);
                if (string.IsNullOrWhiteSpace(html))
                    return Json(new { success = false, message = "Unable to fetch webpage." });

                string title = ExtractTitleFromWebPage(html) ?? ExtractTitleFromUrl(url);
                string host = new Uri(url).Host.Replace("www.", "");
                string imageUrl = ExtractImageUrl(html, url);


                return Json(new { success = true, title, imageUrl, source = host });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ───────────────────────────────────────────────────────────────
        // FETCH HTML PAGE
        // ───────────────────────────────────────────────────────────────
        private async Task<string> FetchHtml(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                client.DefaultRequestHeaders.Add("Accept", "text/html");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                client.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return "";

                return await response.Content.ReadAsStringAsync();
            }
            catch
            {
                return "";
            }
        }

        // ───────────────────────────────────────────────────────────────
        // TITLE EXTRACTION
        // ───────────────────────────────────────────────────────────────
        private string ExtractTitleFromWebPage(string html)
        {
            var match = Regex.Match(html, @"<title>(.*?)<\/title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!match.Success) return null;

            string title = match.Groups[1].Value;
            title = Regex.Replace(title, @"\s*\|.*$", "").Trim();
            return WebUtility.HtmlDecode(title);
        }

        private string ExtractTitleFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                string segment = uri.Segments.LastOrDefault()?.Trim('/') ?? "Imported Recipe";
                segment = Path.GetFileNameWithoutExtension(segment);
                return string.Join(" ", segment.Split('-').Select(w => char.ToUpper(w[0]) + w[1..]));
            }
            catch { return "Imported Recipe"; }
        }

        // ───────────────────────────────────────────────────────────────
        // EXTRACT INGREDIENTS — UNIVERSAL
        // ───────────────────────────────────────────────────────────────
        private List<string> ExtractIngredients(string html)
        {
            var ingredients = new List<string>();
            if (string.IsNullOrWhiteSpace(html)) return ingredients;

            var jsonBlocks = Regex.Matches(html,
                @"<script[^>]*type=['""]application/ld\+json['""][^>]*>(.*?)<\/script>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in jsonBlocks)
            {
                try
                {
                    using var doc = JsonDocument.Parse(m.Groups[1].Value.Trim());
                    foreach (var node in ExtractGraphNodes(doc))
                    {
                        if (node.TryGetProperty("@type", out var type) &&
                            type.ToString().Contains("Recipe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (node.TryGetProperty("recipeIngredient", out var ingNode) &&
                                ingNode.ValueKind == JsonValueKind.Array)
                            {
                                ingredients.AddRange(
                                    ingNode.EnumerateArray()
                                        .Select(i => i.GetString()?.Trim())
                                        .Where(i => !string.IsNullOrWhiteSpace(i))
                                );
                            }
                        }
                    }
                }
                catch { }
            }

            if (ingredients.Count > 0) return ingredients.Distinct().ToList();

            var mdIng = Regex.Match(
                html,
                @"##\s*Ingredients\s*(?<list>(?:[*\-–]\s?.*\n?|\d+\.\s+.*\n?)+)",
                RegexOptions.IgnoreCase);

            if (mdIng.Success)
            {
                var lines = Regex.Matches(mdIng.Groups["list"].Value, @"(?:[*\-–]\s*|\d+\.\s*)(.+)");
                foreach (Match l in lines)
                {
                    string text = l.Groups[1].Value.Trim();
                    if (text.Length > 1) ingredients.Add(WebUtility.HtmlDecode(text));
                }
            }

            if (ingredients.Count > 0) return ingredients.Distinct().ToList();

            var ulMatches = Regex.Matches(html,
                @"<ul[^>]*?(ingredient|ingredients)[^>]*?>(.*?)<\/ul>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match ul in ulMatches)
            {
                var liMatches = Regex.Matches(ul.Groups[2].Value,
                    @"<li[^>]*>(.*?)<\/li>", RegexOptions.Singleline);

                foreach (Match li in liMatches)
                {
                    string cleaned = Regex.Replace(li.Groups[1].Value, "<.*?>", "").Trim();
                    if (cleaned.Length > 1) ingredients.Add(WebUtility.HtmlDecode(cleaned));
                }
            }

            return ingredients.Distinct().ToList();
        }

        // ───────────────────────────────────────────────────────────────
        // EXTRACT INSTRUCTIONS — UNIVERSAL
        // ───────────────────────────────────────────────────────────────
        private string ExtractInstructions(string html)
        {
            var steps = new List<string>();
            if (string.IsNullOrWhiteSpace(html)) return "";

            var jsonBlocks = Regex.Matches(html,
                @"<script[^>]*type=['""]application/ld\+json['""][^>]*>(.*?)<\/script>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in jsonBlocks)
            {
                try
                {
                    using var doc = JsonDocument.Parse(m.Groups[1].Value.Trim());
                    foreach (var node in ExtractGraphNodes(doc))
                    {
                        if (node.TryGetProperty("@type", out var type) &&
                            type.ToString().Contains("Recipe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (node.TryGetProperty("recipeInstructions", out var instrNode))
                                steps.AddRange(ParseInstructionNode(instrNode));
                        }
                    }
                }
                catch { }
            }

            if (steps.Count > 0) return string.Join("\n", steps);

            var mdMethod = Regex.Match(
                html,
                @"##\s*(Method|Method of Cooking)\s*(?<list>.*?)(##|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (mdMethod.Success)
            {
                var lines = Regex.Matches(mdMethod.Groups["list"].Value,
                    @"(?:###\s*step\s*\d+\.?|[*\-–]\s*|\d+\.\s*)(.+)",
                    RegexOptions.IgnoreCase);

                foreach (Match l in lines)
                {
                    string text = l.Groups[1].Value.Trim();
                    if (text.Length > 3) steps.Add(WebUtility.HtmlDecode(text));
                }
            }

            if (steps.Count > 0) return string.Join("\n", steps);

            var htmlBlocks = Regex.Matches(html,
                @"<(div|ol|ul)[^>]*?(method|instruction|steps|directions)[^>]*?>(.*?)<\/\1>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match block in htmlBlocks)
            {
                var items = Regex.Matches(block.Groups[3].Value,
                    @"<(li|p)[^>]*>(.*?)<\/\1>", RegexOptions.Singleline);

                foreach (Match item in items)
                {
                    string cleaned = Regex.Replace(item.Groups[2].Value, "<.*?>", "").Trim();
                    if (cleaned.Length > 3) steps.Add(WebUtility.HtmlDecode(cleaned));
                }
            }

            return string.Join("\n", steps);
        }

        // ───────────────────────────────────────────────────────────────
        // JSON-LD GRAPH HELPERS
        // ───────────────────────────────────────────────────────────────
        private IEnumerable<JsonElement> ExtractGraphNodes(JsonDocument doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.EnumerateArray();

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("@graph", out var graph) &&
                graph.ValueKind == JsonValueKind.Array)
                return graph.EnumerateArray();

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                return new[] { doc.RootElement };

            return Array.Empty<JsonElement>();
        }

        private IEnumerable<string> ParseInstructionNode(JsonElement node)
        {
            var steps = new List<string>();

            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in node.EnumerateArray())
                {
                    if (step.ValueKind == JsonValueKind.String)
                        steps.Add(step.GetString());
                    else if (step.TryGetProperty("text", out var text))
                        steps.Add(text.GetString());
                    else if (step.TryGetProperty("name", out var name))
                        steps.Add(name.GetString());
                }
            }
            else if (node.ValueKind == JsonValueKind.String)
            {
                steps.Add(node.GetString());
            }

            return steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
        }

        // ───────────────────────────────────────────────────────────────
        // IMAGE EXTRACTION (SAFE — DOES NOT DOWNLOAD IMAGES)
        // ───────────────────────────────────────────────────────────────
        private string ExtractImageUrl(string html, string recipeUrl)
        {
            try
            {
                string imageUrl = null;

                // 1️⃣ JSON-LD images
                var jsonMatches = Regex.Matches(html,
                    @"<script[^>]*type=['""]application/ld\+json['""][^>]*>(.*?)<\/script>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match m in jsonMatches)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(m.Groups[1].Value.Trim());
                        foreach (var node in ExtractGraphNodes(doc))
                        {
                            if (node.TryGetProperty("@type", out var type) &&
                                type.ToString().Contains("Recipe", StringComparison.OrdinalIgnoreCase))
                            {
                                if (node.TryGetProperty("image", out var imgNode))
                                {
                                    // Image can be string or an array
                                    if (imgNode.ValueKind == JsonValueKind.String)
                                        return MakeAbsolute(imgNode.GetString(), recipeUrl);

                                    if (imgNode.ValueKind == JsonValueKind.Array &&
                                        imgNode.GetArrayLength() > 0)
                                        return MakeAbsolute(imgNode[0].GetString(), recipeUrl);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2️⃣ OpenGraph <meta property="og:image">
                var og = Regex.Match(html,
                    @"<meta[^>]+property=[""']og:image[""'][^>]+content=[""']([^""']+)[""']",
                    RegexOptions.IgnoreCase);
                if (og.Success)
                    return MakeAbsolute(og.Groups[1].Value, recipeUrl);

                // 3️⃣ Twitter <meta name="twitter:image">
                var tw = Regex.Match(html,
                    @"<meta[^>]+name=[""']twitter:image[""'][^>]+content=[""']([^""']+)[""']",
                    RegexOptions.IgnoreCase);
                if (tw.Success)
                    return MakeAbsolute(tw.Groups[1].Value, recipeUrl);

                // 4️⃣ Fallback: <img src="">
                var img = Regex.Match(html,
                    @"<img[^>]+(src|data-src)=['""]([^'""]+\.(jpg|jpeg|png|webp))['""]",
                    RegexOptions.IgnoreCase);
                if (img.Success)
                    return MakeAbsolute(img.Groups[2].Value, recipeUrl);

                // 5️⃣ Nothing found → default
                return "/uploads/default-recipe.jpg";
            }
            catch
            {
                return "/uploads/default-recipe.jpg";
            }
        }

        // Helper — convert relative URL → absolute
        private string MakeAbsolute(string url, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return url;

            return new Uri(new Uri(baseUrl), url).ToString();
        }


        // ───────────────────────────────────────────────────────────────
        // CATEGORY DETECTOR
        // ───────────────────────────────────────────────────────────────
        private string AutoAssignCategory(string title, string description, List<string> ingredients)
        {
            var txt = (title + " " + description + " " + string.Join(" ", ingredients)).ToLower();

            string[] healthy = {
                "vegan","vegetarian","salad","broccoli","spinach","quinoa","lentil","zucchini",
                "kale","tofu","chickpea","bean","edamame","avocado","vegetable","veg","leafy",
                "detox","clean","grain bowl","wholegrain","oat","smoothie"
            };

            string[] quick = {
                "quick","easy","minute","simple","under","weeknight","fast","speedy","express",
                "traybake","stir fry","one pot","one-pan","instant","microwave","ready"
            };

            string[] comfort = {
                "comfort","lasagna","pie","stew","casserole","roast","mash","cheese","butter",
                "cream","bacon","potato","rich","slow cooked","fried","creamy","hearty","bbq",
                "burger","soup","chili","chilli","pasta","bake","mac","dumpling"
            };

            bool HasAny(string[] list) => list.Any(k => txt.Contains(k));

            if (HasAny(quick)) return "Quick & Easy";
            if (HasAny(comfort)) return "Comfort Cravings";
            if (HasAny(healthy)) return "Healthy Bites";

            return "Comfort Cravings";
        }

        // ───────────────────────────────────────────────────────────────
        // ALLERGEN DETECTOR
        // ───────────────────────────────────────────────────────────────
        private List<string> DetectAllergensFromIngredients(List<string> ingredients)
        {
            if (ingredients == null || ingredients.Count == 0) return new List<string>();

            string txt = string.Join(" ", ingredients).ToLower();

            var allergenMap = new Dictionary<string, List<string>>
            {
                { "Milk", new(){ "milk","cheese","butter","cream","yogurt","whey","casein" } },
                { "Eggs", new(){ "egg","eggs","mayo","mayonnaise" } },
                { "Fish", new(){ "salmon","cod","tuna","hake","trout","pollock","fish" } },
                { "Shellfish (Crustaceans)", new(){ "shrimp","prawn","crab","lobster" } },
                { "Molluscs", new(){ "mussel","oyster","clam","scallop","octopus","squid" } },
                { "Gluten", new(){ "wheat","flour","bread","pasta","rye","barley","couscous" } },
                { "Peanuts", new(){ "peanut","groundnut","satay" } },
                { "Tree Nuts", new(){ "almond","walnut","cashew","pecan","hazelnut","pistachio","nut","pesto" } },
                { "Soy", new(){ "soy","soya","tofu","edamame","miso","tempeh" } },
                { "Sesame", new(){ "sesame","tahini" } },
                { "Mustard", new(){ "mustard","dijon" } },
                { "Celery", new(){ "celery","celeriac" } },
                { "Lupin", new(){ "lupin" } },
                { "Sulphites", new(){ "sulphite","sulfite","e220","e221","e222","e223" } }
            };

            var detected = new List<string>();

            foreach (var allergen in allergenMap)
            {
                if (allergen.Value.Any(k => txt.Contains(k)))
                    detected.Add(allergen.Key);
            }

            return detected;
        }

        // ───────────────────────────────────────────────────────────────
        // SAVE IMPORTED RECIPE
        // ───────────────────────────────────────────────────────────────
        private async Task SaveImportedRecipe(Recipe recipe)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            bool exists = await _db.Recipes.AnyAsync(r =>
                r.Title == recipe.Title && r.ApplicationUserId == user.Id);

            if (exists) return;

            recipe.Category = AutoAssignCategory(
                recipe.Title,
                recipe.ShortDescription ?? "",
                recipe.Ingredients
            );

            recipe.Allergens = DetectAllergensFromIngredients(recipe.Ingredients);
            recipe.ApplicationUserId = user.Id;
            recipe.IngredientsJson = JsonSerializer.Serialize(recipe.Ingredients);

            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();

        }

        }
    }

       
   
