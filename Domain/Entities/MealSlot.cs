namespace Domain.Entities
{
    /// <summary>
    /// Контейнер одного приёма пищи внутри дня планa.
    /// Содержит несколько блюд (<see cref="PlannedMeal"/>) по ролям.
    /// </summary>
    public class MealSlot
    {
        public Guid Id { get; set; }

        public Guid MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        /// <summary>Смещение дня от StartDate плана (0 = понедельник … 6 = воскресенье).</summary>
        public int DayOffset { get; set; }

        public Guid MealTypeId { get; set; }
        public MealType MealType { get; set; } = null!;

        /// <summary>Блюда этого приёма пищи (по одному на каждую роль).</summary>
        public ICollection<PlannedMeal> Items { get; set; } = [];

        // ── Агрегированные значения ───────────────────────────────────────────

        /// <summary>Суммарная калорийность всех блюд слота с учётом порций.</summary>
        public decimal TotalCalories =>
            Items.Sum(m => m.Recipe != null ? m.Recipe.CaloriesPerServing * m.Servings : 0);

        /// <summary>Суммарная стоимость всех блюд слота с учётом порций.</summary>
        public decimal TotalCost =>
            Items.Sum(m => m.Recipe != null ? m.Recipe.TotalCost * m.Servings : 0);
    }
}