using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Recipes.Models.ViewModels
{
    public class ShoppingListRequestViewModel
    {
        [Range(1, 20)]
        public int NumberOfPeople { get; set; }

        [Required]
        public List<string> AgeGroups { get; set; }

        [Range(1, 30)]
        public int DurationInDays { get; set; }

        public string DietPreference { get; set; }
    }
}
