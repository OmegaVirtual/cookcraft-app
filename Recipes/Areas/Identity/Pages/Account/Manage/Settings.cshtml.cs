using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recipes.Data;
using Recipes.Models;

namespace Recipes.Areas.Identity.Pages.Account.Manage
{
    public class SettingsModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SettingsModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _env = env;
        }

        // ------------------------------------------------------------------
        // PROPERTIES
        // ------------------------------------------------------------------
        [BindProperty]
        [EmailAddress]
        public string Email { get; set; }

        [BindProperty]
        public string DietOption { get; set; }

        [BindProperty]
        public IFormFile Upload { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string ProfilePictureUrl { get; set; }

        // ------------------------------------------------------------------
        // GET: Load user data
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            Email = user.Email;
            DietOption = user.DietOption;
            ProfilePictureUrl = user.ProfilePictureUrl;

            return Page();
        }

        // ------------------------------------------------------------------
        // POST: Update settings
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            // Update diet option
            if (!string.IsNullOrEmpty(DietOption))
                user.DietOption = DietOption;

            // Update email
            if (!string.IsNullOrEmpty(Email) && Email != user.Email)
            {
                var setEmail = await _userManager.SetEmailAsync(user, Email);
                if (!setEmail.Succeeded)
                {
                    foreach (var error in setEmail.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);

                    return Page();
                }
            }

            // Upload new profile picture
            if (Upload != null && Upload.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(Upload.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await Upload.CopyToAsync(stream);

                user.ProfilePictureUrl = "/uploads/" + fileName;
            }

            await _userManager.UpdateAsync(user);

            StatusMessage = "Settings updated.";

            // Reload updated user
            var updatedUser = await _userManager.GetUserAsync(User);
            Email = updatedUser.Email;
            DietOption = updatedUser.DietOption;
            ProfilePictureUrl = updatedUser.ProfilePictureUrl;

            return Page();
        }

        // ------------------------------------------------------------------
        // POST: DELETE ACCOUNT
        // ------------------------------------------------------------------
        public async Task<IActionResult> OnPostDeleteAccountAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            // 1️⃣ Delete user's recipes
            var recipes = _db.Recipes.Where(r => r.ApplicationUserId == user.Id).ToList();
            _db.Recipes.RemoveRange(recipes);
            await _db.SaveChangesAsync();

            // 2️⃣ Delete profile picture (if not default)
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl) &&
                !user.ProfilePictureUrl.Contains("default"))
            {
                var filePath = Path.Combine(_env.WebRootPath, user.ProfilePictureUrl.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }

            // 3️⃣ Delete identity user
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                StatusMessage = "Error deleting account.";
                return RedirectToPage();
            }

            // 4️⃣ Sign out
            await _signInManager.SignOutAsync();

            return RedirectToPage("/Index");
        }
    }
}
