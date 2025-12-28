using Domain.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class MealPlanService : IMealPlanService
    {
        private readonly IRepository<MealPlan> _plans;
        private readonly IRepository<PlannedMeal> _plannedMeals;

        public MealPlanService(
            IRepository<MealPlan> plans,
            IRepository<PlannedMeal> plannedMeals)
        {
            _plans = plans;
            _plannedMeals = plannedMeals;
        }

        public async Task<MealPlan?> GetCurrentWeekPlanAsync()
        {
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            return await _plans.Query()
                .Include(p => p.Meals)
                    .ThenInclude(pm => pm.Recipe)
                        .ThenInclude(r => r.Ingredients)
                            .ThenInclude(i => i.Product)
                .Include(p => p.Meals)
                    .ThenInclude(pm => pm.MealType)
                .FirstOrDefaultAsync(p => p.StartDate == monday);
        }

        public async Task<List<MealPlan>> GetAllPlansAsync()
        {
            return await _plans.Query()
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
        }

        public async Task<MealPlan?> GetByIdAsync(Guid id)
        {
            return await _plans.Query()
                .Include(p => p.Meals)
                    .ThenInclude(pm => pm.Recipe)
                        .ThenInclude(r => r.Ingredients)
                            .ThenInclude(i => i.Product)
                .Include(p => p.Meals)
                    .ThenInclude(pm => pm.MealType)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<MealPlan> CreateAsync(MealPlan plan)
        {
            if (plan.Id == Guid.Empty)
                plan.Id = Guid.NewGuid();

            await _plans.AddAsync(plan);
            await _plans.SaveChangesAsync();
            return plan;
        }

        public async Task UpdateAsync(MealPlan plan)
        {
            await _plans.UpdateAsync(plan);
            await _plans.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var plan = await _plans.GetByIdAsync(id);
            if (plan != null)
            {
                await _plans.DeleteAsync(plan);
                await _plans.SaveChangesAsync();
            }
        }

        public async Task<List<RecipeIngredient>> GenerateShoppingListAsync(Guid mealPlanId)
        {
            var plannedMeals = await _plannedMeals.Query()
                .Where(pm => pm.MealPlanId == mealPlanId)
                .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
                .ThenInclude(i => i.Product)
                .ToListAsync();

            if (!plannedMeals.Any())
                return new List<RecipeIngredient>();

            // Собираем все ингредиенты с учётом порций (DefaultServings) каждого PlannedMeal
            var allIngredientsWithServings = plannedMeals
                .Where(pm => pm.Recipe != null)
                .SelectMany(pm => pm.Recipe!.Ingredients.Select(ingredient => new
                {
                    Ingredient = ingredient,
                    ServingsMultiplier = pm.Servings  // вот здесь берём DefaultServings конкретного блюда в плане
                }))
                .ToList();

            // Группируем по продукту и единице измерения
            var grouped = allIngredientsWithServings
                .GroupBy(x => new { x.Ingredient.ProductId, x.Ingredient.Unit })
                .Select(g => new RecipeIngredient
                {
                    ProductId = g.Key.ProductId,
                    Product = g.First().Ingredient.Product,
                    // Суммируем: базовое количество в рецепте * DefaultServings в плане
                    Amount = g.Sum(x => x.Ingredient.Amount * x.ServingsMultiplier),
                    Unit = g.Key.Unit,
                    Comment = string.Join("; ", g
                        .Where(x => !string.IsNullOrWhiteSpace(x.Ingredient.Comment))
                        .Select(x => x.Ingredient.Comment)
                        .Distinct())
                })
                .OrderBy(i => i.Product?.Name)
                .ToList();

            return grouped;
        }
    }
}