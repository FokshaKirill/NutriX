namespace Services.DTO
{
    /// <summary>
    /// Входные данные для генерации меню на неделю.
    /// Передаётся из контроллера в MealPlanGeneratorService.
    /// </summary>
    public class GenerateWeekRequest
    {
        // ── Пользователь ──────────────────────────────────────────────────────
        public Guid UserId { get; init; }

        // ── Калории и цель ────────────────────────────────────────────────────

        /// <summary>
        /// Базовые калории в день (до поправки на цель).
        /// </summary>
        public int DailyCalories { get; init; } = 2000;

        /// <summary>
        /// Цель: "maintain" | "lose" | "lose_fast" | "gain" | "gain_lean"
        /// Применяется как поправочный коэффициент к DailyCalories.
        /// </summary>
        public string Goal { get; init; } = "maintain";

        // ── Бюджет ────────────────────────────────────────────────────────────
        public bool    ConsiderBudget { get; init; } = false;
        public decimal WeeklyBudget   { get; init; } = 3500m;

        // ── Диетические предпочтения ──────────────────────────────────────────
        public bool Vegetarian { get; init; }
        public bool Vegan      { get; init; }
        public bool GlutenFree { get; init; }
        public bool LowCarb    { get; init; }
        public bool HighProtein{ get; init; }
        public bool LowFat     { get; init; }

        // ── Исключения ────────────────────────────────────────────────────────

        /// <summary>Продукты, которые нельзя включать в план.</summary>
        public HashSet<string> ExcludedProducts { get; init; } = new();

        // ── Параметры плана ───────────────────────────────────────────────────
        public int  MealsPerDay   { get; init; } = 3;
        public bool IncludeSnacks { get; init; } = true;

        /// <summary>
        /// Минимальное количество дней между повторным использованием одного рецепта.
        /// 0 = повторы запрещены в рамках всей недели.
        /// 2 = рецепт может повториться не раньше чем через 2 дня.
        /// </summary>
        public int MinDaysBetweenRepeats { get; init; } = 2;

        /// <summary>Seed для воспроизводимой генерации. null = случайный.</summary>
        public int? Seed { get; init; } = null;

        // ── Все доступные рецепты (передаются снаружи, не грузятся внутри) ───
        public List<Recipe> AllRecipes { get; init; } = new();

        // ── Типы приёма пищи ─────────────────────────────────────────────────
        public MealType BreakfastType { get; init; } = null!;
        public MealType LunchType     { get; init; } = null!;
        public MealType DinnerType    { get; init; } = null!;
        public MealType? SnackType    { get; init; }

        // ── Вычисляемые свойства ──────────────────────────────────────────────

        /// <summary>Целевые ккал/день с учётом цели (похудение/набор).</summary>
        public int AdjustedDailyCalories => Goal switch
        {
            "lose"      => DailyCalories - 400,
            "lose_fast" => DailyCalories - 700,
            "gain"      => DailyCalories + 400,
            "gain_lean" => DailyCalories + 200,
            _           => DailyCalories
        };

        public int BreakfastTarget => (int)(AdjustedDailyCalories * 0.25);
        public int LunchTarget     => (int)(AdjustedDailyCalories * 0.35);
        public int DinnerTarget    => (int)(AdjustedDailyCalories * 0.30);
        public int SnackTarget     => (int)(AdjustedDailyCalories * 0.10);

        /// <summary>Бюджет на один день.</summary>
        public decimal DailyBudget => ConsiderBudget ? WeeklyBudget / 7m : decimal.MaxValue;

        /// <summary>Допустимое отклонение калорий по дням (±15% от цели).</summary>
        public int DailyCalorieTolerance => (int)(AdjustedDailyCalories * 0.15);
    }
}