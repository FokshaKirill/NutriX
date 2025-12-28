namespace Presentation.Models;

public class RecipeIngredientViewModel
{
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Unit { get; set; } = null!;
    public string? Comment { get; set; }
    public decimal? Calories { get; set; }
}