using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IRecipeService
    {
        Task<Recipe?> GetByIdAsync(int id);
        Task<List<Recipe>> GetUserRecipesAsync(int userId);
        Task<Recipe> CreateAsync(Recipe recipe);
        Task UpdateAsync(Recipe recipe);
        Task DeleteAsync(int id);
    }

}
