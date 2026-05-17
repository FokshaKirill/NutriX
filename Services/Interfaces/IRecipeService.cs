using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IRecipeService
    {
        Task<List<Recipe>> GetAllRecipesAsync();
        Task<Recipe?> GetByIdAsync(Guid id, bool includeIngredients = false, bool includeSteps = false);
        Task<Recipe> CreateAsync(Recipe recipe);
        Task UpdateAsync(Recipe recipe);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<Recipe>> GetByAuthorAsync(Guid authorId);
    }
}
