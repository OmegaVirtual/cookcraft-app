using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;  // ✅ Correct IEmailSender source
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Recipes.Models;

namespace Recipes.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;   // ✅ Email sender

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)   // ✅ Inject properly
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;  // ✅ Assign
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            public string Username { get; set; }

            [EmailAddress]
            [Display(Name = "Email (optional)")]
            public string Email { get; set; }   // ✔ optional email

            [Required]
            [Display(Name = "Diet Option")]
            public string DietOption { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "Password must be at least {2} characters.", MinimumLength = 5)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = Input.Username,
                    Email = Input.Email,      // ✔ optional
                    DietOption = Input.DietOption,
                    RegistrationDate = DateTime.UtcNow,
                    ProfilePictureUrl = "/images/default-profile.png"
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    // 🔥 SEND WELCOME EMAIL (only if email was entered)
                    if (!string.IsNullOrWhiteSpace(Input.Email))
                    {
                        try
                        {
                            await _emailSender.SendEmailAsync(
                                Input.Email,
                                "Welcome to CookCraft! 🎉",
                                $@"<h2>Welcome, {Input.Username}! 👨‍🍳</h2>
                                   <p>Your CookCraft account has been successfully created.</p>
                                   <p>Start adding recipes, track your achievements, and enjoy personalized cooking!</p>
                                   <p>Happy cooking! 🍽️</p>"
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("❌ Email sending failed: " + ex.Message);
                        }
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }
    }
}
