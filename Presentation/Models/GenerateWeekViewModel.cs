namespace Presentation.Models
{
    public class GenerateWeekViewModel
    {
        public int DailyCalories { get; set; } = 2000; // цель на день
        public string Goal { get; set; } = "maintain"; // maintain, lose, gain — влияет на ±200 ккал
        public bool Vegetarian { get; set; }
        public bool Vegan { get; set; }
        public string? ExcludedProducts { get; set; } = ""; // через запятую
    }
}