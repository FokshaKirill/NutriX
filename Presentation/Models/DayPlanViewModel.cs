namespace Presentation.Models;

public class DayPlanViewModel
{
    public DateTime Date { get; set; }
    public List<PlannedMealViewModel> Meals { get; set; } = [];
}