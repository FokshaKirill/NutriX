namespace Domain.Entities
{
    public class MealPlan
    {
        public Guid Id { get; set; }
        public string? Name { get; set; } = null!;
        public DateTime StartDate { get; set; } // Понедельник недели

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public ICollection<PlannedMeal> Meals { get; set; } = [];

        // ── Настройки генерации ───────────────────────────────────────────
        // Сохраняются при генерации чтобы ReplaceMeal использовал те же
        // ограничения что были выбраны пользователем при создании плана.

        public bool ConsiderBudget   { get; set; } = false;
        public decimal WeeklyBudget  { get; set; } = 3500m;

        public bool Vegetarian  { get; set; } = false;
        public bool Vegan       { get; set; } = false;
        public bool GlutenFree  { get; set; } = false;
        public bool LowCarb     { get; set; } = false;
        public bool HighProtein { get; set; } = false;
        public bool LowFat      { get; set; } = false;

        /// <summary>
        /// Исключённые продукты через запятую: "молоко, орехи, рыба".
        /// Хранится как строка — парсится в HashSet при использовании.
        /// </summary>
        public string? ExcludedProducts { get; set; }
    }
}