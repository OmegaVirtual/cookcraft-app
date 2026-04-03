using Microsoft.AspNetCore.Mvc.ModelBinding;   // for [ValidateNever]
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Recipes.Models;
using System;
using System.Collections.Generic;

namespace Recipes.Models.ViewModels
{
    public class IngredientPickerViewModel
    {
        // Don’t validate or bind this from the request—always set it in code.
        [ValidateNever]
        public List<string> AllIngredients { get; set; } = new List<string>();

        // Bound from checkboxes (can stay non-nullable because we default it to Array.Empty<string>())
        public string[] SelectedIngredients { get; set; } = Array.Empty<string>();

        // Don’t validate or bind this from the request—always set it in code.
        [ValidateNever]
        public List<Recipe> MatchingRecipes { get; set; } = new List<Recipe>();
    }
}
