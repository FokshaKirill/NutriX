using Domain.Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services
{
    public class MealService : IMealService
    {
        private readonly DatabaseContext _db;

        public MealService(DatabaseContext db)
        {
            _db = db;
        }

        public async Task<List<PlannedMeal>> GetMealsByDateAsync(DateTime date, int userId)
        {
            var monday = date.AddDays(-(int)date.DayOfWeek + 1); // понедельник недели
            var offset = (int)(date - monday).TotalDays;

            return await _db.PlannedMeals
                .Include(x => x.Recipe)
                    .ThenInclude(r => r.Ingredients)
                        .ThenInclude(i => i.Product)
                .Include(x => x.MealType)
                .Where(x =>
                    x.MealPlan.UserId == userId &&
                    x.MealPlan.StartDate == monday &&
                    x.DayOffset == offset)
                .ToListAsync();
        }

        public async Task<int> GetTotalCaloriesAsync(DateTime date, int userId)
        {
            var meals = await GetMealsByDateAsync(date, userId);
            return meals.Sum(m => CalcCalories(m));
        }

        public async Task<int> GetTotalProteinAsync(DateTime date, int userId)
        {
            var meals = await GetMealsByDateAsync(date, userId);
            return meals.Sum(m => CalcProtein(m));
        }

        public async Task<int> GetTotalFatAsync(DateTime date, int userId)
        {
            var meals = await GetMealsByDateAsync(date, userId);
            return meals.Sum(m => CalcFat(m));
        }

        public async Task<int> GetTotalCarbsAsync(DateTime date, int userId)
        {
            var meals = await GetMealsByDateAsync(date, userId);
            return meals.Sum(m => CalcCarbs(m));
        }

        // ---------------------------
        //     CALCULATORS
        // ---------------------------
        private int CalcCalories(PlannedMeal meal)
        {
            if (meal.Recipe == null) return 0;

            decimal total = 0;

            foreach (var ing in meal.Recipe.Ingredients)
            {
                if (ing.Product.CaloriesPer100 is null) continue;

                total += (ing.Product.CaloriesPer100.Value * ing.Amount / 100m);
            }

            return (int)(total * meal.Servings / meal.Recipe.DefaultServings);
        }

        private int CalcProtein(PlannedMeal meal)
        {
            if (meal.Recipe == null) return 0;

            decimal total = 0;

            foreach (var ing in meal.Recipe.Ingredients)
            {
                if (ing.Product.ProteinPer100 is null) continue;

                total += (ing.Product.ProteinPer100.Value * ing.Amount / 100m);
            }

            return (int)(total * meal.Servings / meal.Recipe.DefaultServings);
        }

        private int CalcFat(PlannedMeal meal)
        {
            if (meal.Recipe == null) return 0;

            decimal total = 0;

            foreach (var ing in meal.Recipe.Ingredients)
            {
                if (ing.Product.FatPer100 is null) continue;

                total += (ing.Product.FatPer100.Value * ing.Amount / 100m);
            }

            return (int)(total * meal.Servings / meal.Recipe.DefaultServings);
        }

        private int CalcCarbs(PlannedMeal meal)
        {
            if (meal.Recipe == null) return 0;

            decimal total = 0;

            foreach (var ing in meal.Recipe.Ingredients)
            {
                if (ing.Product.CarbsPer100 is null) continue;

                total += (ing.Product.CarbsPer100.Value * ing.Amount / 100m);
            }

            return (int)(total * meal.Servings / meal.Recipe.DefaultServings);
        }
    }
}
