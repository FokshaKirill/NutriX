using System.ComponentModel.DataAnnotations;

namespace Presentation.Models;

public class GenerateWeekViewModel
{
    // ── Базовые настройки калоража и целей ──
    // DailyCalories: базовая норма, введённая пользователем.
    // Коррекция по цели (Goal) применяется в GenerateWeekRequest.AdjustedDailyCalories.
    [Required]
    [Range(1000, 4500, ErrorMessage = "Калории должны быть в диапазоне от 1000 до 4500")]
    public int DailyCalories { get; set; } = 2000;

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
    public decimal? WeightKg      { get; set; }
    public decimal? HeightCm      { get; set; }
    public int?     Age           { get; set; }
    public string?  Gender        { get; set; }
    public string?  ActivityLevel { get; set; }

    // ── Исключаемые продукты ──
    public string? ExcludedProducts { get; set; } = "";

    // ── Бюджетные ограничения ──
    public bool ConsiderBudget { get; set; }
        
    [Range(500, 50000, ErrorMessage = "Бюджет должен быть в пределах от 500 до 50 000 ₽")]
    public decimal WeeklyBudget { get; set; } = 5000;

    // Вычисляемый дневной бюджет для контроллера
    public decimal DailyBudget => ConsiderBudget ? Math.Round(WeeklyBudget / 7, 2) : decimal.MaxValue;

    // ── Настройки структуры плана ──
    [Range(2, 7, ErrorMessage = "Количество приёмов пищи должно быть от 2 до 7")]
    public int MealsPerDay { get; set; } = 3;
    public bool IncludeSnacks { get; set; }
    public bool AvoidRepeats { get; set; }
    public bool IncludeDrinks { get; set; } = true;

    // Распределение калорий по приёмам пищи — рассчитывается ТОЛЬКО в JS для UI preview.
    // Фактическое распределение (с учётом Goal) выполняется в GenerateWeekRequest.AdjustedDailyCalories
    // и DefaultSlotTemplates. Эти свойства оставлены для обратной совместимости View, но не используются в генерации.

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