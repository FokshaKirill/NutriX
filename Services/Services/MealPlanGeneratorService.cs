using Domain.Entities;
using Domain.Enums;
using Services.DTO;
using Services.Interfaces;

namespace Services.Services
{
    /// <summary>
    /// Реализация алгоритма генерации меню на неделю.
    ///
    /// ── Алгоритм ──────────────────────────────────────────────────────────────
    ///
    /// 1. Фильтрация рецептов по диете и исключённым продуктам.
    ///
    /// 2. Для каждого дня (0..6) и каждого SlotTemplate:
    ///    а. Создаём MealSlot.
    ///    б. Для каждой RoleTemplate в слоте подбираем рецепт по лестнице
    ///       (окно ккал × ослабление кулдауна), от строгого к мягкому.
    ///    в. Добавляем слот в план (только если есть хоть одно блюдо).
    ///
    /// 3. Scale-up: докручиваем порции уже выбранных блюд, если дефицит
    ///    калорий дня превышает порог.
    ///
    /// 4. Gap-fill: если после scale-up дефицит всё ещё больше порога —
    ///    докидываем 1-3 лёгких "добивочных" блюда (перекус/напиток/
    ///    десерт/салат/гарнир), максимально близких по калориям к остатку.
    /// </summary>
    public class MealPlanGeneratorService : IMealPlanGeneratorService
    {
        // ── Константы алгоритма ────────────────────────────────────────────────

        private const double KcalWindow              = 0.45;
        private const double KcalWindowFallback       = 0.80;
        private const int    MaxServings              = 4;
        private const int    TopCandidatesCount        = 5;
        private const double CalorieDeficitThreshold   = 0.06;  // было 0.12
        private const int    ScaleUpIterations         = 8;
        private const int    MaxGapFillers             = 3;

        // Пороги макронутриентов (на 100 ккал)
        private const double LowCarbMaxCarbsPer100Kcal       = 10.0;
        private const double HighProteinMinProteinPer100Kcal = 7.0;
        private const double LowFatMaxFatPer100Kcal          = 3.5;

        // Веса скора при ConsiderBudget=true
        private const double WKcalBudget = 0.55;
        private const double WCostBudget = 0.45;

        // Предпочитаемые теги для "добивочных" блюд — лёгкие, не основное блюдо
        private static readonly RecipeTag[] FillerPreferredTags =
        {
            RecipeTag.Snack, RecipeTag.Drink, RecipeTag.Dessert,
            RecipeTag.Salad, RecipeTag.Garnish
        };

        // ──────────────────────────────────────────────────────────────────────

