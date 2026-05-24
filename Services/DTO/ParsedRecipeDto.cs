namespace Services.DTO;


public class ParsedRecipeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Servings { get; set; } = 4;
    public string? ImageUrl { get; set; }
    public List<ParsedIngredientDto> Ingredients { get; set; } = new();
    public List<ParsedStepDto> Steps { get; set; } = new();
    public decimal? TotalCalories { get; set; }
    public decimal? TotalProtein { get; set; }
    public decimal? TotalFat { get; set; }
    public decimal? TotalCarbs { get; set; }
}