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

        public async Task<MealPlan?> GetCurrentWeekPlanAsync(Guid userId)
        {
            var today = DateTime.Today;
            var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff).Date;

            return await _plans.Query()
                .Include(p => p.Meals)
                .ThenInclude(m => m.MealType)
                .Include(p => p.Meals)
                .ThenInclude(m => m.Recipe)
                .ThenInclude(r => r.Ingredients)
                .ThenInclude(i => i.Product)
                .Where(p => p.UserId == userId 
                            && p.StartDate >= monday 
                            && p.StartDate < monday.AddDays(7))  
                .OrderByDescending(p => p.StartDate)     
                .FirstOrDefaultAsync();
        }
        
        public async Task<List<PlannedMeal>> GetTodayMealsAsync(Guid userId)
        {
            var today  = DateTime.Today;
            var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff).Date;
            var offset = (today - monday).Days;

            return await _plans.Query()
                .Where(p =>
                    p.UserId == userId &&
                    p.StartDate >= monday &&
                    p.StartDate < monday.AddDays(1))
                .SelectMany(p => p.Meals)
                .Where(m => m.DayOffset == offset)
                .Include(m => m.MealType)
                .Include(m => m.Recipe)
                .ToListAsync();
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
            var existingPlan = await _plans.Query()
                .Include(p => p.Meals)
                .FirstOrDefaultAsync(p =>
                    p.UserId == plan.UserId &&
                    p.StartDate.Date == plan.StartDate.Date);

            if (existingPlan != null)
            {
                await _plans.DeleteAsync(existingPlan);
                await _plans.SaveChangesAsync();
            }

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
        
        public async Task<PlannedMeal?> GetPlannedMealByIdAsync(Guid id)
        {
            return await _plannedMeals.Query()
                .Include(m => m.Recipe)
                .Include(m => m.MealType)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task ReplaceMealRecipeAsync(Guid plannedMealId, Guid newRecipeId)
        {
            var meal = await _plannedMeals.Query()
                .FirstOrDefaultAsync(m => m.Id == plannedMealId);
            if (meal == null) return;

            meal.RecipeId = newRecipeId;
            await _plannedMeals.UpdateAsync(meal);
            await _plannedMeals.SaveChangesAsync();
        }
    }
}