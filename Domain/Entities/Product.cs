using Domain.Enums;

namespace Domain.Entities
{
    public class Product
    {
        public Guid Id { get; set; }

        public string? ExternalId { get; set; }

        public string Name { get; set; } = null!;

        public decimal PricePerUnit { get; set; }
        public string Unit { get; set; } = "г";      

        public decimal? WeightGrams { get; set; }    

        public decimal? CaloriesPer100 { get; set; }
        public decimal? ProteinPer100 { get; set; }
        public decimal? FatPer100 { get; set; }
        public decimal? CarbsPer100 { get; set; }

        public string? ImageUrl { get; set; }
        public string? Source { get; set; }

        public ProductCategory Category { get; set; } = ProductCategory.Other;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<RecipeIngredient> UsedInRecipeIngredients { get; set; } = new List<RecipeIngredient>();
    }
}