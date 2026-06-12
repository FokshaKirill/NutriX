using Domain.Enums;

namespace Services.DTO
{
    /// <summary>
    /// Описание одной роли внутри шаблона приёма пищи.
    /// Например: "Суп, обязательный, занимает 30% калорий обеда".
    /// </summary>
    public class RoleTemplate
    {
        /// <summary>Тег, по которому ищется рецепт для этой роли.</summary>
        public RecipeTag Tag { get; set; }

        /// <summary>
        /// Обязательная роль (true) или опциональная (false).
        /// Если рецепт не найден и роль обязательная — в лог пишется предупреждение.
        /// Если роль опциональная — слот просто генерируется без этого блюда.
        /// </summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// Доля калорий приёма пищи, отводимая на эту роль (от 0.0 до 1.0).
        /// Сумма по всем ролям слота должна быть ≈ 1.0.
        /// </summary>
        public double CalorieFraction { get; set; }
        
        public bool      IsAddon         { get; init; } = false;
        public double    AddonChance     { get; init; } = 0.4; 
    }
}