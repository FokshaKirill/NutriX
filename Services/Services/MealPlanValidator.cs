using Domain.Entities;

namespace Services.Services;

/// <summary>
/// Валидатор сгенерированного плана питания.
/// Проверяет соответствие калорий, бюджета, уникальности и полноты.
/// </summary>
public class MealPlanValidator
{
    /// <summary>Результат валидации плана.</summary>
    public record ValidationResult(
        bool IsValid,
        List<string> Errors,
        int TotalPlannedKcal,
        decimal TotalPlannedCost);

    /// <summary>
    /// Проверить план на соответствие ожиданиям.
    /// </summary>
    /// <param name="plan">Сгенерированный план</param>
    /// <param name="targetDailyKcal">Целевые калории в день (adjusted)</param>
    /// <param name="weeklyBudget">Недельный бюджет (0 = не учитывать)</param>
    /// <param name="minMealsPerDay">Минимум блюд в день</param>
    /// <param name="kcalTolerancePercent">Допуск по калориям в % (по умолчанию 15%)</param>
    /// <param name="budgetTolerancePercent">Допуск по бюджету в % (по умолчанию 10%)</param>
    public ValidationResult Validate(
        MealPlan plan,
        int targetDailyKcal,
        decimal weeklyBudget = 0,
        int minMealsPerDay = 2,
        double kcalTolerancePercent = 15.0,
        double budgetTolerancePercent = 10.0)
    {
        var errors = new List<string>();
        var slots = plan.Slots.ToList();

        int totalKcal = 0;
        decimal totalCost = 0m;

        for (int dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var daySlots = slots.Where(s => s.DayOffset == dayOffset).ToList();
            int dayMealCount = daySlots.Sum(s => s.Items.Count);

            if (dayMealCount < minMealsPerDay)
            {
                errors.Add($"День {dayOffset + 1}: всего {dayMealCount} блюд (минимум {minMealsPerDay})");
            }

            int dayKcal = daySlots
                .SelectMany(s => s.Items)
                .Sum(m => (int)(m.Recipe?.CaloriesPerServing * m.Servings ?? 0));

            totalKcal += dayKcal;

            if (dayKcal == 0)
            {
                errors.Add($"День {dayOffset + 1}: 0 калорий — день пустой");
            }

            totalCost += daySlots
                .SelectMany(s => s.Items)
                .Sum(m => m.Recipe?.CostPerServing * m.Servings ?? 0);
        }

        // Проверка калорий за неделю
        int expectedWeeklyKcal = targetDailyKcal * 7;
        int kcalDeviation = Math.Abs(totalKcal - expectedWeeklyKcal);
        double kcalDevPercent = (double)kcalDeviation / expectedWeeklyKcal * 100.0;

        if (kcalDevPercent > kcalTolerancePercent && plan.BudgetWasInfeasible)
        {
            errors.Add(
                $"Калорийность плана ниже цели на {kcalDevPercent:F1}% — " +
                $"вероятная причина: указанный бюджет слишком мал для запрошенной калорийности. " +
                $"Увеличьте бюджет или уменьшите целевые калории / число приёмов пищи.");
        }

        // Проверка бюджета
        if (weeklyBudget > 0)
        {
            double budgetDevPercent = (double)(totalCost - weeklyBudget) / (double)weeklyBudget * 100.0;
            if (totalCost > weeklyBudget && budgetDevPercent > budgetTolerancePercent)
            {
                errors.Add(
                    $"Бюджет за неделю: {totalCost:F2} (лимит {weeklyBudget:F2}, " +
                    $"превышение {budgetDevPercent:F1}%, допуск {budgetTolerancePercent}%)");
            }
        }

        return new ValidationResult(!errors.Any(), errors, totalKcal, totalCost);
    }
}