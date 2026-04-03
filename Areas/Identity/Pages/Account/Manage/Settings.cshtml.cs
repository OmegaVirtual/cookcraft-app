using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recipes.Models;

namespace Recipes.Areas.Identity.Pages.Account.Manage
{
    public class SettingsModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public SettingsModel(UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _env = env;
        }

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

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            Email = user.Email;
            DietOption = user.DietOption;
            ProfilePictureUrl = user.ProfilePictureUrl;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(DietOption))
                user.DietOption = DietOption;

            if (!string.IsNullOrEmpty(Email) && Email != user.Email)
            {
                var setEmail = await _userManager.SetEmailAsync(user, Email);
                if (!setEmail.Succeeded)
                {
                    foreach (var error in setEmail.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Page();
                }
            }

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

            // ? Reload the user again so the new image shows
            var updatedUser = await _userManager.GetUserAsync(User);
            Email = updatedUser.Email;
            DietOption = updatedUser.DietOption;
            ProfilePictureUrl = updatedUser.ProfilePictureUrl;

            return Page(); // NO redirect to ensure new image shows
        }
    }
}
