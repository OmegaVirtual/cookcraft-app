using System.Threading.Tasks;
using Recipes.Models;

namespace Recipes.Services
{
    public interface IRecipeImportService
    {
        /// <summary>
        /// Given a recipe page URL, fetches the HTML, looks for JSON-LD markup with "@type": "Recipe",
        /// and returns a mapped Recipe object (or null if parsing fails).
        /// </summary>
        Task<Recipe> ImportFromUrlAsync(string sourceUrl);
    }
}
