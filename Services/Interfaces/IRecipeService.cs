using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using Domain.Enums;

namespace Services.Interfaces
{
    public interface IRecipeService
    {
        Task<List<Recipe>> GetAllRecipesAsync();
        Task<Recipe> CreateAsync(Recipe recipe);
        Task UpdateAsync(Recipe recipe);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<Recipe>> GetByAuthorAsync(Guid authorId);
        Task UpdateTagsAsync(Guid id, RecipeTag tags);

        Task<(List<Recipe> Recipes, int TotalCount)> GetPagedRecipesAsync(
            int page,
            int pageSize,
            string? searchTerm = null,
            RecipeTag? tag = null);
        
        Task<Recipe?> GetByIdAsync(
            Guid id, 
            bool includeIngredients = false, 
            bool includeSteps = false);
    }
}
