using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Recipes.Models;
using System.Text.Json;

namespace Recipes.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Recipe> Recipes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ⭐ Make Email optional (required for optional email signup)
            builder.Entity<ApplicationUser>()
                .Property(u => u.Email)
                .IsRequired(false);

            builder.Entity<ApplicationUser>()
                .Property(u => u.NormalizedEmail)
                .IsRequired(false);

            // ---------------------------------------
            // DO NOT MODIFY BELOW — YOUR ORIGINAL LOGIC
            // ---------------------------------------

            // ✅ Value comparer for List<int>
            var intListComparer = new ValueComparer<List<int>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<int>() : c.ToList()
            );

            // ✅ Value comparer for List<string>
            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, (v ?? string.Empty).GetHashCode())),
                c => c == null ? new List<string>() : c.ToList()
            );

            // ✅ Convert + compare FavoriteRecipeIds <-> JSON
            builder.Entity<ApplicationUser>()
                .Property(u => u.FavoriteRecipeIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new List<int>()
                )
                .Metadata.SetValueComparer(intListComparer);

            // ✅ Convert + compare GroceryItems <-> JSON
            builder.Entity<ApplicationUser>()
                .Property(u => u.GroceryItems)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                )
                .Metadata.SetValueComparer(stringListComparer);

            // ✅ Default values for new users
            builder.Entity<ApplicationUser>()
                .Property(u => u.ProfilePictureUrl)
                .HasDefaultValue("/uploads/default-profile.jpg");

            builder.Entity<ApplicationUser>()
                .Property(u => u.DietOption)
                .HasDefaultValue("None");
        }
    }
}
