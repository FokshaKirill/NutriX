namespace Domain.Enums
{
    /// <summary>
    /// Цель питания пользователя — влияет на скорректированную норму калорий.
    /// </summary>
    public enum NutritionGoal
    {
        /// <summary>Поддержание веса — калории без изменений.</summary>
        Maintain,

        /// <summary>Дефицит — −15% от нормы.</summary>
        Deficit,

        /// <summary>Профицит — +15% от нормы.</summary>
        Surplus
    }
}