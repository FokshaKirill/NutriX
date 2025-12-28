using Domain.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly IRepository<Recipe> _recipes;

        public RecipeService(IRepository<Recipe> recipes)
        {
            _recipes = recipes;
        }

        public async Task<List<Recipe>> GetAllRecipesAsync()
        {
            return await _recipes.Query()
                .Include(r => r.Ingredients)
                .ThenInclude(i => i.Product)
                .Include(r => r.Steps)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<Recipe?> GetByIdAsync(Guid id)
        {
            return await _recipes.Query()
                .Include(r => r.Ingredients)
                .ThenInclude(i => i.Product)
                .Include(r => r.Steps)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Recipe> CreateAsync(Recipe recipe)
        {
            if (recipe.Id == Guid.Empty)
                recipe.Id = Guid.NewGuid();

            await _recipes.AddAsync(recipe);
            await _recipes.SaveChangesAsync();
            return recipe;
        }

        public async Task UpdateAsync(Recipe recipe)
        {
            await _recipes.UpdateAsync(recipe);
            await _recipes.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var recipe = await _recipes.GetByIdAsync(id);
            if (recipe != null)
            {
                await _recipes.DeleteAsync(recipe);
                await _recipes.SaveChangesAsync();
            }
        }
    }
}