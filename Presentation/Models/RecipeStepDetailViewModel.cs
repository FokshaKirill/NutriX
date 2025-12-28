namespace Presentation.Models;

public class RecipeStepDetailViewModel
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? TimerSeconds { get; set; }
    public string? ImageUrl { get; set; }
        
    public string? FormattedTime
    {
        get
        {
            if (!TimerSeconds.HasValue) return null;
                
            var timeSpan = TimeSpan.FromSeconds(TimerSeconds.Value);
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours} ч {timeSpan.Minutes} мин";
            if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes} мин";
            return $"{timeSpan.Seconds} сек";
        }
    }
}