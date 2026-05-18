using System.Security.Claims;
using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.DTO;
using Services.Interfaces;
using Services.UserService.Services.Interfaces;

namespace Presentation.Controllers
{
    public class MealPlanController : Controller
    {
        private readonly IMealPlanService _mealPlanService;
        private readonly IMealTypeService _mealTypeService;
        private readonly IRecipeService   _recipeService;
        private readonly IUserService     _userService;      
        private readonly IMapper          _mapper;
        private readonly IMealPlanGeneratorService _generator;

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
                Days       = new List<DayPlanViewModel>()
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
                    // Pre-fill полей из сохранённого профиля
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
        // После генерации — сохраняем цели питания в профиль User
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
        UserId               = CurrentUserId.Value,
        DailyCalories        = model.DailyCalories,
        Goal                 = model.Goal,
        ConsiderBudget       = model.ConsiderBudget,
        WeeklyBudget         = model.WeeklyBudget,
        Vegetarian           = model.Vegetarian,
        Vegan                = model.Vegan,
        GlutenFree           = model.GlutenFree,
        LowCarb              = model.LowCarb,
        HighProtein          = model.HighProtein,
        LowFat               = model.LowFat,
        ExcludedProducts     = model.GetExcludedSet(),
        MealsPerDay          = model.MealsPerDay,
        IncludeSnacks        = model.IncludeSnacks,
        MinDaysBetweenRepeats = 2,
        AllRecipes           = allRecipes.ToList(),
        BreakfastType        = breakfast,
        LunchType            = lunch,
        DinnerType           = dinner,
        SnackType            = snack,
    };

    var newPlan = _generator.Generate(request);

    if (newPlan == null)
    {
        ModelState.AddModelError("", "Нет подходящих рецептов. Попробуйте смягчить ограничения.");
        return View(model);
    }

    // Удаляем старый план
    var existing = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
    if (existing != null)
        await _mealPlanService.DeleteAsync(existing.Id);

    await _mealPlanService.CreateAsync(newPlan);

    // Сохраняем цели в профиль
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
    catch { /* план уже создан, не прерываем */ }

    TempData["GenerateSuccess"] = "true";
    return RedirectToAction("Week");
}

        // ══════════════════════════════════════════════════════════════════════
        // POST /MealPlan/ReplaceMeal
        // ══════════════════════════════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> ReplaceMeal(
            Guid    plannedMealId,
            bool    considerBudget = false,
            decimal weeklyBudget   = 0)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();

            var meal = await _mealPlanService.GetPlannedMealByIdAsync(plannedMealId);
            if (meal == null) return NotFound();

            var currentKcal = meal.Recipe?.CaloriesPerServing ?? 0;
            var currentCost = meal.Recipe?.TotalCost         ?? 0;

            var allRecipes = await _recipeService.GetAllRecipesAsync();

            var plan    = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
            var usedIds = plan?.Meals.Select(m => m.RecipeId).OfType<Guid>().ToHashSet()
                          ?? new HashSet<Guid>();

            var candidates = allRecipes
                .Where(r => r.Ingredients.Any())
                .Where(r => r.Id != meal.RecipeId && !usedIds.Contains(r.Id))
                .ToList();

            if (!candidates.Any())
                return Json(new { error = "Нет подходящего рецепта для замены" });

            var stats = ComputeStats(candidates);

            var budgetLimit = considerBudget && currentCost > 0
                ? currentCost * 1.3m
                : decimal.MaxValue;

            var fakeModel = new GenerateWeekViewModel
            {
                ConsiderBudget = considerBudget,
                WeeklyBudget   = weeklyBudget > 0 ? weeklyBudget : 3500m,
                AvoidRepeats   = true
            };

            var replacement = PickBest(
                candidates, stats, (int)currentKcal,
                fakeModel, usedIds, budgetLimit, new Random(),
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
        // АЛГОРИТМ  (комментарии оставлены для диплома)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Вычисляет диапазоны калорий и стоимости по коллекции рецептов.
        /// Выполняется один раз перед циклом генерации для стабильной нормализации.
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
                CostRange: Math.Max(costMax - costMin, 0.01)
            );
        }

        /// <summary>
        /// Двухкритериальный выбор рецепта: калорийность + стоимость.
        ///
        /// score = w_kcal × kcal_score + w_cost × cost_score + noise
        ///
        /// kcal_score = 1 − |kcal − target| / kcal_range          ∈ [0, 1]
        /// cost_score = 1 − (cost − cost_min) / cost_range         ∈ [0, 1]  (генерация)
        ///            = 1 − |cost − preferCost| / cost_range        ∈ [0, 1]  (замена)
        ///
        /// Веса при considerBudget=false: w_kcal=1.00, w_cost=0.00
        /// Веса при considerBudget=true:  w_kcal=0.55, w_cost=0.45
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

                double costScore;
                if (preferCostNear >= 0)
                {
                    double costDiff = Math.Abs((double)(r.TotalCost - preferCostNear));
                    costScore = 1.0 - Math.Min(costDiff / stats.CostRange, 1.0);
                }
                else
                {
                    costScore = 1.0 - Math.Min(
                        ((double)r.TotalCost - stats.CostMin) / stats.CostRange, 1.0);
                }

                double score = wKcal * kcalScore
                             + wCost * costScore
                             + rand.NextDouble() * 0.04;

                if (score > bestScore) { bestScore = score; best = r; }
            }

            return best;
        }

        private static bool HasExcludedIngredients(Recipe r, HashSet<string> excluded)
        {
            if (!excluded.Any()) return false;
            return r.Ingredients.Any(i =>
                excluded.Any(ex => i.Product.Name.ToLowerInvariant().Contains(ex)));
        }

        private static bool MatchesDiet(Recipe r, GenerateWeekViewModel req)
        {
            var names = r.Ingredients.Select(i => i.Product.Name.ToLowerInvariant()).ToList();
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
            return true;
        }

        private static void AddMeal(MealPlan plan, int dayOffset, MealType mealType, Recipe recipe)
        {
            plan.Meals.Add(new PlannedMeal
            {
                Id         = Guid.NewGuid(),
                MealPlanId = plan.Id,
                MealTypeId = mealType.Id,
                MealType   = mealType,
                RecipeId   = recipe.Id,
                Recipe     = recipe,
                DayOffset  = dayOffset,
                Servings   = 1
            });
        }

        private static MealType RequireMealType(IEnumerable<MealType> types, string name) =>
            types.FirstOrDefault(mt => mt.Name == name)
            ?? throw new InvalidOperationException($"Тип приёма пищи '{name}' не найден в БД.");

        private record RecipeStats(double KcalMin, double KcalRange, double CostMin, double CostRange);
    }
}