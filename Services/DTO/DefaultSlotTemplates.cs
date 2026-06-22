using Domain.Enums;

namespace Services.DTO;

public static class DefaultSlotTemplates
{
    public static List<SlotTemplate> Get(GenerateWeekRequest req)
    {
        var breakfast = new SlotTemplate
        {
            MealType       = req.BreakfastType,
            TargetCalories = req.BreakfastTarget,
            Roles =
            [
                new() { Tag = RecipeTag.Breakfast,  IsRequired = true,  CalorieFraction = 0.70 },
                new() { Tag = RecipeTag.MainCourse, IsRequired = false, CalorieFraction = 0.70 }, 
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.30,
                    IsAddon = true, AddonChance = 0.35 }
            ]
        };

        var lunch = new SlotTemplate
        {
            MealType       = req.LunchType,
            TargetCalories = req.LunchTarget,
            Roles =
            [
                new() { Tag = RecipeTag.Soup,       IsRequired = false, CalorieFraction = 0.28 },
                new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.44 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.15 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.13 },
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.10,
                    IsAddon = true, AddonChance = 0.30 }
            ]
        };

        var dinner = new SlotTemplate
        {
            MealType       = req.DinnerType,
            TargetCalories = req.DinnerTarget,
            Roles =
            [
                new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.55 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.28 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.17 },
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.10,
                    IsAddon = true, AddonChance = 0.30 }
            ]
        };

        SlotTemplate? MakeSnack(int targetKcal, double drinkChance = 0.7) =>
            req.SnackType == null ? null : new SlotTemplate
            {
                MealType       = req.SnackType,
                TargetCalories = targetKcal,
                Roles =
                [
                    new() { Tag = RecipeTag.Snack,      IsRequired = false, CalorieFraction = 0.65 },
                    new() { Tag = RecipeTag.Dessert,    IsRequired = false, CalorieFraction = 0.65 },
                    new() { Tag = RecipeTag.MainCourse, IsRequired = false, CalorieFraction = 0.65 },
                    new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.35,
                        IsAddon = true, AddonChance = drinkChance }
                ]
            };

        var lightBreakfast = new SlotTemplate
        {
            MealType       = req.BreakfastType,
            TargetCalories = req.BreakfastTarget,
            Roles =
            [
                new() { Tag = RecipeTag.Breakfast,  IsRequired = false, CalorieFraction = 0.60 },
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.40,
                    IsAddon = true, AddonChance = 0.80 }
            ]
        };

        var heavyBreakfast = new SlotTemplate
        {
            MealType       = req.BreakfastType,
            TargetCalories = req.BreakfastTarget,
            Roles =
            [
                new() { Tag = RecipeTag.Breakfast,  IsRequired = true,  CalorieFraction = 0.55 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.30 },
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.15,
                    IsAddon = true, AddonChance = 0.50 }
            ]
        };

        var lightLunch = new SlotTemplate
        {
            MealType       = req.LunchType,
            TargetCalories = req.LunchTarget,
            Roles =
            [
                new() { Tag = RecipeTag.Soup,  IsRequired = false, CalorieFraction = 0.50 },
                new() { Tag = RecipeTag.Salad, IsRequired = true,  CalorieFraction = 0.50 },
                new() { Tag = RecipeTag.Drink, IsRequired = false, CalorieFraction = 0.15,
                    IsAddon = true, AddonChance = 0.40 }
            ]
        };

        var fullLunch = new SlotTemplate
        {
            MealType       = req.LunchType,
            TargetCalories = req.LunchTarget,
            Roles =
            [
                new() { Tag = RecipeTag.Soup,       IsRequired = true,  CalorieFraction = 0.25 },
                new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.40 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.20 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.15 },
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.10,
                    IsAddon = true, AddonChance = 0.40 }
            ]
        };

        var lightDinner = new SlotTemplate
        {
            MealType       = req.DinnerType,
            TargetCalories = req.DinnerTarget,
            Roles =
            [
                new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.65 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.35 }
            ]
        };

        var fullDinner = new SlotTemplate
        {
            MealType       = req.DinnerType,
            TargetCalories = req.DinnerTarget,
            Roles =
            [
                new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.45 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.28 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.15 },
                new() { Tag = RecipeTag.Dessert,    IsRequired = false, CalorieFraction = 0.12,
                    IsAddon = true, AddonChance = 0.50 },
                new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.10,
                    IsAddon = true, AddonChance = 0.30 }
            ]
        };

        var snack      = MakeSnack(req.SnackTarget);
        var halfSnack  = MakeSnack(req.SnackTarget / 2);

        return req.MealsPerDay switch
        {
            2 => [
                new SlotTemplate
                {
                    MealType       = req.BreakfastType,
                    TargetCalories = (int)(req.AdjustedDailyCalories * 0.45),
                    Roles =
                    [
                        new() { Tag = RecipeTag.Breakfast,  IsRequired = false, CalorieFraction = 0.50 },
                        new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.50 },
                        new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.15,
                            IsAddon = true, AddonChance = 0.50 }
                    ]
                },
                new SlotTemplate
                {
                    MealType       = req.DinnerType,
                    TargetCalories = (int)(req.AdjustedDailyCalories * 0.55),
                    Roles =
                    [
                        new() { Tag = RecipeTag.MainCourse, IsRequired = true,  CalorieFraction = 0.45 },
                        new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.28 },
                        new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.15 },
                        new() { Tag = RecipeTag.Dessert,    IsRequired = false, CalorieFraction = 0.12,
                            IsAddon = true, AddonChance = 0.40 }
                    ]
                }
            ],

            // 3 приёма: стандарт
            3 => [breakfast, lunch, dinner],

            // 4 приёма: + 1 перекус после обеда
            4 when snack != null => [breakfast, lunch, snack, dinner],
            4                    => [breakfast, lunch, dinner],

            // 5 приёмов: + 2 перекуса
            5 when halfSnack != null => [breakfast, halfSnack, fullLunch, halfSnack, fullDinner],
            5                        => [breakfast, fullLunch, fullDinner],

            // 6 приёмов: лёгкий завтрак, перекус, обед, перекус, ужин, вечерний перекус
            6 when halfSnack != null => [lightBreakfast, halfSnack, fullLunch, halfSnack, fullDinner, halfSnack],
            6                        => [lightBreakfast, fullLunch, fullDinner],

            // 7 приёмов: максимальная дробность
            7 when halfSnack != null => [lightBreakfast, halfSnack, lightLunch, halfSnack, fullLunch, halfSnack, lightDinner],
            7                        => [lightBreakfast, lightLunch, fullLunch, lightDinner],

            // Любое другое значение — стандартные 3
            _ => [breakfast, lunch, dinner]
        };
    }
}