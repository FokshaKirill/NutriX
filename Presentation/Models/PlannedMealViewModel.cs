namespace Presentation.Models;

public class PlannedMealViewModel
{
    public Guid Id { get; set; }
    public string MealTypeName { get; set; } = null!;
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public string? RecipeImageUrl { get; set; }
    public int Servings { get; set; }

    public int Calories { get; set; }
    public int Protein { get; set; }
    public int Fat { get; set; }
    public int Carbs { get; set; }
}