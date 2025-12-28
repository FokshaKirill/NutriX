namespace Presentation.Models;

public class RecipeStepViewModel
{
    public int Order { get; set; }
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public int? TimerSeconds { get; set; }
}