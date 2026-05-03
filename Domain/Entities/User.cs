using Domain.Enums;

namespace Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;

        // Пароль опционален — Google-пользователи его не имеют
        public string? PasswordHash { get; set; }
        public string? GoogleId { get; set; }
        public string? AvatarUrl { get; set; }      // фото из Google
        public string? Bio { get; set; }             // короткое описание себя
        public string? TimeZone { get; set; }        // часовой пояс

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
        public ICollection<Recipe> Recipes { get; set; } = [];           // созданные рецепты
        public ICollection<FavoriteRecipe> FavoriteRecipes { get; set; } = []; // избранные
        public ICollection<MealPlan> MealPlans { get; set; } = [];
    }
}