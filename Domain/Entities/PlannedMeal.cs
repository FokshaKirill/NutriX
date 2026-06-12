using Domain.Enums;

namespace Domain.Entities
{
    /// <summary>
    /// Одно блюдо внутри слота приёма пищи (<see cref="MealSlot"/>).
    /// Поле <see cref="Role"/> определяет роль блюда в приёме пищи
    /// (суп, основное, салат, напиток и т.д.).
    /// </summary>
    public class PlannedMeal
    {
        public Guid Id { get; set; }

        /// <summary>Слот приёма пищи, которому принадлежит это блюдо.</summary>
        public Guid MealSlotId { get; set; }
        public MealSlot MealSlot { get; set; } = null!;

        /// <summary>
        /// Роль блюда в приёме пищи.
        /// Например: Soup, MainCourse, Salad, Drink, Garnish.
        /// Совпадает с тегом (<see cref="RecipeTag"/>), по которому был найден рецепт.
        /// </summary>
        public RecipeTag Role { get; set; }

        public Guid RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public int Servings { get; set; } = 1;
    }
}