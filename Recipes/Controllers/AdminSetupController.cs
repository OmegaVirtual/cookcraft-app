using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Recipes.Models;

namespace Recipes.Controllers
{
    public class AdminSetupController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminSetupController(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // CREATE ADMIN ROLE
        public async Task<IActionResult> CreateRole()
        {
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            return Content("Admin role created.");
        }

        // ASSIGN ADMIN TO USER
        public async Task<IActionResult> MakeUserAdmin(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return Content("User not found");

            await _userManager.AddToRoleAsync(user, "Admin");

            return Content($"{email} is now an Admin!");
        }
    }
}
