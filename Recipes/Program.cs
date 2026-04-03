using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Recipes.Data;
using Recipes.Models;
using Recipes.Services;
using Recipes.Services.Email;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ───────────────────────────────────────────────
// DATABASE CONFIGURATION
// ───────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ───────────────────────────────────────────────
// IDENTITY CONFIGURATION
// ───────────────────────────────────────────────
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = false;

    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequiredLength = 3;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// EMAIL SETTINGS
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings")
);

// EMAIL SENDER
builder.Services.AddSingleton<IEmailSender, EmailSender>();

// COOKIE SETTINGS
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ───────────────────────────────────────────────
// HTTP CLIENT
// ───────────────────────────────────────────────
builder.Services.AddHttpClient();

// ───────────────────────────────────────────────
// MVC + SERVICES
// ───────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<RewriteRecipeService>();
builder.Services.AddRazorPages();

var app = builder.Build();

// ───────────────────────────────────────────────
// AUTO CREATE SQLITE TABLES
// ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

// ───────────────────────────────────────────────
// ROLE + ADMIN SEEDING
// ───────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Ensure Admin Role
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
        Console.WriteLine("✅ Admin role created.");
    }

    // Create Default Admin User
    string adminEmail = "admin@example.com";
    string adminPassword = "Admin@123";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DietOption = "None",
            ProfilePictureUrl = "/uploads/default-profile.jpg"
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine($"✅ Admin user created: {adminEmail}");
        }
        else
        {
            Console.WriteLine($"⚠ Failed creating admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }
    }
    else
    {
        Console.WriteLine("ℹ Admin user already exists.");
    }
}

// ───────────────────────────────────────────────
// MIDDLEWARE PIPELINE
// ───────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Static /Data folder
var dataFolder = Path.Combine(Directory.GetCurrentDirectory(), "Data");
if (Directory.Exists(dataFolder))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(dataFolder),
        RequestPath = "/Data"
    });
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ───────────────────────────────────────────────
// ROUTES
// ───────────────────────────────────────────────
app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ───────────────────────────────────────────────
// API ENDPOINT FOR ALLERGEN PAGE
// ───────────────────────────────────────────────
app.MapGet("/api/recipes", async (ApplicationDbContext db) =>
{
    try
    {
        var dbRecipes = await db.Recipes
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Category,
                r.IngredientsJson,
                r.ImageUrl
            })
            .ToListAsync();

        if (dbRecipes != null && dbRecipes.Count > 0)
        {
            var enriched = dbRecipes.Select(r =>
            {
                var ingredients = JsonSerializer.Deserialize<List<string>>(r.IngredientsJson ?? "[]") ?? new List<string>();
                var lower = string.Join(" ", ingredients).ToLower();

                var allergens = new List<string>();
                if (lower.Contains("milk") || lower.Contains("cheese") || lower.Contains("butter")) allergens.Add("Milk");
                if (lower.Contains("egg")) allergens.Add("Eggs");
                if (lower.Contains("wheat") || lower.Contains("flour")) allergens.Add("Wheat");
                if (lower.Contains("peanut")) allergens.Add("Peanuts");
                if (lower.Contains("almond") || lower.Contains("cashew") || lower.Contains("nut")) allergens.Add("Tree Nuts");
                if (lower.Contains("fish") || lower.Contains("salmon") || lower.Contains("tuna")) allergens.Add("Fish");
                if (lower.Contains("shrimp") || lower.Contains("crab") || lower.Contains("lobster")) allergens.Add("Shellfish");
                if (lower.Contains("soy")) allergens.Add("Soybeans");
                if (lower.Contains("sesame")) allergens.Add("Sesame");
                if (lower.Contains("mustard")) allergens.Add("Mustard");
                if (lower.Contains("celery")) allergens.Add("Celery");
                if (lower.Contains("barley") || lower.Contains("rye")) allergens.Add("Barley (Gluten)");

                return new
                {
                    r.Id,
                    r.Title,
                    r.Category,
                    r.ImageUrl,
                    Allergens = allergens.Distinct().ToList()
                };
            }).ToList();

            return Results.Json(enriched);
        }

        // fallback
        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "recipes.json");
        if (File.Exists(jsonPath))
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            var fileRecipes = JsonSerializer.Deserialize<List<object>>(json);
            return Results.Json(fileRecipes ?? new List<object>());
        }

        return Results.Json(new List<object>());
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Error loading recipes: " + ex.Message);
        return Results.Problem("Failed to load recipes.");
    }
});

app.Run();
