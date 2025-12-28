namespace Domain.Entities
{
    // Models/Recipe.cs
    public class Recipe
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int DefaultServings { get; set; } = 1;
        public decimal TotalCost { get; set; } = 0;
        public string? ImageUrl { get; set; }

        // public Guid? UserId { get; set; }
        // public User? User { get; set; }

        public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
        public ICollection<RecipeStep> Steps { get; set; } = [];
        public ICollection<PlannedMeal> PlannedMeals { get; set; } = [];
        public decimal TotalCalories => Ingredients.Sum(i => i.Calories ?? 0);
        public decimal TotalProtein => Ingredients.Sum(i => i.Protein ?? 0);
        public decimal TotalFat => Ingredients.Sum(i => i.Fat ?? 0);
        public decimal TotalCarbs => Ingredients.Sum(i => i.Carbs ?? 0);

        // На одну порцию
        public decimal CaloriesPerServing => DefaultServings > 0 ? TotalCalories / DefaultServings : 0;
        public decimal ProteinPerServing => DefaultServings > 0 ? TotalProtein / DefaultServings : 0;
        public decimal FatPerServing => DefaultServings > 0 ? TotalFat / DefaultServings : 0;
        public decimal CarbsPerServing => DefaultServings > 0 ? TotalCarbs / DefaultServings : 0;
    }
}
