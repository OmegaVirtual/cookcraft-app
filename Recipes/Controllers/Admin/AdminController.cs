using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using System.Globalization;

namespace Recipes.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ───────────────────────────────────────────────
        // ADMIN DASHBOARD
        // ───────────────────────────────────────────────
        public async Task<IActionResult> Dashboard()
        {
            // ─────────────── BASIC STATS ───────────────
            ViewBag.TotalUsers = await _db.Users.CountAsync();
            ViewBag.TotalRecipes = await _db.Recipes.CountAsync();
            ViewBag.TotalViews = await _db.Recipes.SumAsync(r => r.Views);
            ViewBag.FlaggedRecipes = await _db.Recipes.CountAsync(r => r.FlaggedForReview == true);
            ViewBag.BannedUsers = await _db.Users.CountAsync(u => u.LockoutEnd != null);

            // ─────────────── DATE RANGE FOR ANALYTICS ───────────────
            DateTime today = DateTime.UtcNow.Date;
            DateTime weekAgo = today.AddDays(-6);

            // ─────────────── DAILY ACTIVE USERS ───────────────
            // Users are "active" if RegistrationDate or LockoutEnd or Profile update is recorded.
            var activeData = await _db.Users
                .Where(u => u.RegistrationDate >= weekAgo)
                .GroupBy(u => u.RegistrationDate.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var activeUsersDaily = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                var day = weekAgo.AddDays(i);
                activeUsersDaily[day.ToString("dd MMM")] =
                    activeData.FirstOrDefault(a => a.Day == day)?.Count ?? 0;
            }

            ViewBag.ActiveUsersDaily = activeUsersDaily;

            // ─────────────── RECIPES ADDED PER DAY ───────────────
            var recipeData = await _db.Recipes
                .Where(r => r.DateAdded >= weekAgo)
                .GroupBy(r => r.DateAdded.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var recipesDaily = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                var day = weekAgo.AddDays(i);
                recipesDaily[day.ToString("dd MMM")] =
                    recipeData.FirstOrDefault(r => r.Day == day)?.Count ?? 0;
            }

            ViewBag.RecipesDaily = recipesDaily;

            // ─────────────── TOTAL VIEWS PER DAY (approximate) ───────────────
            // We estimate views by CreatedAt date because SQLite has no per-view logs.
            var viewsData = await _db.Recipes
                .Where(r => r.CreatedAt >= weekAgo && r.Views > 0)
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new { Day = g.Key, Views = g.Sum(r => r.Views) })
                .ToListAsync();

            var viewsDaily = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                var day = weekAgo.AddDays(i);
                viewsDaily[day.ToString("dd MMM")] =
                    viewsData.FirstOrDefault(v => v.Day == day)?.Views ?? 0;
            }

            ViewBag.ViewsDaily = viewsDaily;

            return View();
        }
    }
}
