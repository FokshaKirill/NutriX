// Services/Interfaces/INutritionSummaryService.cs
namespace Services.Interfaces;

public record DailyNutritionSummary(int Kcal, int Protein, int Fat, int Carbs);

public interface INutritionSummaryService
{
    Task<DailyNutritionSummary> GetTodayAsync(Guid userId);
}