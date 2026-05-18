using System.ComponentModel.DataAnnotations;

namespace Presentation.Models
{
    public class GenerateWeekViewModel
    {
        // ── Базовые настройки калоража и целей ──
        [Required]
        [Range(1000, 4500, ErrorMessage = "Калории должны быть в диапазоне от 1000 до 4500")]
        public int DailyCalories { get; set; } = 2000;
        public int AdjustedDailyCalories { get; set; } = 2000;

        [Required]
        public string Goal { get; set; } = "maintain"; // maintain, lose, lose_fast, gain, gain_lean

        // ── Таргеты по БЖУ (Макронутриенты) ──
        [Range(0, 500, ErrorMessage = "Некорректное значение белков")]
        public int? ProteinGoal { get; set; }

        [Range(0, 300, ErrorMessage = "Некорректное значение жиров")]
        public int? FatGoal { get; set; }

        [Range(0, 700, ErrorMessage = "Некорректное значение углеводов")]
        public int? CarbsGoal { get; set; }

        // ── Диетические предпочтения (флаги для фильтрации) ──
        public bool Vegetarian { get; set; }
        public bool Vegan { get; set; }
        public bool GlutenFree { get; set; }
        public bool LowCarb { get; set; }
        public bool HighProtein { get; set; }
        public bool LowFat { get; set; }

        // ── Исключаемые продукты ──
        public string? ExcludedProducts { get; set; } = "";

        // ── Бюджетные ограничения ──
        public bool ConsiderBudget { get; set; }
        
        [Range(500, 50000, ErrorMessage = "Бюджет должен быть в пределах от 500 до 50 000 ₽")]
        public decimal WeeklyBudget { get; set; } = 5000;

        // Вычисляемый дневной бюджет для контроллера
        public decimal DailyBudget => ConsiderBudget ? Math.Round(WeeklyBudget / 7, 2) : decimal.MaxValue;

        // ── Настройки структуры плана ──
        [Range(3, 6, ErrorMessage = "Количество приёмов пищи должно быть от 3 до 6")]
        public int MealsPerDay { get; set; } = 3;
        public bool IncludeSnacks { get; set; }
        public bool AvoidRepeats { get; set; }

        // ── Распределение калорий по типам приемов пищи ──
        // (Вычисляется динамически на основе скорректированного калоража)
        public int BreakfastTarget => (int)Math.Round(AdjustedCalories * 0.25);
        public int LunchTarget => (int)Math.Round(AdjustedCalories * 0.35);
        public int DinnerTarget => (int)Math.Round(AdjustedCalories * 0.30);
        public int SnackTarget => (int)Math.Round(AdjustedCalories * 0.10);

        // Корректировка калорий в зависимости от глобальной цели
        public int AdjustedCalories
        {
            get
            {
                return Goal switch
                {
                    "lose" => DailyCalories - 400,
                    "lose_fast" => DailyCalories - 700,
                    "gain" => DailyCalories + 400,
                    "gain_lean" => DailyCalories + 200,
                    _ => DailyCalories
                };
            }
        }

        /// <summary>
        /// Вспомогательный метод парсинга исключений для контроллера
        /// </summary>
        public HashSet<string> GetExcludedSet()
        {
            if (string.IsNullOrWhiteSpace(ExcludedProducts)) return [];
            return ExcludedProducts
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().ToLowerInvariant())
                .ToHashSet();
        }
    }
}