namespace Presentation.Models;

public class ShoppingIngredientViewModel
{
    public Product? Product { get; set; }
    public string? ProductName => Product?.Name;
    public decimal Amount { get; set; }
    public string Unit { get; set; } = null!;
    public string? Comment { get; set; }
    public string? Category { get; set; }                          // ← добавить
    public decimal TotalPrice => (Product?.PricePerUnit ?? 0) * Amount / 100; // ← добавить
}