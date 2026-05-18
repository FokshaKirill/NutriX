using Services.DTO;

namespace Services.Interfaces
{
    /// <summary>
    /// Сервис генерации плана питания.
    /// Принимает запрос с параметрами и возвращает готовый MealPlan.
    /// Вся математическая и алгоритмическая логика изолирована здесь,
    /// контроллер только собирает запрос и вызывает сервис.
    /// </summary>
    public interface IMealPlanGeneratorService
    {
        /// <summary>
        /// Генерирует план питания на неделю.
        /// </summary>
        /// <param name="request">Параметры генерации.</param>
        /// <returns>
        /// Готовый MealPlan с заполненными Meals.
        /// null если не удалось подобрать рецепты (слишком строгие фильтры).
        /// </returns>
        MealPlan? Generate(GenerateWeekRequest request);
    }
}