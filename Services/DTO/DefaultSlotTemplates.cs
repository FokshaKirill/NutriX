using Domain.Enums;

namespace Services.DTO
{
    /// <summary>
    /// Фабрика дефолтных шаблонов слотов приёмов пищи.
    /// Шаблоны определяют какие роли блюд (Soup, MainCourse, Salad…)
    /// входят в каждый приём пищи и какую долю калорий занимают.
    /// </summary>
    public static class DefaultSlotTemplates
    {
        /// <summary>
        /// Возвращает список шаблонов слотов для одного дня на основе запроса.
        /// Перекус добавляется только если IncludeSnacks=true и MealsPerDay≥4.
        /// </summary>
        public static List<SlotTemplate> Get(GenerateWeekRequest req)
        {
            var breakfast = new SlotTemplate
            {
                MealType       = req.BreakfastType,
                TargetCalories = req.BreakfastTarget,
                Roles = [
                    new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.75 },
                    new() { Tag = RecipeTag.Drink, IsRequired = false, CalorieFraction = 0.25, IsAddon = true, AddonChance = 0.3 }
                ]
            };

            var lunch = new SlotTemplate
            {
                MealType       = req.LunchType,
                TargetCalories = req.LunchTarget,
                Roles = [
                    new() { Tag = RecipeTag.Soup,       IsRequired = false, CalorieFraction = 0.30 },
                    new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.45 },
                    new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.15 },
                    new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.10, IsAddon = true, AddonChance = 0.3 }
                ]
            };

            var dinner = new SlotTemplate
            {
                MealType       = req.DinnerType,
                TargetCalories = req.DinnerTarget,
                Roles = [
                    new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.55 },
                    new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.30 },
                    new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.15 },
                    new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.10, IsAddon = true, AddonChance = 0.3 }
                ]
            };

            SlotTemplate? MakeSnack(int targetKcal) => req.SnackType == null ? null : new SlotTemplate
            {
                MealType       = req.SnackType,
                TargetCalories = targetKcal,
                Roles = [
                    new() { Tag = RecipeTag.Snack, IsRequired = true,  CalorieFraction = 0.70 },
                    new() { Tag = RecipeTag.Drink, IsRequired = false, CalorieFraction = 0.30, IsAddon = true, AddonChance = 0.8 }
                ]
            };

            return req.MealsPerDay switch
            {
                3 => [breakfast, lunch, dinner],

                4 when MakeSnack(req.SnackTarget) is { } snack
                    => [breakfast, lunch, snack, dinner],
                4   => [breakfast, lunch, dinner],

                >= 5 when MakeSnack(req.SnackTarget / 2) is { } snack
                    => [breakfast, snack, lunch, snack, dinner],
                >= 5 => [breakfast, lunch, dinner],

                _ => [breakfast, lunch, dinner]
            };
        }
    }
}