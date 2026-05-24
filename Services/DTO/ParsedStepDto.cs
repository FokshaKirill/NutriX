namespace Services.DTO;

public class ParsedStepDto
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? TimerSeconds { get; set; }
    public string? ImageUrl { get; set; }
}