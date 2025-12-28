namespace Domain.Entities
{
    // Models/MealType.cs
    public class MealType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public int Order { get; set; } = 0;

        // public Guid? UserId { get; set; }
        // public User? User { get; set; }

        public ICollection<PlannedMeal> PlannedMeals { get; set; } = [];
    }
}
