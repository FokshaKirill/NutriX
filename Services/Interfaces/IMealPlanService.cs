using Domain.Entities;

namespace Services.Interfaces
{
    public interface IMealPlanService
    {
        /// <summary>
        /// Получить текущий план на эту неделю (по StartDate = понедельник текущей недели)
        /// </summary>
        Task<MealPlan?> GetCurrentWeekPlanAsync();

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
    }
}