namespace Presentation.Models
{
    public class HomeViewModel
    {
        public DateTime TodayDate { get; set; }

        public List<MealViewModel> TodayMeals { get; set; }

        public int TodayTotalCalories { get; set; }
        public int TodayTotalProtein { get; set; }
        public int TodayTotalFat { get; set; }
        public int TodayTotalCarbs { get; set; }

        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }

        public List<DayPlanViewModel> WeekDays { get; set; }
    }
}
