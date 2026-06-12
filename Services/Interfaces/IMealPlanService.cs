using Domain.Entities;

namespace Services.Interfaces
{
    public interface IMealPlanService
    {
        /// <summary>
        /// Получить текущий план на эту неделю (по StartDate = понедельник текущей недели)
        /// </summary>
        Task<MealPlan?> GetCurrentWeekPlanAsync(Guid userId);

        /// <summary>
        /// Получить план по ID
        /// </summary>
        Task<MealPlan?> GetByIdAsync(Guid id);

        /// <summary>
        /// Получить все планы (для админки или истории)
        /// </summary>
        Task<List<MealPlan>> GetAllPlansAsync();

        /// <summary>
        /// Создать новый план
        /// </summary>
        Task<MealPlan> CreateAsync(MealPlan plan);

        /// <summary>
        /// Обновить существующий план
        /// </summary>
        Task UpdateAsync(MealPlan plan);

        /// <summary>
        /// Удалить план
        /// </summary>
        Task DeleteAsync(Guid id);

        /// <summary>
        /// Сгенерировать список покупок по плану (с суммированием количества)
        /// </summary>
        Task<List<RecipeIngredient>> GenerateShoppingListAsync(Guid mealPlanId);

        /// <summary>
        /// Получить сегодняшний план
        /// </summary>
        Task<List<PlannedMeal>> GetTodayMealsAsync(Guid userId);

        /// <summary>
        /// Получить запланнированный рецепт по ID
        /// </summary>
        Task<PlannedMeal?> GetPlannedMealByIdAsync(Guid id);

        /// <summary>
        /// Сменить запланнированный рецепт
        /// </summary>
        Task ReplaceMealRecipeAsync(Guid plannedMealId, Guid newRecipeId);

        /// <summary>
        /// Добавляет новое блюдо в существующий план (ручное добавление)
        /// </summary>
        Task AddPlannedMealAsync(PlannedMeal meal);
        
        /// <summary>
        /// Добавляет новый слот для блюда в существующий план (ручное добавление)
        /// </summary>
        Task AddSlotAsync(MealSlot slot);
        
        Task UpdateServingsAsync(Guid plannedMealId, int servings);
        Task RemoveMealAsync(Guid plannedMealId);
        Task<List<MealPlan>> GetHistoryAsync(Guid userId);
    }
}