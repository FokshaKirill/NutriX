using Domain.Entities;
using Domain.Enums;
using Services.DTO;
using Services.Interfaces;

namespace Services.Services
{
    /// <summary>
    /// Генерация меню на неделю. Четыре последовательных этапа:
    ///   1. ФИЛЬТРАЦИЯ     — отбор подходящих рецептов
    ///   2. ПОДБОР         — заполнение каждой роли каждого слота по лестнице попыток
    ///   3. SCALE-UP       — докрутка порций если дефицит ккал > 6%
    ///   4. GAP-FILL       — добивка лёгкими блюдами если дефицит всё ещё > 6%
    ///
    /// Исправлены все 4 бага из ТЗ v2.0:
    ///   A. TryBalanceDayCalories / TryFillRemainingGap теперь вызываются в Generate()
    ///   B. usedInDay передаётся в TryFillRemainingGap — нет дублей внутри дня
    ///   C. BudgetStrictness.Flexible реализован через множитель +10%
    ///   D. IsRealisticSingleServing оставлен как фильтр качества данных (по ТЗ §4.2)
    /// </summary>
    public class MealPlanGeneratorService : IMealPlanGeneratorService
    {
        // ── Константы (значения из ТЗ) ────────────────────────────────────────

        private const double KcalWindow             = 0.45;  // ±45% строгое окно
        private const double KcalWindowFallback     = 0.80;  // ±80% расширенное окно
        private const int    MaxServings            = 4;     // максимум порций на блюдо
        private const int    TopCandidatesCount     = 5;     // топ-N для случайного выбора
        private const double CalorieDeficitThreshold = 0.06; // 6% — порог дефицита
        private const int    ScaleUpIterations      = 8;     // итераций scale-up
        private const int    MaxGapFillers          = 3;     // максимум добивочных блюд (ТЗ §4.5)

        // Пороги макронутриентов (г на 100 ккал)
        private const double LowCarbMax    = 10.0;
        private const double HighProteinMin = 7.0;
        private const double LowFatMax     = 3.5;

        // Веса скора
        private const double WKcalOnly   = 1.00;
        private const double WKcalBudget = 0.55;
        private const double WCostBudget = 0.45;

        // Теги предпочтительных блюд для gap-fill (ТЗ §4.5)
        private static readonly RecipeTag[] GapFillTags =
        {
            RecipeTag.Snack, RecipeTag.Drink, RecipeTag.Dessert,
            RecipeTag.Salad, RecipeTag.Garnish
        };

        // Лестница попыток (ТЗ §4.3, таблица)
        private static readonly (double Window, double CooldownMul, double BudgetMul)[] AttemptLadder =
        {
            (KcalWindow,         1.0, 1.0),  // попытка 1: строго
            (KcalWindowFallback, 1.0, 1.0),  // попытка 2: окно ккал шире
            (KcalWindowFallback, 0.5, 1.3),  // попытка 3: кулдаун слабее, бюджет +30%
            (KcalWindowFallback, 0.0, 2.0),  // попытка 4: бюджет почти не блокирует
        };

        // ─────────────────────────────────────────────────────────────────────

