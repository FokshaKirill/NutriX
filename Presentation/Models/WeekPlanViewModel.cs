namespace Presentation.Models;

public class WeekPlanViewModel
{
    public Guid MealPlanId { get; set; }
    public DateTime StartDate { get; set; }
    public List<DayPlanViewModel> Days { get; set; } = [];
}
