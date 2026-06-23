using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Enums;

namespace Domain.Entities
{
    public class Recipe
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int DefaultServings { get; set; } = 1;
        public string? ImageUrl { get; set; }
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// Битовые теги рецепта (RecipeTag). Хранится как int в БД.
        /// Пример: Tags = RecipeTag.Lunch | RecipeTag.Soup | RecipeTag.Quick
        /// </summary>
        public RecipeTag Tags { get; set; } = RecipeTag.None;

        // Автор рецепта (null = системный рецепт)
        public Guid? AuthorId { get; set; }
        public User? Author { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
        public ICollection<RecipeStep> Steps { get; set; } = [];
        public ICollection<PlannedMeal> PlannedMeals { get; set; } = [];
        public ICollection<FavoriteRecipe> FavoritedBy { get; set; } = [];

        // ── Вычисляемые поля ──────────────────────────────────────────────────

        public decimal TotalCost => Ingredients.Sum(i =>
            (i.Product?.PricePerUnit ?? 0) * (i.GetGrams() / 100));
        public decimal TotalCalories => Ingredients.Sum(i => i.Calories ?? 0);
        public decimal TotalProtein  => Ingredients.Sum(i => i.Protein  ?? 0);
        public decimal TotalFat      => Ingredients.Sum(i => i.Fat      ?? 0);
        public decimal TotalCarbs    => Ingredients.Sum(i => i.Carbs    ?? 0);
        public decimal CostPerServing => DefaultServings > 0 ? TotalCost / DefaultServings : 0;

        public decimal CaloriesPerServing => DefaultServings > 0 ? TotalCalories / DefaultServings : 0;
        public decimal ProteinPerServing  => DefaultServings > 0 ? TotalProtein  / DefaultServings : 0;
        public decimal FatPerServing      => DefaultServings > 0 ? TotalFat      / DefaultServings : 0;
        public decimal CarbsPerServing    => DefaultServings > 0 ? TotalCarbs    / DefaultServings : 0;

        /// <summary>Проверяет наличие тега (поддерживает комбинированные флаги).</summary>
        public bool HasTag(RecipeTag tag) => Tags.HasFlag(tag);
        
        public bool IsRealisticSingleServing =>
            CaloriesPerServing >= 30 && CaloriesPerServing <= 1500 &&
            CostPerServing >= 0 && CostPerServing <= 500;
    }
}