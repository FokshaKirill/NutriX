namespace Presentation.Models;

public class RecipeIngredientViewModel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Unit { get; set; } = "г";
    public string? Comment { get; set; }

    public decimal? Calories { get; set; }
    public decimal? Protein  { get; set; }
    public decimal? Fat      { get; set; }
    public decimal? Carbs    { get; set; }

    public decimal PricePerUnit { get; set; }
    public decimal PriceForAmount => PricePerUnit * Amount / 100;
}