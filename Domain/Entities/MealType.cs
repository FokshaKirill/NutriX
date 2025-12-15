namespace Domain.Entities
{
    // Models/MealType.cs
    public class MealType
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int Order { get; set; } = 0;

        public int? UserId { get; set; }
        public User? User { get; set; }

        public ICollection<PlannedMeal> PlannedMeals { get; set; } = [];
    }
}
