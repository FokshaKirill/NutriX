namespace Domain.Entities
{
    // Models/MealPlan.cs
    public class MealPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public DateTime StartDate { get; set; } // Понедельник недели

        public int? UserId { get; set; }
        public User? User { get; set; }

        public ICollection<PlannedMeal> Meals { get; set; } = [];
    }
}