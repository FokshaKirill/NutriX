namespace Domain.Entities
{
    // Models/Product.cs
    public class Product
    {
        public Guid Id { get; set; }
        public string? ExternalId { get; set; } = null!;
        public string Name { get; set; } = null!;

        public decimal PricePerUnit { get; set; }
        public string Unit { get; set; } = null!;

        public decimal? WeightGrams { get; set; }
        public decimal? CaloriesPer100 { get; set; }
        public decimal? ProteinPer100 { get; set; }
        public decimal? FatPer100 { get; set; }
        public decimal? CarbsPer100 { get; set; }
        public string? ImageUrl { get; set; }
        public string? Source { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Иерархия
        public Guid? ParentId { get; set; }
        public Product? Parent { get; set; }
        public ICollection<Product> Children { get; set; } = [];

        // public Guid? UserId { get; set; }
        // public User? User { get; set; }

        public ICollection<RecipeIngredient> UsedInRecipeIngredients { get; set; } = [];
    }
}
