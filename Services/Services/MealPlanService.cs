using Domain.Entities;
using Infrastructure;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Services
{
    public class MealPlanService : IMealPlanService
    {
        private readonly IRepository<MealPlan> _plans;
        private readonly IRepository<PlannedMeal> _plannedMeals;
        private readonly IRepository<RecipeIngredient> _ingredients;

        public MealPlanService(
            IRepository<MealPlan> plans,
            IRepository<PlannedMeal> plannedMeals,
            IRepository<RecipeIngredient> ingredients)
        {
            _plans = plans;
            _plannedMeals = plannedMeals;
            _ingredients = ingredients;
        }

        public Task<MealPlan?> GetByIdAsync(int id)
            => _plans.Query()
                .Include(p => p.Meals)
                .ThenInclude(pm => pm.Recipe)
                .FirstOrDefaultAsync(p => p.Id == id);

        public Task<List<MealPlan>> GetUserPlansAsync(int userId)
            => _plans.Query()
                     .Where(p => p.UserId == userId)
                     .ToListAsync();

        public async Task<MealPlan> CreateAsync(MealPlan plan)
        {
            await _plans.AddAsync(plan);
            await _plans.SaveChangesAsync();
            return plan;
        }

        public async Task UpdateAsync(MealPlan plan)
        {
            await _plans.UpdateAsync(plan);
            await _plans.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var plan = await _plans.GetByIdAsync(id);
            if (plan == null) return;

            await _plans.DeleteAsync(plan);
            await _plans.SaveChangesAsync();
        }

        public async Task<List<Product>> GenerateShoppingListAsync(int mealPlanId)
        {
            var meals = await _plannedMeals.Query()
                .Where(pm => pm.MealPlanId == mealPlanId)
                .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
                .ThenInclude(i => i.Product)
                .ToListAsync();

            var result = new List<Product>();
            // тут просто заглушка — расширишь под свои нужды
            return result;
        }
    }
}
