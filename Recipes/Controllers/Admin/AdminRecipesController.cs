using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;

namespace Recipes.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminRecipesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminRecipesController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ───────────────────────────────────────────
        // LIST ALL RECIPES
        // ───────────────────────────────────────────
        public async Task<IActionResult> Index(string search)
        {
            var recipes = await _db.Recipes
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                recipes = recipes.Where(r =>
                    (r.Title != null && r.Title.ToLower().Contains(search)) ||
                    (r.ShortDescription != null && r.ShortDescription.ToLower().Contains(search))
                ).ToList();
            }

            return View(recipes);
        }

        // ───────────────────────────────────────────
        // FLAGGED RECIPES PAGE
        // ───────────────────────────────────────────
        public async Task<IActionResult> Flagged()
        {
            var recipes = await _db.Recipes
                .Where(r => r.FlaggedForReview == true)
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return View(recipes);
        }

        // ───────────────────────────────────────────
        // DELETE RECIPE
        // ───────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null)
                return NotFound();

            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Recipe deleted.";
            return RedirectToAction("Index");
        }
    }
}
