namespace Presentation.Models;

public class RecipeDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int DefaultServings { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CostPerServing => DefaultServings > 0 ? TotalCost / DefaultServings : 0;
    
    public bool IsFavorite { get; set; }
    public bool IsOwner    { get; set; }

    public List<RecipeIngredientViewModel> Ingredients { get; set; } = [];
    public List<RecipeStepViewModel> Steps { get; set; } = [];
    public Domain.Enums.RecipeTag Tags { get; set; } = Domain.Enums.RecipeTag.None;

    public decimal TotalCalories => DefaultServings > 0 ? Ingredients.Sum(i => i.Calories ?? 0) : 0;
    public decimal ProteinPerServing { get; set; } = 0;
    public decimal FatPerServing { get; set; } = 0;
    public decimal CarbsPerServing { get; set; } = 0;
    public decimal CaloriesPerServing => TotalCalories / DefaultServings;
}
