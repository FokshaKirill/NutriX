// Services/NutritionSummaryService.cs

using Services.Interfaces;

namespace Services.Services;

public class NutritionSummaryService : INutritionSummaryService
{
    private readonly IMealPlanService _mealPlanService;

    public NutritionSummaryService(IMealPlanService mealPlanService)
        => _mealPlanService = mealPlanService;

    public async Task<DailyNutritionSummary> GetTodayAsync(Guid userId)
    {
        var plan = await _mealPlanService.GetCurrentWeekPlanAsync(userId);
        if (plan is null)
            return new DailyNutritionSummary(0, 0, 0, 0);

        var today  = DateTime.Today;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).Date;
        var offset = (today - monday).Days;

        var items = plan.Slots
            .Where(s => s.DayOffset == offset)
            .SelectMany(s => s.Items)
            .Where(m => m.Recipe != null)
            .ToList();

        return new DailyNutritionSummary(
            Kcal:    (int)Math.Round(items.Sum(m => m.Recipe!.CaloriesPerServing * m.Servings)),
            Protein: (int)Math.Round(items.Sum(m => m.Recipe!.ProteinPerServing  * m.Servings)),
            Fat:     (int)Math.Round(items.Sum(m => m.Recipe!.FatPerServing      * m.Servings)),
            Carbs:   (int)Math.Round(items.Sum(m => m.Recipe!.CarbsPerServing    * m.Servings))
        );
    }
}