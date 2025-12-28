namespace Presentation.Models;

public class RecipeIngredientDetailViewModel
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Comment { get; set; }
        
    // Данные продукта
    public decimal? PricePerUnit { get; set; }
    public decimal? TotalPrice => Amount * (PricePerUnit ?? 0);
        
    // Пищевая ценность
    public decimal? CaloriesPer100 { get; set; }
    public decimal? ProteinPer100 { get; set; }
    public decimal? FatPer100 { get; set; }
    public decimal? CarbsPer100 { get; set; }
        
    // Расчет пищевой ценности для указанного количества
    public decimal? TotalCalories => CaloriesPer100 * Amount / 100;
    public decimal? TotalProtein => ProteinPer100 * Amount / 100;
    public decimal? TotalFat => FatPer100 * Amount / 100;
    public decimal? TotalCarbs => CarbsPer100 * Amount / 100;
}