using Domain.Enums;

namespace Services.DTO
{
    /// <summary>
    /// Строгость соблюдения бюджета при генерации.
    /// </summary>
    public enum BudgetStrictness
    {
        /// <summary>Бюджет не учитывается.</summary>
        Ignore,
 
        /// <summary>
        /// Гибкий режим: допускается превышение дневного бюджета до 10%.
        /// Недельный бюджет всё равно соблюдается строго.
        /// </summary>
        Flexible,
 
        /// <summary>Строгий режим: нельзя превысить бюджет ни на рубль.</summary>
        Strict
    }
}