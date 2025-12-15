namespace Domain.Entities
{
    public class PlannedMeal
    {
        public int Id { get; set; }

        public int MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        public int MealTypeId { get; set; }
        public MealType MealType { get; set; } = null!;

        public int? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public int DayOffset { get; set; } 
        public int Servings { get; set; } = 1;
    }
}
