using Domain.Entities;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class MealPlanService : IMealPlanService
    {
        private readonly IRepository<MealPlan>    _plans;
        private readonly IRepository<PlannedMeal> _plannedMeals;
        private readonly IRepository<MealSlot>    _slots;

        public MealPlanService(
            IRepository<MealPlan>    plans,
            IRepository<PlannedMeal> plannedMeals,
            IRepository<MealSlot>    slots)
        {
            _plans        = plans;
            _plannedMeals = plannedMeals;
            _slots        = slots;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Чтение планов
        // ══════════════════════════════════════════════════════════════════════

        public async Task<MealPlan?> GetCurrentWeekPlanAsync(Guid userId)
        {
            var today  = DateTime.Today;
            var diff   = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff).Date;

            return await _plans.Query()
                .Include(p => p.Slots).ThenInclude(s => s.MealType)
                .Include(p => p.Slots).ThenInclude(s => s.Items)
                    .ThenInclude(m => m.Recipe)
                    .ThenInclude(r => r.Ingredients)
                    .ThenInclude(i => i.Product)
                // ── УДАЛЕНО: .ThenInclude(m => m.MealSlot) ──────────────────
                // EF Core автоматически заполняет PlannedMeal.MealSlot через fix-up
                // при загрузке Slots → Items. Явный Include вызывал краш:
                // NavigationBaseIncludeIgnored: 'PlannedMeal.MealSlot' was ignored.
                .Where(p => p.UserId    == userId
                         && p.StartDate >= monday
                         && p.StartDate <  monday.AddDays(7))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<PlannedMeal>> GetTodayMealsAsync(Guid userId)
        {
            var today  = DateTime.Today;
            var diff   = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff).Date;
            var offset = (today - monday).Days;

            var plan = await GetCurrentWeekPlanAsync(userId);
            if (plan == null) return [];

            return plan.Slots
                .Where(s => s.DayOffset == offset)
                .SelectMany(s => s.Items)
                .ToList();
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
                .Include(p => p.Slots).ThenInclude(s => s.MealType)
                .Include(p => p.Slots).ThenInclude(s => s.Items)
                    .ThenInclude(m => m.Recipe)
                    .ThenInclude(r => r.Ingredients)
                    .ThenInclude(i => i.Product)
                // MealSlot заполняется EF автоматически — явный Include не нужен
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Создание / обновление / удаление плана
        // ══════════════════════════════════════════════════════════════════════

        public async Task<MealPlan> CreateAsync(MealPlan plan)
        {
            var existing = await _plans.Query()
                .Include(p => p.Slots)
                .FirstOrDefaultAsync(p =>
                    p.UserId         == plan.UserId &&
                    p.StartDate.Date == plan.StartDate.Date);

            if (existing != null)
            {
                await _plans.DeleteAsync(existing);
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

        // ══════════════════════════════════════════════════════════════════════
        // Операции с MealSlot
        // ══════════════════════════════════════════════════════════════════════

        public async Task AddSlotAsync(MealSlot slot)
        {
            if (slot.Id == Guid.Empty) slot.Id = Guid.NewGuid();
            await _slots.AddAsync(slot);
            await _slots.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Операции с PlannedMeal
        // ══════════════════════════════════════════════════════════════════════

        public async Task<PlannedMeal?> GetPlannedMealByIdAsync(Guid id)
        {
            return await _plannedMeals.Query()
                .Include(m => m.Recipe)
                    .ThenInclude(r => r.Ingredients)
                    .ThenInclude(i => i.Product)
                .Include(m => m.MealSlot)
                    .ThenInclude(s => s.MealType)
                .Include(m => m.MealSlot)
                    .ThenInclude(s => s.MealPlan)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task AddPlannedMealAsync(PlannedMeal meal)
        {
            if (meal.Id == Guid.Empty)
                meal.Id = Guid.NewGuid();

            await _plannedMeals.AddAsync(meal);
            await _plannedMeals.SaveChangesAsync();
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

        // ══════════════════════════════════════════════════════════════════════
        // Список покупок
        // ══════════════════════════════════════════════════════════════════════

        public async Task<List<RecipeIngredient>> GenerateShoppingListAsync(Guid mealPlanId)
        {
            var plan = await _plans.Query()
                .Include(p => p.Slots)
                    .ThenInclude(s => s.Items)
                    .ThenInclude(m => m.Recipe)
                    .ThenInclude(r => r.Ingredients)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(p => p.Id == mealPlanId);

            if (plan == null) return [];

            var allMeals = plan.Slots
                .SelectMany(s => s.Items)
                .Where(m => m.Recipe != null);

            return allMeals
                .SelectMany(pm => pm.Recipe!.Ingredients.Select(ing => new { ing, pm.Servings }))
                .GroupBy(x => new { x.ing.ProductId, x.ing.Unit })
                .Select(g => new RecipeIngredient
                {
                    ProductId = g.Key.ProductId,
                    Product   = g.First().ing.Product,
                    Amount    = g.Sum(x => x.ing.Amount * x.Servings),
                    Unit      = g.Key.Unit,
                    Comment   = string.Join("; ", g
                        .Where(x => !string.IsNullOrWhiteSpace(x.ing.Comment))
                        .Select(x => x.ing.Comment!).Distinct())
                })
                .OrderBy(i => i.Product?.Name)
                .ToList();
        }

        public async Task UpdateServingsAsync(Guid plannedMealId, int servings)
        {
            var meal = await _plannedMeals.Query()
                .FirstOrDefaultAsync(m => m.Id == plannedMealId);
            if (meal == null) return;
            meal.Servings = Math.Max(1, servings);
            await _plannedMeals.UpdateAsync(meal);
            await _plannedMeals.SaveChangesAsync();
        }

        public async Task RemoveMealAsync(Guid plannedMealId)
        {
            var meal = await _plannedMeals.Query()
                .FirstOrDefaultAsync(m => m.Id == plannedMealId);
            if (meal == null) return;
            await _plannedMeals.DeleteAsync(meal);
            await _plannedMeals.SaveChangesAsync();
        }

        public async Task<List<MealPlan>> GetHistoryAsync(Guid userId)
        {
            return await _plans.Query()
                .Where(p => p.UserId == userId && p.IsArchived)
                .OrderByDescending(p => p.StartDate)
                .Include(p => p.Slots)
                .ThenInclude(s => s.Items)
                .ThenInclude(i => i.Recipe)
                .ThenInclude(r => r.Ingredients)
                .ThenInclude(ri => ri.Product)
                .ToListAsync();
        }

        public async Task DeleteAllForUserAsync(Guid userId)
        {
            var plans = await _plans.Query()
                .Where(p => p.UserId == userId && p.IsArchived)
                .ToListAsync();

            foreach (var plan in plans)
                await _plans.DeleteAsync(plan);

            await _plans.SaveChangesAsync();
        }
    }
}