using Domain.Entities;
using Domain.Enums;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly IRepository<Recipe>           _recipes;
        private readonly IRepository<RecipeIngredient> _ingredients;
        private readonly IRepository<RecipeStep>       _steps;

        public RecipeService(
            IRepository<Recipe> recipes,
            IRepository<RecipeIngredient> ingredients,
            IRepository<RecipeStep> steps)
        {
            _recipes     = recipes;
            _ingredients = ingredients;
            _steps       = steps;
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

        public async Task<Recipe?> GetByIdAsync(Guid id, bool includeIngredients = true, bool includeSteps = true)
        {
            var query = _recipes.Query().AsNoTracking();

            if (includeIngredients)
                query = query.Include(r => r.Ingredients)
                    .ThenInclude(i => i.Product);   // ← Важно!

            if (includeSteps)
                query = query.Include(r => r.Steps);

            return await query.FirstOrDefaultAsync(r => r.Id == id);
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
            // 1. Сохраняем новые коллекции до очистки
            var newIngredients = recipe.Ingredients.ToList();
            var newSteps       = recipe.Steps.ToList();

            // 2. Удаляем старые из БД
            var oldIngredients = await _ingredients.Query()
                .Where(i => i.RecipeId == recipe.Id).ToListAsync();
            _ingredients.RemoveRange(oldIngredients);

            var oldSteps = await _steps.Query()
                .Where(s => s.RecipeId == recipe.Id).ToListAsync();
            _steps.RemoveRange(oldSteps);

            // 3. Очищаем коллекции на объекте, чтобы Update не трогал их
            recipe.Ingredients.Clear();
            recipe.Steps.Clear();

            // 4. Обновляем скалярные поля рецепта
            await _recipes.UpdateAsync(recipe);

            // 5. Добавляем новые дочерние записи
            await _ingredients.AddRangeAsync(newIngredients);
            await _steps.AddRangeAsync(newSteps);

            // 6. Один SaveChanges на всё
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
        
        public async Task<IEnumerable<Recipe>> GetByAuthorAsync(Guid authorId)
        {
            return await _recipes.Query()
                .Include(r => r.Ingredients)
                .ThenInclude(i => i.Product)
                .Include(r => r.Steps)
                .Where(r => r.AuthorId == authorId)
                .OrderBy(r => r.Name)
                .ToListAsync();
        }
        
        public async Task<(List<Recipe> Recipes, int TotalCount)> GetPagedRecipesAsync(
            int page, 
            int pageSize, 
            string? searchTerm = null,
            RecipeCategory? category = null)
        {
            IQueryable<Recipe> query = _recipes.Query()
                .Include(r => r.Ingredients)
                .ThenInclude(i => i.Product);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(r => r.Name.ToLower().Contains(term) ||
                                         (r.Description != null && r.Description.ToLower().Contains(term)));
            }

            if (category.HasValue && category.Value != RecipeCategory.Other)
            {
                query = query.Where(r => r.Category == category.Value);
            }

            var totalCount = await query.CountAsync();

            var recipes = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (recipes, totalCount);
        }
    }
}