        public MealPlan? Generate(GenerateWeekRequest req)
        {
            var rand = req.Seed.HasValue ? new Random(req.Seed.Value) : new Random();

            // ── ЭТАП 1: Фильтрация ───────────────────────────────────────────
            // IsRealisticSingleServing — фильтр качества данных (ТЗ §4.2):
            // отсеивает рецепты с некорректными КБЖУ (0 ккал, 9999 ккал и т.п.)
            var filtered = req.AllRecipes
                .Where(r => r.Ingredients.Any())
                .Where(r => !HasExcluded(r, req.ExcludedProducts))
                .Where(r => MatchesDiet(r, req))
                .Where(r => r.IsRealisticSingleServing)
                .ToList();

            if (!filtered.Any()) return null;

            var stats     = ComputeStats(filtered);
            var templates = req.CustomSlotTemplates ?? DefaultSlotTemplates.Get(req);

            bool budgetInfeasible = false;
            if (req.ConsiderBudget)
            {
                decimal minDayCost = EstimateMinDayCost(templates, filtered);
                if (req.DailyBudget < minDayCost * 0.85m)
                {
                    budgetInfeasible = true;
                    Console.WriteLine(
                        $"[Generator] WARNING: бюджет {req.DailyBudget:F0}₽/день меньше минимально " +
                        $"возможной стоимости плана ~{minDayCost:F0}₽/день. План будет урезан по калориям.");
                }
            }
            
            var lastUsedDay = new Dictionary<Guid, int>();

            var monday = GetMonday();
            var plan = new MealPlan
            {
                Id        = Guid.NewGuid(),
                Name      = $"Меню на неделю с {monday:dd MMMM yyyy}",
                StartDate = monday,
                UserId    = req.UserId,
                Slots     = new List<MealSlot>()
            };

            decimal totalSpent = 0m;

            for (int day = 0; day < 7; day++)
            {
                // ── БАГ C FIX: BudgetStrictness.Flexible = +10% к дневному лимиту ──
                decimal rawDay = req.ConsiderBudget
                    ? Math.Min(req.DailyBudget, req.WeeklyBudget - totalSpent)
                    : decimal.MaxValue;

                decimal dayBudgetLeft = req.ConsiderBudget
                    ? ApplyBudgetMode(rawDay, req.BudgetMode)
                    : decimal.MaxValue;

                var dayMeals = new List<PlannedMeal>();
                var daySlots = new List<MealSlot>();

                // ── БАГ B FIX: usedInDay отслеживает уникальность рецептов за день ──
                var usedInDay = new HashSet<Guid>();

                // ── ЭТАП 2: Подбор по лестнице попыток ──────────────────────
                foreach (var template in templates)
                {
                    var slot = new MealSlot
                    {
                        Id         = Guid.NewGuid(),
                        MealPlanId = plan.Id,
                        MealPlan   = plan,
                        DayOffset  = day,
                        MealTypeId = template.MealType.Id,
                        MealType   = template.MealType,
                        Items      = new List<PlannedMeal>()
                    };

                    foreach (var role in template.Roles)
                    {
                        // Addon (напиток) — пропускаем по шансу
                        if (role.IsAddon &&
                            (!req.IncludeDrinks || rand.NextDouble() > role.AddonChance))
                            continue;

                        // targetKcal для роли (ТЗ §4.3)
                        int targetKcal = (int)(template.TargetCalories * role.CalorieFraction);

                        // Пул рецептов по тегу (если нет — все рецепты)
                        var tagPool = filtered.Where(r => r.HasTag(role.Tag)).ToList();
                        if (!tagPool.Any()) tagPool = filtered;

                        PlannedMeal? meal = null;

                        foreach (var (window, cooldownMul, budgetMul) in AttemptLadder)
                        {
                            decimal effectiveBudgetLeft = req.ConsiderBudget
                                ? dayBudgetLeft * (decimal)budgetMul
                                : decimal.MaxValue;

                            var candidates = GetCandidates(
                                tagPool, req, targetKcal, window,
                                day, lastUsedDay, usedInDay, effectiveBudgetLeft, cooldownMul);

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

                            if (req.ConsiderBudget)
                            {
                                dayBudgetLeft -= recipe.CostPerServing;
                                totalSpent    += recipe.CostPerServing;
                            }
                            
                            break;
                        }

                        if (meal == null)
                        {
                            if (role.IsRequired)
                                Console.WriteLine(
                                    $"[Generator] День {day}, слот {template.MealType.Name}, " +
                                    $"роль {role.Tag}: рецепт не найден после всех попыток.");
                            continue;
                        }

                        slot.Items.Add(meal);
                        dayMeals.Add(meal);
                        usedInDay.Add(meal.RecipeId);    // регистрируем на уровне дня
                        lastUsedDay[meal.RecipeId] = day;
                    }

                    if (slot.Items.Any())
                    {
                        plan.Slots.Add(slot);
                        daySlots.Add(slot);
                    }
                }

                // ── БАГ A FIX: вызываем оба метода в конце каждого дня ──────

                // ЭТАП 3: Scale-up — докрутка порций
                TryBalanceDayCalories(dayMeals, req, ref dayBudgetLeft, ref totalSpent);

                // ЭТАП 4: Gap-fill — добивка лёгкими блюдами
                // usedInDay передаём чтобы не дублировать рецепты внутри дня (баг B)
                TryFillRemainingGap(
                    daySlots, dayMeals, filtered, req,
                    usedInDay, ref dayBudgetLeft, ref totalSpent);
            }

            plan.BudgetWasInfeasible = budgetInfeasible;
            
            // ── Валидация плана ──────────────────────────────────────────────
            var validator = new MealPlanValidator();
            var validation = validator.Validate(
                plan,
                req.AdjustedDailyCalories,
                req.ConsiderBudget ? req.WeeklyBudget : 0);

            if (!validation.IsValid)
            {
                Console.WriteLine($"[Generator] Валидация плана: {validation.Errors.Count} ошибок");
                foreach (var err in validation.Errors)
                    Console.WriteLine($"  ! {err}");
            }


            Console.WriteLine(
                $"[Generator] План: {validation.TotalPlannedKcal} ккал/нед " +
                $"(~{validation.TotalPlannedKcal / 7} ккал/день), " +
                $"стоимость: {validation.TotalPlannedCost:F2}");

            return plan;
        }

