using Domain.Enums;

namespace Services.DTO;

public static class DefaultSlotTemplates
{
    public static List<SlotTemplate> Get(GenerateWeekRequest req)
    {
        int total = req.AdjustedDailyCalories;

        SlotTemplate Breakfast() => new()
        {
            MealType = req.BreakfastType,
            Roles =
            [
                // Сумма не-аддонов = 1.0: 0.55 + 0.45 = 1.0
                new() { Tag = RecipeTag.Breakfast,  IsRequired = false, CalorieFraction = 0.55 },
                new() { Tag = RecipeTag.MainCourse, IsRequired = false, CalorieFraction = 0.45 },
                new() { Tag = RecipeTag.Drink, IsRequired = false, CalorieFraction = 0.30,
                    IsAddon = true, AddonChance = 0.35 }
            ]
        };

        SlotTemplate Lunch() => new()
        {
            MealType = req.LunchType,
            Roles =
            [
                // Сумма не-аддонов = 1.0: 0.22 + 0.45 + 0.18 + 0.15 = 1.0
                new() { Tag = RecipeTag.Soup,       IsRequired = false, CalorieFraction = 0.22 },
                new() { Tag = RecipeTag.MainCourse, IsRequired = false, CalorieFraction = 0.45 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.18 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.15 },
                new() { Tag = RecipeTag.Drink, IsRequired = false, CalorieFraction = 0.10,
                    IsAddon = true, AddonChance = 0.30 }
            ]
        };

        SlotTemplate Dinner() => new()
        {
            MealType = req.DinnerType,
            Roles =
            [
                // Сумма не-аддонов = 1.0: 0.50 + 0.30 + 0.20 = 1.0
                new() { Tag = RecipeTag.MainCourse, IsRequired = false, CalorieFraction = 0.50 },
                new() { Tag = RecipeTag.Garnish,    IsRequired = false, CalorieFraction = 0.30 },
                new() { Tag = RecipeTag.Salad,      IsRequired = false, CalorieFraction = 0.20 },
                new() { Tag = RecipeTag.Drink, IsRequired = false, CalorieFraction = 0.10,
                    IsAddon = true, AddonChance = 0.30 }
            ]
        };

        SlotTemplate? Snack() =>
            req.SnackType == null ? null : new SlotTemplate
            {
                MealType = req.SnackType,
                Roles =
                [
                    // Сумма не-аддонов = 1.0: 0.50 + 0.50 = 1.0
                    new() { Tag = RecipeTag.Snack,      IsRequired = false, CalorieFraction = 0.50 },
                    new() { Tag = RecipeTag.Dessert,    IsRequired = false, CalorieFraction = 0.50 },
                    new() { Tag = RecipeTag.Drink,      IsRequired = false, CalorieFraction = 0.65,
                        IsAddon = true, AddonChance = 0.40 }
                ]
            };

        // ── Веса (относительные доли дня, не абсолютные ккал) ──
        List<(SlotTemplate Slot, double Weight)> plan;

        switch (req.MealsPerDay)
        {
            case 2:
                plan =
                [
                    (Breakfast(), 0.45),
                    (Dinner(),    0.55)
                ];
                break;

            case 4 when Snack() is { } s4:
                plan =
                [
                    (Breakfast(), 0.25),
                    (Lunch(),     0.35),
                    (s4,          0.10),
                    (Dinner(),    0.30)
                ];
                break;

            case 5 when Snack() is { } s5a && Snack() is { } s5b:
                plan =
                [
                    (Breakfast(), 0.22),
                    (s5a,         0.08),
                    (Lunch(),     0.32),
                    (s5b,         0.08),
                    (Dinner(),    0.30)
                ];
                break;

            case 6 when Snack() is { } s6a && Snack() is { } s6b && Snack() is { } s6c:
                plan =
                [
                    (Breakfast(), 0.20),
                    (s6a,         0.07),
                    (Lunch(),     0.28),
                    (s6b,         0.07),
                    (Dinner(),    0.28),
                    (s6c,         0.10)
                ];
                break;

            case 7 when Snack() is { } s7a && Snack() is { } s7b && Snack() is { } s7c && Snack() is { } s7d:
                plan =
                [
                    (Breakfast(), 0.16),
                    (s7a,         0.08),
                    (Lunch(),     0.22),
                    (s7b,         0.08),
                    (Dinner(),    0.22),
                    (s7c,         0.08),
                    (s7d,         0.16)
                ];
                break;

            default:
                plan =
                [
                    (Breakfast(), 0.25),
                    (Lunch(),     0.40),
                    (Dinner(),    0.35)
                ];
                break;
        }

        double weightSum = plan.Sum(p => p.Weight);
        foreach (var (slot, weight) in plan)
            slot.TargetCalories = (int)Math.Round(total * weight / weightSum);

        return plan.Select(p => p.Slot).ToList();
    }
}