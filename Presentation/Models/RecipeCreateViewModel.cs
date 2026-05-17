// Presentation.Models/RecipeCreateViewModel.cs

namespace Presentation.Models;

public class RecipeCreateViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DefaultServings { get; set; } = 1;
    public decimal TotalCost { get; set; } = 0;
    public string? ImageUrl { get; set; }

    public List<RecipeIngredientViewModel> Ingredients { get; set; } = new();
    public List<RecipeStepViewModel> Steps { get; set; } = new();
}
