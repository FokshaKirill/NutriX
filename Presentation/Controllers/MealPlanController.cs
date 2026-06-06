using System.Security.Claims;
using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.DTO;
using Services.Interfaces;

namespace Presentation.Controllers
{
    public class MealPlanController : Controller
    {
        private readonly IMealPlanService             _mealPlanService;
        private readonly IMealTypeService             _mealTypeService;
        private readonly IRecipeService               _recipeService;
        private readonly IUserService                 _userService;
        private readonly IMapper                      _mapper;
        private readonly IMealPlanGeneratorService    _generator;

        public MealPlanController(
            IMealPlanService          mealPlanService,
            IMealTypeService          mealTypeService,
            IRecipeService            recipeService,
            IUserService              userService,
            IMealPlanGeneratorService generator,
            IMapper                   mapper)
        {
            _mealPlanService = mealPlanService;
            _mealTypeService = mealTypeService;
            _recipeService   = recipeService;
            _userService     = userService;
            _generator       = generator;
            _mapper          = mapper;
        }

        private Guid? CurrentUserId =>
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        // ══════════════════════════════════════════════════════════════════════
        // GET /MealPlan/Week
        // ══════════════════════════════════════════════════════════════════════
        public async Task<IActionResult> Week()
        {
            if (!CurrentUserId.HasValue) return RedirectToAction("AuthPage", "Account");

            var plan = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
            if (plan == null) return View("EmptyWeek");

            var model = new WeekPlanViewModel
            {
                MealPlanId = plan.Id,
                StartDate  = plan.StartDate,
                Days       = new List<DayPlanViewModel>(),
                Plan       = plan   // ← добавить
            };


            for (int offset = 0; offset < 7; offset++)
            {
                var dayMeals = plan.Meals
                    .Where(m => m.DayOffset == offset)
                    .OrderBy(m => m.MealType.Order)
                    .ToList();

                model.Days.Add(new DayPlanViewModel
                {
                    Date  = plan.StartDate.AddDays(offset),
                    Meals = _mapper.Map<List<PlannedMealViewModel>>(dayMeals)
                });
            }

            var rawIngredients = await _mealPlanService.GenerateShoppingListAsync(plan.Id);
            if (rawIngredients?.Any() == true)
            {
                model.ShoppingList = rawIngredients
                    .GroupBy(i => new { i.ProductId, i.Unit })
                    .Select(g => new ShoppingIngredientViewModel
                    {
                        Product  = g.First().Product,
                        Amount   = g.Sum(x => x.Amount),
                        Unit     = g.Key.Unit,
                        Comment  = string.Join("; ", g
                            .Where(x => !string.IsNullOrWhiteSpace(x.Comment))
                            .Select(x => x.Comment).Distinct()),
                        Category = g.First().Product?.Category.ToString()
                    })
                    .OrderBy(i => i.Category)
                    .ThenBy(i => i.Product?.Name)
                    .ToList();
            }

            return View(model);
        }

