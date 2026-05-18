namespace Presentation.Models;

public class ProductViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public decimal PricePerUnit { get; set; }
    public string Unit { get; set; } = null!;

    public decimal? WeightGrams { get; set; }    

    public decimal? CaloriesPer100 { get; set; }
    public decimal? ProteinPer100 { get; set; }
    public decimal? FatPer100 { get; set; }
    public decimal? CarbsPer100 { get; set; }

    public string? CategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public string? Source { get; set; }       
}