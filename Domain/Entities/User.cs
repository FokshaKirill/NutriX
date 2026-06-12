using Domain.Enums;

namespace Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;

        public string? PasswordHash { get; set; }
        public string? GoogleId { get; set; }
        public string? AvatarUrl { get; set; }  
        public string? Bio { get; set; }             
        public string? TimeZone { get; set; }
        public decimal? WeightKg     { get; set; }
        public decimal? HeightCm     { get; set; }
        public int?     Age          { get; set; }
        public string?  Gender       { get; set; } 
        public string?  ActivityLevel { get; set; }

        public UserRole Role { get; set; } = UserRole.User;
        public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;

        public bool IsGoogleUser { get; set; }
        public bool IsEmailConfirmed { get; set; }

        // Настройки питания
        public int? DailyCalorieGoal { get; set; }
        public decimal? DailyProteinGoal { get; set; }
        public decimal? DailyFatGoal { get; set; }
        public decimal? DailyCarbsGoal { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? SubscriptionExpiresAt { get; set; }

        // Навигационные свойства
        public ICollection<Recipe> Recipes { get; set; } = [];    
        public ICollection<FavoriteRecipe> FavoriteRecipes { get; set; } = []; 
        public ICollection<MealPlan> MealPlans { get; set; } = [];
    }
}