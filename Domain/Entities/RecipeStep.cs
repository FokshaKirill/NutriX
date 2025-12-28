namespace Domain.Entities
{
    // Models/RecipeStep.cs
    public class RecipeStep
    {
        public Guid Id { get; set; }
        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public int Order { get; set; }
        public string Description { get; set; } = null!;
        public int? TimerSeconds { get; set; }
        public string? ImageUrl { get; set; }
    }
}
