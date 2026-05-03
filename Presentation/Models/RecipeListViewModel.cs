namespace Presentation.Models;

public class RecipeListViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? CookingTimeMinutes { get; set; }
    public int DefaultServings { get; set; } = 1;
    public decimal TotalCost { get; set; } = 0;
    public int IngredientsCount { get; set; }
    public int StepsCount { get; set; }
    public bool IsFavorite { get; set; }
    
    // Пищевая ценность на порцию
    public decimal? CaloriesPerServing { get; set; }
    public decimal? ProteinPerServing { get; set; }
    public decimal? FatPerServing { get; set; }
    public decimal? CarbsPerServing { get; set; }

    public DateTime CreatedAt { get; set; }
}