        // ══════════════════════════════════════════════════════════════════════
        // GET /MealPlan/GenerateWeek
        // Pre-fill формы из профиля пользователя если цели уже заданы
        // ══════════════════════════════════════════════════════════════════════
        public async Task<IActionResult> GenerateWeek()
        {
            var vm = new GenerateWeekViewModel();

            if (CurrentUserId.HasValue)
            {
                var user = await _userService.GetByIdAsync(CurrentUserId.Value);
                if (user != null)
                {
                    if (user.DailyCalorieGoal.HasValue)
                        vm.DailyCalories = user.DailyCalorieGoal.Value;

                    if (user.DailyProteinGoal.HasValue)
                        vm.ProteinGoal = (int)user.DailyProteinGoal.Value;

                    if (user.DailyFatGoal.HasValue)
                        vm.FatGoal = (int)user.DailyFatGoal.Value;

                    if (user.DailyCarbsGoal.HasValue)
                        vm.CarbsGoal = (int)user.DailyCarbsGoal.Value;
                }
            }

            return View(vm);
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST /MealPlan/GenerateWeek
        // После генерации сохраняем цели питания в профиль User
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateWeek(GenerateWeekViewModel model)
        {
            if (!CurrentUserId.HasValue) return RedirectToAction("AuthPage", "Account");
            if (!ModelState.IsValid)     return View(model);

            var allRecipes = await _recipeService.GetAllRecipesAsync();
            var mealTypes  = await _mealTypeService.GetAllMealTypesAsync();

            var breakfast = RequireMealType(mealTypes, "Завтрак");
            var lunch     = RequireMealType(mealTypes, "Обед");
            var dinner    = RequireMealType(mealTypes, "Ужин");
            var snack     = mealTypes.FirstOrDefault(mt => mt.Name is "Перекус" or "Снэк");

            var request = new GenerateWeekRequest
            {
                UserId                = CurrentUserId.Value,
                DailyCalories         = model.DailyCalories,
                Goal                  = model.Goal,
                ConsiderBudget        = model.ConsiderBudget,
                WeeklyBudget          = model.WeeklyBudget,
                Vegetarian            = model.Vegetarian,
                Vegan                 = model.Vegan,
                GlutenFree            = model.GlutenFree,
                LowCarb               = model.LowCarb,
                HighProtein           = model.HighProtein,
                LowFat                = model.LowFat,
                ExcludedProducts      = model.GetExcludedSet(),
                MealsPerDay           = model.MealsPerDay,
                IncludeSnacks         = model.IncludeSnacks,
                MinDaysBetweenRepeats = model.AvoidRepeats ? 2 : 0,
                AllRecipes            = allRecipes.ToList(),
                BreakfastType         = breakfast,
                LunchType             = lunch,
                DinnerType            = dinner,
                SnackType             = snack,
            };

            var newPlan = _generator.Generate(request);

            if (newPlan == null)
            {
                ModelState.AddModelError("", "Нет подходящих рецептов. Попробуйте смягчить ограничения.");
                return View(model);
            }

            // Сохраняем настройки генерации в план — они нужны для ReplaceMeal,
            // чтобы замена рецепта использовала те же ограничения.
            newPlan.ConsiderBudget   = model.ConsiderBudget;
            newPlan.WeeklyBudget     = model.WeeklyBudget;
            newPlan.Vegetarian       = model.Vegetarian;
            newPlan.Vegan            = model.Vegan;
            newPlan.GlutenFree       = model.GlutenFree;
            newPlan.LowCarb          = model.LowCarb;
            newPlan.HighProtein      = model.HighProtein;
            newPlan.LowFat           = model.LowFat;
            newPlan.ExcludedProducts = model.ExcludedProducts; // строка "молоко, орехи"

            // Удаляем старый план текущей недели
            var existing = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
            if (existing != null)
                await _mealPlanService.DeleteAsync(existing.Id);

            await _mealPlanService.CreateAsync(newPlan);

            // Сохраняем цели в профиль пользователя
            try
            {
                var user = await _userService.GetByIdAsync(CurrentUserId.Value);
                if (user != null)
                {
                    int adjKcal = request.AdjustedDailyCalories;
                    user.DailyCalorieGoal = adjKcal;
                    user.DailyProteinGoal = model.ProteinGoal.HasValue
                        ? (decimal)model.ProteinGoal.Value
                        : Math.Round((decimal)(adjKcal * 0.30 / 4), 0);
                    user.DailyFatGoal = model.FatGoal.HasValue
                        ? (decimal)model.FatGoal.Value
                        : Math.Round((decimal)(adjKcal * 0.30 / 9), 0);
                    user.DailyCarbsGoal = model.CarbsGoal.HasValue
                        ? (decimal)model.CarbsGoal.Value
                        : Math.Round((decimal)(adjKcal * 0.40 / 4), 0);
                    await _userService.UpdateAsync(user);
                }
            }
            catch { /* план уже создан, не прерываем редирект */ }

            TempData["GenerateSuccess"] = "true";
            return RedirectToAction("Week");
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST /MealPlan/ReplaceMeal
        //
        // Заменяет одно блюдо в плане.
        // Диетические ограничения читаются из самого MealPlan — они были
        // сохранены туда при генерации. Никаких параметров из JS не нужно.
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> ReplaceMeal(Guid plannedMealId)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();

            var meal = await _mealPlanService.GetPlannedMealByIdAsync(plannedMealId);
            if (meal == null) return NotFound();

            // Читаем план — в нём хранятся настройки генерации
            var plan = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
            if (plan == null) return NotFound();

            var currentKcal = meal.Recipe?.CaloriesPerServing ?? 0;
            var currentCost = meal.Recipe?.TotalCost         ?? 0;

            // Исключения — разбираем строку из плана
            var excluded = string.IsNullOrWhiteSpace(plan.ExcludedProducts)
                ? new HashSet<string>()
                : plan.ExcludedProducts
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToHashSet();

            // Собираем модель диеты из сохранённых полей плана
            var dietModel = new GenerateWeekViewModel
            {
                ConsiderBudget = plan.ConsiderBudget,
                WeeklyBudget   = plan.WeeklyBudget,
                AvoidRepeats   = true,
                Vegetarian     = plan.Vegetarian,
                Vegan          = plan.Vegan,
                GlutenFree     = plan.GlutenFree,
                LowCarb        = plan.LowCarb,
                HighProtein    = plan.HighProtein,
                LowFat         = plan.LowFat,
            };

            var allRecipes = await _recipeService.GetAllRecipesAsync();

            var usedIds = plan.Meals
                .Select(m => m.RecipeId)
                .OfType<Guid>()
                .ToHashSet();

            decimal budgetLimit = plan.ConsiderBudget && currentCost > 0
                ? currentCost * 1.3m
                : decimal.MaxValue;

            // ── Фильтрация кандидатов ─────────────────────────────────────────
            // 1. Есть ингредиенты
            // 2. Не тот же рецепт
            // 3. Не в списке уже использованных (AvoidRepeats)
            // 4. Не содержит исключённых продуктов
            // 5. Соответствует диетическим флагам
            // 6. В рамках бюджета
            var candidates = allRecipes
                .Where(r => r.Ingredients.Any())
                .Where(r => r.Id != meal.RecipeId)
                .Where(r => !usedIds.Contains(r.Id))
                .Where(r => !HasExcludedIngredients(r, excluded))
                .Where(r => MatchesDiet(r, dietModel))
                .Where(r => !plan.ConsiderBudget || r.TotalCost <= budgetLimit)
                .ToList();

            if (!candidates.Any())
                return Json(new { error = "Нет подходящего рецепта для замены" });

            var stats = ComputeStats(candidates);

            var replacement = PickBest(
                candidates, stats, (int)currentKcal,
                dietModel, usedIds, budgetLimit, new Random(),
                preferCostNear: currentCost);

            if (replacement == null)
                return Json(new { error = "Нет подходящего рецепта для замены" });

            await _mealPlanService.ReplaceMealRecipeAsync(plannedMealId, replacement.Id);

            return Json(new
            {
                success  = true,
                recipeId = replacement.Id,
                name     = replacement.Name,
                imageUrl = replacement.ImageUrl ?? "",
                calories = (int)Math.Round(replacement.CaloriesPerServing),
                protein  = (int)Math.Round(replacement.ProteinPerServing),
                fat      = (int)Math.Round(replacement.FatPerServing),
                carbs    = (int)Math.Round(replacement.CarbsPerServing),
                cost     = Math.Round(replacement.TotalCost, 2)
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // POST /MealPlan/AddMeal
        //
        // Добавляет произвольное блюдо в план на конкретный день и тип приёма.
        // Используется когда пользователь вручную добавляет блюдо через UI.
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> AddMeal(
            Guid   mealPlanId,
            Guid   recipeId,
            Guid   mealTypeId,
            int    dayOffset,
            int    servings = 1)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();

            var plan = await _mealPlanService.GetByIdAsync(mealPlanId);
            if (plan == null || plan.UserId != CurrentUserId.Value)
                return NotFound();

            var recipe   = await _recipeService.GetByIdAsync(recipeId);
            var mealType = (await _mealTypeService.GetAllMealTypesAsync())
                               .FirstOrDefault(mt => mt.Id == mealTypeId);

            if (recipe == null || mealType == null)
                return BadRequest("Рецепт или тип приёма пищи не найден.");

            // Нельзя добавить тот же рецепт дважды в один день
            bool alreadyInDay = plan.Meals
                .Any(m => m.DayOffset == dayOffset && m.RecipeId == recipeId);

            if (alreadyInDay)
                return Json(new { error = "Этот рецепт уже добавлен в указанный день." });

            var newMeal = CreateMeal(plan.Id, mealType, recipe, dayOffset, servings);
            await _mealPlanService.AddPlannedMealAsync(newMeal);

            return Json(new
            {
                success      = true,
                plannedMealId = newMeal.Id,
                name         = recipe.Name,
                imageUrl     = recipe.ImageUrl ?? "",
                calories     = (int)Math.Round(recipe.CaloriesPerServing * servings),
                protein      = (int)Math.Round(recipe.ProteinPerServing  * servings),
                fat          = (int)Math.Round(recipe.FatPerServing      * servings),
                carbs        = (int)Math.Round(recipe.CarbsPerServing    * servings),
                cost         = Math.Round(recipe.TotalCost * servings, 2)
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Алгоритмические хелперы контроллера
        // (только для ReplaceMeal — основная логика живёт в сервисе)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Двухкритериальный выбор рецепта для замены.
        /// preferCostNear: если ≥ 0, cost_score считается по близости к этой стоимости,
        /// иначе — по дешевизне (стандартный режим генерации).
        /// </summary>
        private static Recipe? PickBest(
            List<Recipe>          recipes,
            RecipeStats           stats,
            int                   targetKcal,
            GenerateWeekViewModel model,
            HashSet<Guid>         used,
            decimal               budgetLimit,
            Random                rand,
            decimal               preferCostNear = -1)
        {
            double wKcal = model.ConsiderBudget ? 0.55 : 1.0;
            double wCost = model.ConsiderBudget ? 0.45 : 0.0;

            Recipe? best      = null;
            double  bestScore = double.MinValue;

            foreach (var r in recipes)
            {
                if (model.AvoidRepeats && used.Contains(r.Id))        continue;
                if (model.ConsiderBudget && r.TotalCost > budgetLimit) continue;

                double kcalDiff  = Math.Abs((double)r.CaloriesPerServing - targetKcal);
                double kcalScore = 1.0 - Math.Min(kcalDiff / stats.KcalRange, 1.0);

                double costScore = preferCostNear >= 0
                    ? 1.0 - Math.Min(
                        Math.Abs((double)(r.TotalCost - preferCostNear)) / stats.CostRange, 1.0)
                    : 1.0 - Math.Min(
                        ((double)r.TotalCost - stats.CostMin) / stats.CostRange, 1.0);

                double score = wKcal * kcalScore
                             + wCost * costScore
                             + rand.NextDouble() * 0.04; // небольшой шум для разнообразия

                if (score > bestScore) { bestScore = score; best = r; }
            }

            return best;
        }

        /// <summary>
        /// Предвычисляет диапазоны калорий/стоимости (нужны для нормализации скора).
        /// </summary>
        private static RecipeStats ComputeStats(List<Recipe> recipes)
        {
            if (!recipes.Any()) return new RecipeStats(0, 1, 0, 0.01);

            double kcalMin = double.MaxValue, kcalMax = double.MinValue;
            double costMin = double.MaxValue, costMax = double.MinValue;

            foreach (var r in recipes)
            {
                double k = (double)r.CaloriesPerServing;
                double c = (double)r.TotalCost;
                if (k < kcalMin) kcalMin = k;
                if (k > kcalMax) kcalMax = k;
                if (c < costMin) costMin = c;
                if (c > costMax) costMax = c;
            }

            return new RecipeStats(
                KcalMin:   kcalMin,
                KcalRange: Math.Max(kcalMax - kcalMin, 1.0),
                CostMin:   costMin,
                CostRange: Math.Max(costMax - costMin, 0.01));
        }

        /// <summary>
        /// Возвращает true если рецепт содержит хотя бы один из исключённых продуктов.
        /// </summary>
        private static bool HasExcludedIngredients(Recipe r, HashSet<string> excluded)
        {
            if (!excluded.Any()) return false;
            return r.Ingredients.Any(i =>
                excluded.Any(ex =>
                    i.Product.Name.ToLowerInvariant().Contains(ex)));
        }

        /// <summary>
        /// Проверяет все диетические флаги из ViewModel.
        /// Полная копия логики сервиса — нужна для фильтрации в ReplaceMeal
        /// без необходимости поднимать полный GenerateWeekRequest.
        /// </summary>
        private static bool MatchesDiet(Recipe r, GenerateWeekViewModel req)
        {
            var names = r.Ingredients
                .Select(i => i.Product.Name.ToLowerInvariant())
                .ToList();

            if (req.Vegetarian || req.Vegan)
            {
                var meat = new[]
                {
                    "курица", "говядина", "свинина", "баранина",
                    "индейка", "рыба", "тунец", "лосось", "фарш"
                };
                if (names.Any(n => meat.Any(k => n.Contains(k)))) return false;
            }

            if (req.Vegan)
            {
                var animal = new[]
                {
                    "яйцо", "молоко", "сливки", "сыр", "творог",
                    "масло сливочное", "мёд", "кефир"
                };
                if (names.Any(n => animal.Any(k => n.Contains(k)))) return false;
            }

            if (req.GlutenFree)
            {
                var gluten = new[] { "мука", "хлеб", "макароны", "пшеница", "манка" };
                if (names.Any(n => gluten.Any(k => n.Contains(k)))) return false;
            }

            // LowCarb / HighProtein / LowFat — по нормализованным показателям на 100 ккал
            if (req.LowCarb && r.CaloriesPerServing > 0)
            {
                double carbsPer100 = (double)r.CarbsPerServing / (double)r.CaloriesPerServing * 100.0;
                if (carbsPer100 > 10.0) return false;
            }

            if (req.HighProtein && r.CaloriesPerServing > 0)
            {
                double protPer100 = (double)r.ProteinPerServing / (double)r.CaloriesPerServing * 100.0;
                if (protPer100 < 7.0) return false;
            }

            if (req.LowFat && r.CaloriesPerServing > 0)
            {
                double fatPer100 = (double)r.FatPerServing / (double)r.CaloriesPerServing * 100.0;
                if (fatPer100 > 3.5) return false;
            }

            return true;
        }

        /// <summary>
        /// Создаёт объект PlannedMeal. Используется в AddMeal.
        /// </summary>
        private static PlannedMeal CreateMeal(
            Guid     planId,
            MealType mealType,
            Recipe   recipe,
            int      dayOffset,
            int      servings = 1) =>
            new()
            {
                Id         = Guid.NewGuid(),
                MealPlanId = planId,
                MealTypeId = mealType.Id,
                MealType   = mealType,
                RecipeId   = recipe.Id,
                Recipe     = recipe,
                DayOffset  = dayOffset,
                Servings   = Math.Max(1, servings)
            };

        private static MealType RequireMealType(IEnumerable<MealType> types, string name) =>
            types.FirstOrDefault(mt => mt.Name == name)
            ?? throw new InvalidOperationException($"Тип приёма пищи '{name}' не найден в БД.");

        private record RecipeStats(double KcalMin, double KcalRange, double CostMin, double CostRange);
    }
}