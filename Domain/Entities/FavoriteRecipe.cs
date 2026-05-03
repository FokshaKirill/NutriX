namespace Domain.Entities
{
    /// <summary>
    /// Связь пользователь ↔ избранный рецепт (many-to-many через явную таблицу)
    /// </summary>
    public class FavoriteRecipe
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}