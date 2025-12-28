namespace Presentation.Models;

public class ShoppingIngredientViewModel
{
    public Product? Product { get; set; }
    public decimal Amount { get; set; }
    public string Unit { get; set; } = null!;
    public string? Comment { get; set; }
}