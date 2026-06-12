using Domain.Entities;
using Domain.Enums;

namespace Services.DTO
{
    /// <summary>
    /// Запрос на генерацию плана питания на неделю.
    /// Передаётся в <see cref="IMealPlanGeneratorService.Generate"/>.
    /// </summary>
    public class GenerateWeekRequest
    {
        // ── Пользователь ──────────────────────────────────────────────────────

        public Guid UserId { get; set; }

        // ── Калории и цели ────────────────────────────────────────────────────

        public int DailyCalories { get; set; } = 2000;

        public NutritionGoal Goal { get; set; } = NutritionGoal.Maintain;
        public bool IncludeDrinks { get; set; } = true;
        
        /// <summary>
        /// Скорректированная дневная норма калорий с учётом цели.
        /// Deficit −15%, Surplus +15%, Maintain — без изменений.
        /// </summary>
        public int AdjustedDailyCalories => Goal switch
        {
            NutritionGoal.Deficit => (int)(DailyCalories * 0.85),
            NutritionGoal.Surplus => (int)(DailyCalories * 1.15),
            _                     => DailyCalories
        };

        // ── Целевые макросы (опциональные) ───────────────────────────────────

        public int?  ProteinGoal { get; set; }
        public int?  FatGoal     { get; set; }
        public int?  CarbsGoal   { get; set; }

        // ── Распределение калорий по приёмам пищи ────────────────────────────

        public int BreakfastTarget => (int)(AdjustedDailyCalories * 0.25);
        public int LunchTarget     => (int)(AdjustedDailyCalories * 0.40);
        public int DinnerTarget    => (int)(AdjustedDailyCalories * 0.30);
        public int SnackTarget     => (int)(AdjustedDailyCalories * 0.05);

        // ── Бюджет ────────────────────────────────────────────────────────────

        public bool    ConsiderBudget { get; set; } = false;
        public decimal WeeklyBudget   { get; set; } = 3500m;
        public decimal DailyBudget    => WeeklyBudget / 7m;

        /// <summary>Строгость соблюдения бюджета.</summary>
        public BudgetStrictness BudgetMode { get; set; } = BudgetStrictness.Flexible;

        // ── Диетические предпочтения ──────────────────────────────────────────

        public bool Vegetarian  { get; set; } = false;
        public bool Vegan       { get; set; } = false;
        public bool GlutenFree  { get; set; } = false;
        public bool LowCarb     { get; set; } = false;
        public bool HighProtein { get; set; } = false;
        public bool LowFat      { get; set; } = false;

        /// <summary>
        /// Исключённые продукты (нижний регистр, нормализованные).
        /// Пример: { "молоко", "орехи", "рыба" }
        /// </summary>
        public HashSet<string> ExcludedProducts { get; set; } = [];

        // ── Структура приёмов пищи ────────────────────────────────────────────

        public int  MealsPerDay   { get; set; } = 3;

        /// <summary>
        /// Количество дней между повторами одного рецепта.
        /// 0 = повторы разрешены.
        /// </summary>
        public int MinDaysBetweenRepeats { get; set; } = 3;

        /// <summary>Уровень разнообразия — определяет кулдаун повторений.</summary>
        public DiversityLevel Diversity { get; set; } = DiversityLevel.Normal;

        /// <summary>
        /// Пользовательские шаблоны слотов.
        /// Если null — генератор использует <see cref="DefaultSlotTemplates"/>.
        /// </summary>
        public List<SlotTemplate>? CustomSlotTemplates { get; set; }

        // ── Типы приёмов пищи из БД ───────────────────────────────────────────

        public MealType  BreakfastType { get; set; } = null!;
        public MealType  LunchType     { get; set; } = null!;
        public MealType  DinnerType    { get; set; } = null!;
        public MealType? SnackType     { get; set; }

        // ── Источник рецептов ─────────────────────────────────────────────────

        public List<Recipe> AllRecipes { get; set; } = [];

        // ── Сид для воспроизводимости (тесты) ────────────────────────────────

        public int? Seed { get; set; }

        // ── Вспомогательный метод ─────────────────────────────────────────────

        /// <summary>
        /// Парсит строку исключений "молоко, орехи, рыба" в HashSet.
        /// Используется контроллером при построении запроса.
        /// </summary>
        public static HashSet<string> ParseExcluded(string? raw) =>
            string.IsNullOrWhiteSpace(raw)
                ? []
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim().ToLowerInvariant())
                     .ToHashSet();
    }
}