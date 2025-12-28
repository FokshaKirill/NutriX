namespace Domain.Entities
{
    // Models/RecipeIngredient.cs
    public class RecipeIngredient
    {
        public Guid Id { get; set; }

        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!; // Это будет группа (ParentId == null)

        public decimal Amount { get; set; }
        public string Unit { get; set; } = null!;
        public string? Comment { get; set; }
        private decimal GetGrams()
        {
            // Здесь нужна логика конвертации Amount + Unit в граммы.
            // Это сложная часть — зависит от продукта (плотность, стандартные конверсии).
            // Пример упрощённый: если Unit == "g" — return Amount, иначе TODO.
            // Рекомендую добавить метод или таблицу конверсий.
            return Unit.Trim().ToLower() == "g" ? Amount : Amount * 100; // placeholder
        }

        public decimal? Calories => Product?.CaloriesPer100 * (GetGrams() / 100);
        public decimal? Protein => Product?.ProteinPer100 * (GetGrams() / 100);
        public decimal? Fat => Product?.FatPer100 * (GetGrams() / 100);
        public decimal? Carbs => Product?.CarbsPer100 * (GetGrams() / 100);
    }
}