        // ══════════════════════════════════════════════════════════════════════
        // БАГ C: ApplyBudgetMode — реализация BudgetStrictness
        // ══════════════════════════════════════════════════════════════════════

        private static decimal ApplyBudgetMode(decimal rawDayBudget, BudgetStrictness mode) =>
            mode switch
            {
                BudgetStrictness.Flexible => rawDayBudget * 1.10m, // +10% допуск
                BudgetStrictness.Ignore   => decimal.MaxValue,
                _                         => rawDayBudget           // Strict — без допуска
            };

        // ══════════════════════════════════════════════════════════════════════
        // GetCandidates: фильтрация с учётом окна ккал, кулдауна, бюджета
        // Использует usedInDay вместо usedInSlot — нет дублей внутри дня
        // ══════════════════════════════════════════════════════════════════════

        private static List<Recipe> GetCandidates(
            List<Recipe>          recipes,
            GenerateWeekRequest   req,
            int                   targetKcal,
            double                window,
            int                   day,
            Dictionary<Guid, int> lastUsedDay,
            HashSet<Guid>         usedInDay,
            decimal               dayBudgetLeft,
            double                cooldownMul)
        {
            double low  = targetKcal * (1.0 - window);
            double high = targetKcal * (1.0 + window);

            // DiversityLevel enum value = кулдаун в днях (Low=1, Normal=3, High=5)
            int baseCooldown = req.MinDaysBetweenRepeats > 0
                ? req.MinDaysBetweenRepeats
                : (int)req.Diversity;

            int cooldown = (int)Math.Round(baseCooldown * cooldownMul);

            return recipes
                .Where(r => (double)r.CaloriesPerServing >= low
                         && (double)r.CaloriesPerServing <= high)
                .Where(r => !usedInDay.Contains(r.Id))    // уникальность внутри дня
                .Where(r => cooldown <= 0
                         || !lastUsedDay.TryGetValue(r.Id, out int last)
                         || (day - last) >= cooldown)
                .Where(r => !req.ConsiderBudget || r.CostPerServing <= dayBudgetLeft)
                .ToList();
        }

        // ══════════════════════════════════════════════════════════════════════
        // PickFromTopN: топ-5 по скору → случайный выбор
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
        // ЭТАП 3: Scale-up — увеличение порций (ТЗ §4.4)
        // Приоритет: Ужин > Обед > Завтрак > Перекус
        // Максимум MaxServings порций, максимум ScaleUpIterations итераций
        // Нельзя превысить цель + threshold
        // ══════════════════════════════════════════════════════════════════════

