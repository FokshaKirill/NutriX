using Domain.Enums;

namespace Services.DTO
{
    /// <summary>
    /// Шаблон одного приёма пищи (завтрак, обед, ужин, перекус).
    /// Определяет целевые калории слота и набор ролей которые нужно заполнить.
    /// </summary>
    public class SlotTemplate
    {
        public MealType MealType { get; set; } = null!;

        /// <summary>Целевые калории для всего слота (сумма по всем ролям).</summary>
        public int TargetCalories { get; set; }

        /// <summary>Роли блюд внутри этого приёма пищи.</summary>
        public List<RoleTemplate> Roles { get; set; } = [];
    }
}