namespace Presentation.Models;

public class RecipeDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int DefaultServings { get; set; }
    public decimal TotalCost { get; set; } = 0;
    public decimal CostPerServing => TotalCost / DefaultServings;
    
    public bool IsFavorite { get; set; }
    public bool IsOwner    { get; set; }

    public List<RecipeIngredientViewModel> Ingredients { get; set; } = [];
    public List<RecipeStepViewModel> Steps { get; set; } = [];

    public decimal CaloriesPerServing => TotalCalories / DefaultServings;
    public decimal TotalCalories => DefaultServings > 0 ? Ingredients.Sum(i => i.Calories ?? 0) : 0;
    public decimal ProteinPerServing { get; set; } = 0;
    public decimal FatPerServing { get; set; } = 0;
    public decimal CarbsPerServing { get; set; } = 0;
}