using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Recipes.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ✅ GET: /Profile/View/{id}
        [HttpGet("/Profile/View/{id}")]
        public async Task<IActionResult> View(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            // Load user’s recipes
            var userRecipes = await _db.Recipes
                .Where(r => r.ApplicationUserId == id)
                .ToListAsync();

            ViewBag.RecipeCount = userRecipes.Count;
            ViewBag.Recipes = userRecipes;

            return View(user);
        }
    }
}
