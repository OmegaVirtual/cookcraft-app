using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Recipes.Data;
using Recipes.Models;

namespace Recipes.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;
        private readonly ApplicationDbContext _context;

        public DownloadPersonalDataModel(
            UserManager<ApplicationUser> userManager,
            ILogger<DownloadPersonalDataModel> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        // Public dictionary for use in the Razor page
        public Dictionary<string, string> PersonalData { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Get personal data via [PersonalData] attributes
            var personalDataProps = typeof(ApplicationUser).GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(PersonalDataAttribute)));

            foreach (var prop in personalDataProps)
            {
                PersonalData.Add(prop.Name, prop.GetValue(user)?.ToString() ?? "null");
            }

            // Add recipe count
            int recipeCount = _context.Recipes.Count(r => r.ApplicationUserId == user.Id);

            PersonalData.Add("Recipes Added", recipeCount.ToString());

            // Include login providers and keys
            var logins = await _userManager.GetLoginsAsync(user);
            foreach (var login in logins)
            {
                PersonalData.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);
            }

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (!string.IsNullOrEmpty(authenticatorKey))
            {
                PersonalData.Add("Authenticator Key", authenticatorKey);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation("User with ID '{UserId}' requested to download their personal data.", _userManager.GetUserId(User));

            var personalData = new Dictionary<string, string>();

            var personalDataProps = typeof(ApplicationUser).GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(PersonalDataAttribute)));

            foreach (var prop in personalDataProps)
            {
                personalData.Add(prop.Name, prop.GetValue(user)?.ToString() ?? "null");
            }

            // Add recipe count for download too
            int recipeCount = _context.Recipes.Count(r => r.ApplicationUserId == user.Id);
            personalData.Add("Recipes Added", recipeCount.ToString());

            var logins = await _userManager.GetLoginsAsync(user);
            foreach (var login in logins)
            {
                personalData.Add($"{login.LoginProvider} external login provider key", login.ProviderKey);
            }

            var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (!string.IsNullOrEmpty(authenticatorKey))
            {
                personalData.Add("Authenticator Key", authenticatorKey);
            }

            Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
            return new FileContentResult(JsonSerializer.SerializeToUtf8Bytes(personalData), "application/json");
        }
    }
}
