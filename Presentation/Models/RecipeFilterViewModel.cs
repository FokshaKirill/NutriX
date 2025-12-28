namespace Presentation.Models;

public class RecipeFilterViewModel
{
    public string? SearchQuery { get; set; }
    public int? MaxCookingTime { get; set; }
    public decimal? MaxCost { get; set; }
    public int? MinServings { get; set; }
    public int? MaxServings { get; set; }
    public List<Guid>? IngredientIds { get; set; }
    public string? SortBy { get; set; } // "name", "cost", "time", "calories"
    public bool SortDescending { get; set; }
}