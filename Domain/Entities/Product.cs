using Domain.Enums;

namespace Domain.Entities
{
    public class Product
    {
        public Guid Id { get; set; }

        public string? ExternalId { get; set; }

        public string Name { get; set; } = null!;

        public decimal PricePerUnit { get; set; }
        public string Unit { get; set; } = "г";           // по умолчанию граммы

        public decimal? WeightGrams { get; set; }         // вес упаковки, если есть

        // Нутриенты на 100г
        public decimal? CaloriesPer100 { get; set; }
        public decimal? ProteinPer100 { get; set; }
        public decimal? FatPer100 { get; set; }
        public decimal? CarbsPer100 { get; set; }

        public string? ImageUrl { get; set; }
        public string? Source { get; set; }

        // Новая категория
        public ProductCategory Category { get; set; } = ProductCategory.Other;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Иерархия (если нужно)
        public Guid? ParentId { get; set; }
        public Product? Parent { get; set; }
        public ICollection<Product> Children { get; set; } = new List<Product>();

        public ICollection<RecipeIngredient> UsedInRecipeIngredients { get; set; } = new List<RecipeIngredient>();
    }
}