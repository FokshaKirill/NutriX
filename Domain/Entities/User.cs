using Domain.Enums;

namespace Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string? GoogleId { get; set; }

        public UserRole Role { get; set; } = UserRole.Guest;
        public SubscriptionType SubscriptionType { get; set; } = SubscriptionType.Free;

        public bool IsGoogleUser { get; set; }
        public bool IsEmailConfirmed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? SubscriptionExpiresAt { get; set; }

        public ICollection<Product> Products { get; set; } = [];
        public ICollection<MealType> MealTypes { get; set; } = [];
        public ICollection<Recipe> Recipes { get; set; } = [];
        public ICollection<MealPlan> MealPlans { get; set; } = [];
    }
}