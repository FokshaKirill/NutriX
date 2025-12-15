using Infrastructure.Interfaces;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly IRepository<Recipe> _recipes;

        public RecipeService(IRepository<Recipe> recipes)
        {
            _recipes = recipes;
        }

        public Task<Recipe?> GetByIdAsync(int id)
            => _recipes.Query()
               .Include(r => r.Ingredients)
               .Include(r => r.Steps)
               .FirstOrDefaultAsync(r => r.Id == id);

        public Task<List<Recipe>> GetUserRecipesAsync(int userId)
            => _recipes.Query()
                .Where(r => r.UserId == userId)
                .ToListAsync();

        public async Task<Recipe> CreateAsync(Recipe recipe)
        {
            await _recipes.AddAsync(recipe);
            await _recipes.SaveChangesAsync();
            return recipe;
        }

        public async Task UpdateAsync(Recipe recipe)
        {
            await _recipes.UpdateAsync(recipe);
            await _recipes.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var recipe = await _recipes.GetByIdAsync(id);
            if (recipe == null) return;

            await _recipes.DeleteAsync(recipe);
            await _recipes.SaveChangesAsync();
        }
    }

}