        public MealPlan? Generate(GenerateWeekRequest req)
        {
            var rand = req.Seed.HasValue ? new Random(req.Seed.Value) : new Random();

            // 1. Фильтрация
            var filtered = req.AllRecipes
                .Where(r => r.Ingredients.Any())
                .Where(r => !HasExcluded(r, req.ExcludedProducts))
                .Where(r => MatchesDiet(r, req))
                .ToList();

            if (!filtered.Any()) return null;

            // 2. Статистика для нормализации скора
            var stats = ComputeStats(filtered);

            // 3. Кулдаун: recipeId → последний день использования
            var lastUsedDay = new Dictionary<Guid, int>();

            // 4. Шаблоны слотов
            var slotTemplates = req.CustomSlotTemplates ?? DefaultSlotTemplates.Get(req);

            // 5. Строим план
            var monday = GetMonday();
            var plan = new MealPlan
            {
                Id        = Guid.NewGuid(),
                Name      = $"Меню на неделю с {monday:dd MMMM yyyy}",
                StartDate = monday,
                UserId    = req.UserId,
                Slots     = new List<MealSlot>()
            };

            decimal weekBudgetLeft = req.ConsiderBudget ? req.WeeklyBudget : decimal.MaxValue;

            // Лестница попыток подбора: сначала строго, потом всё мягче
            var attempts = new (double Window, double CooldownMul)[]
            {
                (KcalWindow,         1.0),
                (KcalWindowFallback, 1.0),
                (KcalWindowFallback, 0.5),
                (KcalWindowFallback, 0.0),
            };

            for (int day = 0; day < 7; day++)
            {
                decimal dayBudgetLeft = req.ConsiderBudget
                    ? Math.Min(req.DailyBudget, weekBudgetLeft)
                    : decimal.MaxValue;

                var dayMeals = new List<PlannedMeal>();
                var daySlots = new List<MealSlot>();

                foreach (var slotTemplate in slotTemplates)
                {
                    var slot = new MealSlot
                    {
                        Id         = Guid.NewGuid(),
                        MealPlanId = plan.Id,
                        MealPlan   = plan,
                        DayOffset  = day,
                        MealTypeId = slotTemplate.MealType.Id,
                        MealType   = slotTemplate.MealType,
                        Items      = new List<PlannedMeal>()
                    };

                    var usedInSlot = new HashSet<Guid>();

                    foreach (var role in slotTemplate.Roles)
                    {
                        if (role.IsAddon && (!req.IncludeDrinks || rand.NextDouble() > role.AddonChance))
                            continue;

                        int targetKcal = (int)(slotTemplate.TargetCalories * role.CalorieFraction);

                        var tagFiltered = filtered.Where(r => r.HasTag(role.Tag)).ToList();
                        if (!tagFiltered.Any())
                            tagFiltered = filtered;

                        PlannedMeal? meal = null;

                        foreach (var attempt in attempts)
                        {
                            var candidates = GetCandidates(
                                tagFiltered, req, targetKcal, attempt.Window,
                                day, lastUsedDay, usedInSlot, dayBudgetLeft, attempt.CooldownMul);

                            if (!candidates.Any()) continue;

                            var recipe = PickFromTopN(candidates, stats, targetKcal, req, rand);
                            if (recipe == null) continue;

                            meal = new PlannedMeal
                            {
                                Id         = Guid.NewGuid(),
                                MealSlotId = slot.Id,
                                MealSlot   = slot,
                                Role       = role.Tag,
                                RecipeId   = recipe.Id,
                                Recipe     = recipe,
                                Servings   = 1
                            };

                            dayBudgetLeft -= recipe.TotalCost;
                            if (req.ConsiderBudget)
                                weekBudgetLeft -= recipe.TotalCost;

                            break;
                        }

                        if (meal == null)
                        {
                            if (role.IsRequired)
                                Console.WriteLine($"[MealPlanGenerator] День {day}, слот {slotTemplate.MealType.Name}, " +
                                                  $"роль {role.Tag}: рецепт не найден даже после ослабления кулдауна.");
                            continue;
                        }

                        slot.Items.Add(meal);
                        dayMeals.Add(meal);
                        usedInSlot.Add(meal.RecipeId);
                        lastUsedDay[meal.RecipeId] = day;
                    }

                    if (slot.Items.Any())
                    {
                        plan.Slots.Add(slot);
                        daySlots.Add(slot);
                    }
                }

                // Scale-up калорий дня
                TryBalanceDayCalories(dayMeals, req, ref dayBudgetLeft, ref weekBudgetLeft);

                // Добивка остатка лёгкими блюдами, если scale-up не дотянул
                TryFillRemainingGap(daySlots, dayMeals, filtered, req, ref dayBudgetLeft, ref weekBudgetLeft);
            }

            return plan;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Группа кандидатов
        // ══════════════════════════════════════════════════════════════════════

        private static List<Recipe> GetCandidates(
            List<Recipe>          recipes,
            GenerateWeekRequest   req,
            int                   targetKcal,
            double                window,
            int                   day,
            Dictionary<Guid, int> lastUsedDay,
            HashSet<Guid>         usedInSlot,
            decimal               dayBudgetLeft,
            double                cooldownMultiplier = 1.0)
        {
            double low  = targetKcal * (1 - window);
            double high = targetKcal * (1 + window);

            int baseCooldown = req.MinDaysBetweenRepeats > 0
                ? req.MinDaysBetweenRepeats
                : (int)req.Diversity;

            int cooldown = (int)Math.Round(baseCooldown * cooldownMultiplier);

            return recipes
                .Where(r => (double)r.CaloriesPerServing >= low
                         && (double)r.CaloriesPerServing <= high)
                .Where(r => !usedInSlot.Contains(r.Id))
                .Where(r => cooldown <= 0
                         || !lastUsedDay.TryGetValue(r.Id, out int lastDay)
                         || (day - lastDay) >= cooldown)
                .Where(r => !req.ConsiderBudget || r.TotalCost <= dayBudgetLeft)
                .ToList();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Взвешенный случайный выбор из топ-N
        // ══════════════════════════════════════════════════════════════════════

        private static Recipe? PickFromTopN(
            List<Recipe>        candidates,
            RecipeStats         stats,
            int                 targetKcal,
            GenerateWeekRequest req,
            Random              rand)
        {
            if (!candidates.Any()) return null;

            var topN = candidates
                .Select(r => (Recipe: r, Score: ComputeScore(r, stats, targetKcal, req)))
                .OrderByDescending(x => x.Score)
                .Take(TopCandidatesCount)
                .ToList();

            return topN[rand.Next(topN.Count)].Recipe;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Scale-up калорий дня
        // ══════════════════════════════════════════════════════════════════════

        private static void TryBalanceDayCalories(
            List<PlannedMeal>   dayMeals,
            GenerateWeekRequest req,
            ref decimal         dayBudgetLeft,
            ref decimal         weekBudgetLeft)
        {
            if (!dayMeals.Any()) return;

            int targetKcal = req.AdjustedDailyCalories;
            int threshold  = (int)(targetKcal * CalorieDeficitThreshold);

            var priority = new[] { "Ужин", "Обед", "Завтрак", "Перекус" };

            for (int iter = 0; iter < ScaleUpIterations; iter++)
            {
                int current = dayMeals.Sum(m => (int)(m.Recipe!.CaloriesPerServing * m.Servings));
                if (targetKcal - current <= threshold) break;

                decimal capDay  = dayBudgetLeft;
                decimal capWeek = weekBudgetLeft;

                var candidate = priority
                    .SelectMany(name => dayMeals.Where(m =>
                        m.MealSlot?.MealType?.Name == name))
                    .FirstOrDefault(m =>
                        m.Servings < MaxServings
                        && (!req.ConsiderBudget || m.Recipe!.TotalCost <= capDay)
                        && (!req.ConsiderBudget || m.Recipe!.TotalCost <= capWeek));

                if (candidate == null) break;

                candidate.Servings++;
                if (req.ConsiderBudget)
                {
                    dayBudgetLeft  -= candidate.Recipe!.TotalCost;
                    weekBudgetLeft -= candidate.Recipe!.TotalCost;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Gap-fill: добивка остатка лёгкими блюдами
        // ══════════════════════════════════════════════════════════════════════

        private static void TryFillRemainingGap(
            List<MealSlot>      daySlots,
            List<PlannedMeal>   dayMeals,
            List<Recipe>        filtered,
            GenerateWeekRequest req,
            ref decimal         dayBudgetLeft,
            ref decimal         weekBudgetLeft)
        {
            if (!dayMeals.Any() || !daySlots.Any()) return;

            int targetKcal = req.AdjustedDailyCalories;
            int threshold  = (int)(targetKcal * CalorieDeficitThreshold);

            var usedRecipeIds = new HashSet<Guid>(dayMeals.Select(m => m.RecipeId));

            for (int i = 0; i < MaxGapFillers; i++)
            {
                int current = dayMeals.Sum(m => (int)(m.Recipe!.CaloriesPerServing * m.Servings));
                int gap     = targetKcal - current;
                if (gap <= threshold) break;

                decimal cap = req.ConsiderBudget ? Math.Min(dayBudgetLeft, weekBudgetLeft) : decimal.MaxValue;

                var pool = filtered.Where(r => FillerPreferredTags.Any(t => r.HasTag(t))).ToList();
                if (!pool.Any()) pool = filtered;

                var candidate = pool
                    .Where(r => !usedRecipeIds.Contains(r.Id))
                    .Where(r => !req.ConsiderBudget || r.TotalCost <= cap)
                    .OrderBy(r => Math.Abs((double)r.CaloriesPerServing - gap))
                    .FirstOrDefault();

                if (candidate == null) break;

                // Докидываем туда, где меньше всего блюд — чтобы не перегружать один слот
                var slot = daySlots.OrderBy(s => s.Items.Count).First();

                var filler = new PlannedMeal
                {
                    Id         = Guid.NewGuid(),
                    MealSlotId = slot.Id,
                    MealSlot   = slot,
                    Role       = RecipeTag.None,
                    RecipeId   = candidate.Id,
                    Recipe     = candidate,
                    Servings   = 1
                };

                slot.Items.Add(filler);
                dayMeals.Add(filler);
                usedRecipeIds.Add(candidate.Id);

                if (req.ConsiderBudget)
                {
                    dayBudgetLeft  -= candidate.TotalCost;
                    weekBudgetLeft -= candidate.TotalCost;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Scoring
        // ══════════════════════════════════════════════════════════════════════

        private static double ComputeScore(
            Recipe              r,
            RecipeStats         stats,
            int                 targetKcal,
            GenerateWeekRequest req)
        {
            double wKcal = req.ConsiderBudget ? WKcalBudget : 1.0;
            double wCost = req.ConsiderBudget ? WCostBudget : 0.0;

            double kcalDiff  = Math.Abs((double)r.CaloriesPerServing - targetKcal);
            double kcalScore = 1.0 - Math.Min(kcalDiff / stats.KcalRange, 1.0);
            double costScore = 1.0 - Math.Min(
                ((double)r.TotalCost - stats.CostMin) / stats.CostRange, 1.0);

            return wKcal * kcalScore + wCost * costScore;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Диетические ограничения
        // ══════════════════════════════════════════════════════════════════════

        private static bool MatchesDiet(Recipe r, GenerateWeekRequest req)
        {
            var names = r.Ingredients
                .Select(i => i.Product.Name.ToLowerInvariant())
                .ToList();

            if (req.Vegetarian || req.Vegan)
            {
                var meat = new[] { "курица", "говядина", "свинина", "баранина", "индейка", "рыба", "тунец", "лосось", "фарш" };
                if (names.Any(n => meat.Any(k => n.Contains(k)))) return false;
            }
            if (req.Vegan)
            {
                var animal = new[] { "яйцо", "молоко", "сливки", "сыр", "творог", "масло сливочное", "мёд", "кефир" };
                if (names.Any(n => animal.Any(k => n.Contains(k)))) return false;
            }
            if (req.GlutenFree)
            {
                var gluten = new[] { "мука", "хлеб", "макароны", "пшеница", "манка" };
                if (names.Any(n => gluten.Any(k => n.Contains(k)))) return false;
            }
            if (req.LowCarb && r.CaloriesPerServing > 0)
            {
                double v = (double)r.CarbsPerServing / (double)r.CaloriesPerServing * 100.0;
                if (v > LowCarbMaxCarbsPer100Kcal) return false;
            }
            if (req.HighProtein && r.CaloriesPerServing > 0)
            {
                double v = (double)r.ProteinPerServing / (double)r.CaloriesPerServing * 100.0;
                if (v < HighProteinMinProteinPer100Kcal) return false;
            }
            if (req.LowFat && r.CaloriesPerServing > 0)
            {
                double v = (double)r.FatPerServing / (double)r.CaloriesPerServing * 100.0;
                if (v > LowFatMaxFatPer100Kcal) return false;
            }
            return true;
        }

        private static bool HasExcluded(Recipe r, HashSet<string> excluded)
        {
            if (!excluded.Any()) return false;
            return r.Ingredients.Any(i =>
                excluded.Any(ex => i.Product.Name.ToLowerInvariant().Contains(ex)));
        }

        // ══════════════════════════════════════════════════════════════════════
        // Утилиты
        // ══════════════════════════════════════════════════════════════════════

        private static RecipeStats ComputeStats(List<Recipe> recipes)
        {
            double kcalMin = double.MaxValue, kcalMax = double.MinValue;
            double costMin = double.MaxValue, costMax = double.MinValue;
            foreach (var r in recipes)
            {
                double k = (double)r.CaloriesPerServing;
                double c = (double)r.TotalCost;
                if (k < kcalMin) kcalMin = k; if (k > kcalMax) kcalMax = k;
                if (c < costMin) costMin = c; if (c > costMax) costMax = c;
            }
            return new RecipeStats(kcalMin, Math.Max(kcalMax - kcalMin, 1.0),
                                   costMin, Math.Max(costMax - costMin, 0.01));
        }

        private static DateTime GetMonday()
        {
            var today = DateTime.Today;
            return today.AddDays(-(((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
        }

        private record RecipeStats(double KcalMin, double KcalRange, double CostMin, double CostRange);
    }
}