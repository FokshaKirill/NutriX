namespace Presentation.Models;

public class RecipeDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int DefaultServings { get; set; }
    public decimal TotalCost { get; set; } = 0;

    public List<RecipeIngredientViewModel> Ingredients { get; set; } = [];
    public List<RecipeStepViewModel> Steps { get; set; } = [];

    public decimal CaloriesPerServing => DefaultServings > 0 ? Ingredients.Sum(i => i.Calories ?? 0) / DefaultServings : 0;
    // Аналогично Protein, Fat, Carbs
}