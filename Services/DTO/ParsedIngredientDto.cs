namespace Services.DTO;

public class ParsedIngredientDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Unit { get; set; } = "г";
    public string? Comment { get; set; }
    public string OriginalText { get; set; } = string.Empty;
}