        private static void TryBalanceDayCalories(
            List<PlannedMeal>   dayMeals,
            GenerateWeekRequest req,
            ref decimal         dayBudgetLeft,
            ref decimal         totalSpent)
        {
            if (!dayMeals.Any()) return;

            int target    = req.AdjustedDailyCalories;
            int threshold = (int)(target * CalorieDeficitThreshold);

            var priority = new[] { "Ужин", "Обед", "Завтрак", "Перекус" };

            for (int iter = 0; iter < ScaleUpIterations; iter++)
            {
                int current = dayMeals.Sum(m => (int)(m.Recipe!.CaloriesPerServing * m.Servings));
                if (target - current <= threshold) break;

                int cur = current;
                decimal budgetLeftLocal = dayBudgetLeft;
                decimal totalSpentLocal = totalSpent;

                var candidate = priority
                    .SelectMany(name => dayMeals.Where(m => m.MealSlot?.MealType?.Name == name))
                    .FirstOrDefault(m =>
                        m.Servings < MaxServings
                        // не превысить цель + порог (чтобы не уйти сильно в плюс)
                        && (cur + (double)m.Recipe!.CaloriesPerServing) <= target + threshold
                        && (!req.ConsiderBudget || m.Recipe!.CostPerServing <= budgetLeftLocal)
                        && (!req.ConsiderBudget || totalSpentLocal + m.Recipe!.CostPerServing <= req.WeeklyBudget));

                if (candidate == null) break;

                candidate.Servings++;
                if (req.ConsiderBudget)
                {
                    dayBudgetLeft -= candidate.Recipe!.CostPerServing;
                    totalSpent    += candidate.Recipe!.CostPerServing;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ЭТАП 4: Gap-fill — добивка лёгкими блюдами (ТЗ §4.5)
        // БАГ B FIX: принимает usedInDay — исключает дубли рецептов внутри дня
        // Добавляет в слот с наименьшим количеством позиций (балансировка)
        // ══════════════════════════════════════════════════════════════════════

        private static void TryFillRemainingGap(
            List<MealSlot>      daySlots,
            List<PlannedMeal>   dayMeals,
            List<Recipe>        filtered,
            GenerateWeekRequest req,
            HashSet<Guid>       usedInDay,   // баг B: передаём из Generate()
            ref decimal         dayBudgetLeft,
            ref decimal         totalSpent)
        {
            if (!dayMeals.Any() || !daySlots.Any()) return;
            if (req.ConsiderBudget && totalSpent >= req.WeeklyBudget) return;

            int target    = req.AdjustedDailyCalories;
            int threshold = (int)(target * CalorieDeficitThreshold);

            for (int i = 0; i < MaxGapFillers; i++)
            {
                int current = dayMeals.Sum(m => (int)(m.Recipe!.CaloriesPerServing * m.Servings));
                int gap     = target - current;
                if (gap <= threshold) break;

                decimal cap = req.ConsiderBudget
                    ? Math.Min(dayBudgetLeft, req.WeeklyBudget - totalSpent)
                    : decimal.MaxValue;

                if (req.ConsiderBudget && cap <= 0) break;

                // Пул — предпочтительно лёгкие блюда, иначе все
                var pool = filtered.Where(r => GapFillTags.Any(t => r.HasTag(t))).ToList();
                if (!pool.Any()) pool = filtered;

                int gapCopy = gap;

                // Основной поиск: блюдо ≤ gap + threshold (не уйти сильно в плюс)
                var pick = pool
                    .Where(r => !usedInDay.Contains(r.Id))
                    .Where(r => !req.ConsiderBudget || r.CostPerServing <= cap)
                    .Where(r => (double)r.CaloriesPerServing <= gapCopy + threshold)
                    .OrderBy(r => Math.Abs((double)r.CaloriesPerServing - gapCopy))
                    .FirstOrDefault();

                // Fallback: допускаем блюдо до gap * 1.3 (строже, было 1.5)
                if (pick == null)
                {
                    pick = pool
                        .Where(r => !usedInDay.Contains(r.Id))
                        .Where(r => !req.ConsiderBudget || r.CostPerServing <= cap)
                        .Where(r => (double)r.CaloriesPerServing <= gapCopy * 1.3)
                        .OrderBy(r => Math.Abs((double)r.CaloriesPerServing - gapCopy))
                        .FirstOrDefault();
                }

                if (pick == null) break;

                // Добавляем в слот с наименьшим количеством блюд (балансировка)
                var targetSlot = daySlots.OrderBy(s => s.Items.Count).First();

                var filler = new PlannedMeal
                {
                    Id         = Guid.NewGuid(),
                    MealSlotId = targetSlot.Id,
                    MealSlot   = targetSlot,
                    Role       = RecipeTag.None,
                    RecipeId   = pick.Id,
                    Recipe     = pick,
                    Servings   = 1
                };

                targetSlot.Items.Add(filler);
                dayMeals.Add(filler);
                usedInDay.Add(pick.Id);    // регистрируем чтобы не добавить дважды

                if (req.ConsiderBudget)
                {
                    dayBudgetLeft -= pick.CostPerServing;
                    totalSpent    += pick.CostPerServing;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Скоринг (ТЗ §6)
        // ══════════════════════════════════════════════════════════════════════

        private static double ComputeScore(
            Recipe r, RecipeStats stats, int targetKcal, GenerateWeekRequest req)
        {
            double kcalDiff  = Math.Abs((double)r.CaloriesPerServing - targetKcal);
            double kcalScore = 1.0 - Math.Min(kcalDiff / stats.KcalRange, 1.0);

            if (!req.ConsiderBudget)
                return WKcalOnly * kcalScore;

            double costScore = 1.0 - Math.Min(
                ((double)r.CostPerServing - stats.CostMin) / stats.CostRange, 1.0);

            // НОВОЕ: эффективность — ккал на рубль, нормированная по максимуму в пуле
            double efficiency = (double)r.CaloriesPerServing / Math.Max((double)r.CostPerServing, 1.0);
            double effScore   = Math.Min(efficiency / stats.MaxEfficiency, 1.0);

            return 0.45 * kcalScore + 0.35 * costScore + 0.20 * effScore;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Диетические ограничения (ТЗ §5)
        // ══════════════════════════════════════════════════════════════════════

        private static bool MatchesDiet(Recipe r, GenerateWeekRequest req)
        {
            var names = r.Ingredients
                .Select(i => i.Product.Name.ToLowerInvariant())
                .ToList();

            if (req.Vegetarian || req.Vegan)
            {
                var meat = new[]
                    { "курица", "говядина", "свинина", "баранина",
                      "индейка", "рыба", "тунец", "лосось", "фарш" };
                if (names.Any(n => meat.Any(k => n.Contains(k)))) return false;
            }
            if (req.Vegan)
            {
                var animal = new[]
                    { "яйцо", "молоко", "сливки", "сыр", "творог",
                      "масло сливочное", "мёд", "кефир" };
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
                if (v > LowCarbMax) return false;
            }
            if (req.HighProtein && r.CaloriesPerServing > 0)
            {
                double v = (double)r.ProteinPerServing / (double)r.CaloriesPerServing * 100.0;
                if (v < HighProteinMin) return false;
            }
            if (req.LowFat && r.CaloriesPerServing > 0)
            {
                double v = (double)r.FatPerServing / (double)r.CaloriesPerServing * 100.0;
                if (v > LowFatMax) return false;
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

        private record RecipeStats(
            double KcalMin, double KcalRange,
            double CostMin, double CostRange,
            double MaxEfficiency);

        private static RecipeStats ComputeStats(List<Recipe> recipes)
        {
            double kcalMin = double.MaxValue, kcalMax = double.MinValue;
            double costMin = double.MaxValue, costMax = double.MinValue;
            double maxEff  = 0.01;
            foreach (var r in recipes)
            {
                double k = (double)r.CaloriesPerServing;
                double c = (double)r.CostPerServing;
                if (k < kcalMin) kcalMin = k;
                if (k > kcalMax) kcalMax = k;
                if (c < costMin) costMin = c;
                if (c > costMax) costMax = c;
                double eff = k / Math.Max(c, 1.0);
                if (eff > maxEff) maxEff = eff;
            }
            return new RecipeStats(
                kcalMin, Math.Max(kcalMax - kcalMin, 1.0),
                costMin, Math.Max(costMax - costMin, 0.01),
                maxEff);
        }
        
        private static decimal EstimateMinDayCost(List<SlotTemplate> templates, List<Recipe> filtered)
        {
            decimal total = 0m;
            foreach (var template in templates)
            foreach (var role in template.Roles)
            {
                if (role.IsAddon) continue; // напитки не обязательны
                var pool = filtered.Where(r => r.HasTag(role.Tag)).ToList();
                if (!pool.Any()) pool = filtered;
                total += pool.Min(r => r.CostPerServing);
            }
            return total;
        }

        private static DateTime GetMonday()
        {
            var today = DateTime.Today;
            return today.AddDays(-(((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
        }
    }
}