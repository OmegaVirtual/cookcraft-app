using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recipes.Data;
using Recipes.Models;
using Recipes.Models.ViewModels;

namespace Recipes.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminUsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ───────────────────────────────────────────────
        // LIST ALL USERS
        // ───────────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.OrderBy(u => u.Email).ToListAsync();
            var userList = new List<UserAdminViewModel>();

            foreach (var u in users)
            {
                var recipeCount = await _db.Recipes.CountAsync(r => r.ApplicationUserId == u.Id);
                bool isBanned = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow;

                userList.Add(new UserAdminViewModel
                {
                    UserId = u.Id,
                    Email = u.Email ?? "",
                    Username = u.UserName ?? "",
                    DietOption = u.DietOption ?? "",
                    DateJoined = u.RegistrationDate,
                    RecipeCount = recipeCount,
                    IsBanned = isBanned
                });
            }

            return View(userList);
        }

        // ───────────────────────────────────────────────
        // BAN USER
        // ───────────────────────────────────────────────
        public async Task<IActionResult> Ban(string id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddDays(7);

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{user.Email} has been banned for 7 days.";
            return RedirectToAction("Index");
        }

        // ───────────────────────────────────────────────
        // UNBAN USER
        // ───────────────────────────────────────────────
        public async Task<IActionResult> Unban(string id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            user.LockoutEnd = null;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"{user.Email} has been unbanned.";
            return RedirectToAction("Index");
        }

        // ───────────────────────────────────────────────
        // DELETE USER
        // ───────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "User deleted successfully.";
            return RedirectToAction("Index");
        }
    }
}
