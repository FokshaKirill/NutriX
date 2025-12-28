namespace Domain.Entities
{
    public class PlannedMeal
    {
        public Guid Id { get; set; }

        public Guid MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        public Guid MealTypeId { get; set; }
        public MealType MealType { get; set; } = null!;

        public Guid? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public int DayOffset { get; set; } 
        public int Servings { get; set; } = 1;
    }
}
