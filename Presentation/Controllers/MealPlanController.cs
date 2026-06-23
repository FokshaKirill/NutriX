using System.Security.Claims;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Presentation.Models;
using Services.DTO;
using Services.Helpers;
using Services.Interfaces;

namespace Presentation.Controllers;

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

    /// <summary>
    /// GET /MealPlan/GetMealTypeId?name=Обед
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMealTypeId(string name)
    {
        var types = await _mealTypeService.GetAllMealTypesAsync();
        var mt    = types.FirstOrDefault(t => t.Name == name);
        if (mt == null) return Json(new { id = (Guid?)null });
        return Json(new { id = mt.Id });
    }
        
    // Обновить порции блюда
    [HttpPost]
    public async Task<IActionResult> UpdateServings(Guid plannedMealId, int servings)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        servings = Math.Max(1, Math.Min(10, servings));
        await _mealPlanService.UpdateServingsAsync(plannedMealId, servings);

        // Возвращаем пересчитанные данные
        var meal = await _mealPlanService.GetPlannedMealByIdAsync(plannedMealId);
        if (meal?.Recipe == null) return NotFound();

        return Json(new {
            success  = true,
            servings,
            calories = (int)Math.Round(meal.Recipe.CaloriesPerServing * servings),
            protein  = (int)Math.Round(meal.Recipe.ProteinPerServing  * servings),
            fat      = (int)Math.Round(meal.Recipe.FatPerServing      * servings),
            carbs    = (int)Math.Round(meal.Recipe.CarbsPerServing    * servings),
            cost     = Math.Round(meal.Recipe.CostPerServing * servings, 2)
        });
    }

    // Удалить блюдо из плана
    [HttpPost]
    public async Task<IActionResult> RemoveMeal(Guid plannedMealId)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        await _mealPlanService.RemoveMealAsync(plannedMealId);
        return Json(new { success = true });
    }
        
        
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
            Plan       = plan
        };

        for (int offset = 0; offset < 7; offset++)
        {
            // Все PlannedMeal этого дня — через слоты
            var dayMeals = plan.Slots
                .Where(s => s.DayOffset == offset)
                .OrderBy(s => s.MealType.Order)
                .SelectMany(s => s.Items)
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
    // ══════════════════════════════════════════════════════════════════════
    public async Task<IActionResult> GenerateWeek()
    {
        var vm = new GenerateWeekViewModel();

        if (CurrentUserId.HasValue)
        {
            var user = await _userService.GetByIdAsync(CurrentUserId.Value);
            if (user != null)
            {
                if (user.DailyCalorieGoal.HasValue) vm.DailyCalories = user.DailyCalorieGoal.Value;
                // if (user.DailyProteinGoal.HasValue) vm.ProteinGoal   = (int)user.DailyProteinGoal.Value;
                // if (user.DailyFatGoal.HasValue)     vm.FatGoal       = (int)user.DailyFatGoal.Value;
                // if (user.DailyCarbsGoal.HasValue)   vm.CarbsGoal     = (int)user.DailyCarbsGoal.Value;
                vm.WeightKg      = user.WeightKg;
                vm.HeightCm      = user.HeightCm;
                vm.Age           = user.Age;
                vm.Gender        = user.Gender;
                vm.ActivityLevel = user.ActivityLevel;

                if (!user.DailyCalorieGoal.HasValue
                    && user.WeightKg.HasValue && user.HeightCm.HasValue && user.Age.HasValue
                    && !string.IsNullOrEmpty(user.Gender) && !string.IsNullOrEmpty(user.ActivityLevel))
                {
                    var bmr  = CalorieCalculator.CalcBmr(user.WeightKg.Value, user.HeightCm.Value, user.Age.Value, user.Gender);
                    var tdee = CalorieCalculator.CalcTdee(bmr, user.ActivityLevel);
                    vm.DailyCalories = tdee;
                }
            }
        }

        return View(vm);
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /MealPlan/GenerateWeek
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateWeek(GenerateWeekViewModel model)
    {
        Console.WriteLine($"[DEBUG] Получено с формы: DailyCalories={model.DailyCalories}, Goal={model.Goal}, MealsPerDay={model.MealsPerDay}, WeeklyBudget={model.WeeklyBudget}, ConsiderBudget={model.ConsiderBudget}");
        
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
            Goal = MapGoal(model.Goal),
            ConsiderBudget        = model.ConsiderBudget,
            WeeklyBudget          = model.WeeklyBudget,
            Vegetarian            = model.Vegetarian,
            Vegan                 = model.Vegan,
            GlutenFree            = model.GlutenFree,
            LowCarb               = model.LowCarb,
            HighProtein           = model.HighProtein,
            IncludeDrinks         = model.IncludeDrinks,
            LowFat                = model.LowFat,
            ExcludedProducts      = model.GetExcludedSet(),
            MealsPerDay           = model.MealsPerDay,
            MinDaysBetweenRepeats = model.AvoidRepeats ? 2 : 0,
            AllRecipes            = allRecipes.ToList(),
            BreakfastType         = breakfast,
            LunchType             = lunch,
            DinnerType            = dinner,
            SnackType             = snack,
            ProteinGoal           = model.ProteinGoal,
            FatGoal               = model.FatGoal,
            CarbsGoal             = model.CarbsGoal,
        };

        var newPlan = _generator.Generate(request);

        if (newPlan == null)
        {
            ModelState.AddModelError("", "Нет подходящих рецептов. Попробуйте смягчить ограничения.");
            return View(model);
        }

        if (newPlan.BudgetWasInfeasible)
        {
            TempData["BudgetWarning"] =
                $"Бюджет {model.WeeklyBudget:F0}₽/нед слишком мал для {model.DailyCalories} ккал/день " +
                $"при {model.MealsPerDay} приёмах пищи в день. План сгенерирован, но калорийность ниже цели. " +
                $"Увеличьте бюджет или уменьшите калорийность для точного соответствия.";
        }

        newPlan.ConsiderBudget   = model.ConsiderBudget;
        newPlan.WeeklyBudget     = model.WeeklyBudget;
        newPlan.Vegetarian       = model.Vegetarian;
        newPlan.Vegan            = model.Vegan;
        newPlan.GlutenFree       = model.GlutenFree;
        newPlan.LowCarb          = model.LowCarb;
        newPlan.HighProtein      = model.HighProtein;
        newPlan.LowFat           = model.LowFat;
        newPlan.ExcludedProducts = model.ExcludedProducts;

        var existing = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
        if (existing != null)
        {
            existing.IsArchived = true;
            await _mealPlanService.UpdateAsync(existing);
        }

        await _mealPlanService.CreateAsync(newPlan);

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
        catch { }

        TempData["GenerateSuccess"] = "true";
        return RedirectToAction("Week");
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /MealPlan/ReplaceMeal
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost]
    public async Task<IActionResult> ReplaceMeal(Guid plannedMealId)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        // PlannedMeal теперь привязан к MealSlot, не к MealPlan напрямую
        var meal = await _mealPlanService.GetPlannedMealByIdAsync(plannedMealId);
        if (meal == null) return NotFound();

        var plan = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);
        if (plan == null) return NotFound();

        var currentKcal = meal.Recipe?.CaloriesPerServing ?? 0;
        var currentCost = meal.Recipe?.CostPerServing ?? 0;
        // Роль блюда — чтобы подбирать замену с тем же тегом
        var role = meal.Role;

        var excluded = string.IsNullOrWhiteSpace(plan.ExcludedProducts)
            ? new HashSet<string>()
            : plan.ExcludedProducts
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet();

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

        // Все уже использованные рецепты в плане
        var usedIds = plan.Slots
            .SelectMany(s => s.Items)
            .Select(m => m.RecipeId)
            .ToHashSet();

        decimal budgetLimit = plan.ConsiderBudget && currentCost > 0
            ? currentCost * 1.3m
            : decimal.MaxValue;

        var candidates = allRecipes
            .Where(r => r.Ingredients.Any())
            .Where(r => r.Id != meal.RecipeId)
            .Where(r => !usedIds.Contains(r.Id))
            .Where(r => !HasExcludedIngredients(r, excluded))
            .Where(r => MatchesDiet(r, dietModel))
            .Where(r => !plan.ConsiderBudget || r.CostPerServing <= budgetLimit)
            // Подбираем замену с тем же тегом роли (если тег задан)
            .Where(r => role == RecipeTag.None || r.HasTag(role))
            .ToList();

        // Если с тегом никого нет — расширяем поиск без фильтра по тегу
        if (!candidates.Any())
        {
            candidates = allRecipes
                .Where(r => r.Ingredients.Any())
                .Where(r => r.Id != meal.RecipeId)
                .Where(r => !usedIds.Contains(r.Id))
                .Where(r => !HasExcludedIngredients(r, excluded))
                .Where(r => MatchesDiet(r, dietModel))
                .Where(r => !plan.ConsiderBudget || r.CostPerServing <= budgetLimit)
                .ToList();
        }

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
            cost     = Math.Round(replacement.CostPerServing, 2)
        });
    }
    
    [HttpPost]
    public async Task<IActionResult> ReplaceMealWithRecipe(Guid plannedMealId, Guid newRecipeId)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        var recipe = await _recipeService.GetByIdAsync(newRecipeId);
        if (recipe == null) return Json(new { error = "Рецепт не найден" });

        await _mealPlanService.ReplaceMealRecipeAsync(plannedMealId, newRecipeId);

        return Json(new
        {
            success  = true,
            recipeId = recipe.Id,
            name     = recipe.Name,
            imageUrl = recipe.ImageUrl ?? "",
            calories = (int)Math.Round(recipe.CaloriesPerServing),
            protein  = (int)Math.Round(recipe.ProteinPerServing),
            fat      = (int)Math.Round(recipe.FatPerServing),
            carbs    = (int)Math.Round(recipe.CarbsPerServing),
            cost     = Math.Round(recipe.CostPerServing, 2)
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // POST /MealPlan/AddMeal
    // ══════════════════════════════════════════════════════════════════════
    [HttpPost]
    public async Task<IActionResult> AddMeal(
        Guid mealPlanId, Guid recipeId, Guid mealTypeId, int dayOffset, int servings = 1)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        var plan = await _mealPlanService.GetByIdAsync(mealPlanId);
        if (plan == null || plan.UserId != CurrentUserId.Value) return NotFound();

        var recipe   = await _recipeService.GetByIdAsync(recipeId);
        var mealType = (await _mealTypeService.GetAllMealTypesAsync())
            .FirstOrDefault(mt => mt.Id == mealTypeId);

        if (recipe == null || mealType == null)
            return BadRequest("Рецепт или тип приёма пищи не найден.");

        // Найти существующий слот или создать новый
        var slot = plan.Slots.FirstOrDefault(s =>
            s.DayOffset == dayOffset && s.MealTypeId == mealTypeId);

        if (slot == null)
        {
            slot = new MealSlot
            {
                Id         = Guid.NewGuid(),
                MealPlanId = plan.Id,
                MealTypeId = mealTypeId, 
                DayOffset  = dayOffset,
                Items      = new List<PlannedMeal>()
            };
            await _mealPlanService.AddSlotAsync(slot);
        }

        bool alreadyInSlot = slot.Items.Any(m => m.RecipeId == recipeId);
        if (alreadyInSlot)
            return Json(new { error = "Этот рецепт уже добавлен в этот приём пищи." });

        var newMeal = new PlannedMeal
        {
            Id         = Guid.NewGuid(),
            MealSlotId = slot.Id,
            Role       = RecipeTag.MainCourse,
            RecipeId   = recipe.Id,
            Servings   = Math.Max(1, servings)
        };
        await _mealPlanService.AddPlannedMealAsync(newMeal);

        return Json(new
        {
            success       = true,
            plannedMealId = newMeal.Id,
            name          = recipe.Name,
            imageUrl      = recipe.ImageUrl ?? "",
            calories      = (int)Math.Round(recipe.CaloriesPerServing * servings),
            protein       = (int)Math.Round(recipe.ProteinPerServing  * servings),
            fat           = (int)Math.Round(recipe.FatPerServing      * servings),
            carbs         = (int)Math.Round(recipe.CarbsPerServing    * servings),
            cost          = Math.Round(recipe.CostPerServing * servings, 2)
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // Хелперы
    // ══════════════════════════════════════════════════════════════════════

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
            if (model.ConsiderBudget && r.CostPerServing > budgetLimit) continue;

            double kcalDiff  = Math.Abs((double)r.CaloriesPerServing - targetKcal);
            double kcalScore = 1.0 - Math.Min(kcalDiff / stats.KcalRange, 1.0);

            double costScore = preferCostNear >= 0
                ? 1.0 - Math.Min(
                    Math.Abs((double)(r.CostPerServing - preferCostNear)) / stats.CostRange, 1.0)
                : 1.0 - Math.Min(
                    ((double)r.CostPerServing - stats.CostMin) / stats.CostRange, 1.0);

            double score = wKcal * kcalScore
                           + wCost * costScore
                           + rand.NextDouble() * 0.04;

            if (score > bestScore) { bestScore = score; best = r; }
        }

        return best;
    }

    private static RecipeStats ComputeStats(List<Recipe> recipes)
    {
        if (!recipes.Any()) return new RecipeStats(0, 1, 0, 0.01);

        double kcalMin = double.MaxValue, kcalMax = double.MinValue;
        double costMin = double.MaxValue, costMax = double.MinValue;

        foreach (var r in recipes)
        {
            double k = (double)r.CaloriesPerServing;
            double c = (double)r.CostPerServing;
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

    private static bool HasExcludedIngredients(Recipe r, HashSet<string> excluded)
    {
        if (!excluded.Any()) return false;
        return r.Ingredients.Any(i =>
            excluded.Any(ex => i.Product.Name.ToLowerInvariant().Contains(ex)));
    }

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
        if (req.LowCarb && r.CaloriesPerServing > 0)
        {
            double v = (double)r.CarbsPerServing / (double)r.CaloriesPerServing * 100.0;
            if (v > 10.0) return false;
        }
        if (req.HighProtein && r.CaloriesPerServing > 0)
        {
            double v = (double)r.ProteinPerServing / (double)r.CaloriesPerServing * 100.0;
            if (v < 7.0) return false;
        }
        if (req.LowFat && r.CaloriesPerServing > 0)
        {
            double v = (double)r.FatPerServing / (double)r.CaloriesPerServing * 100.0;
            if (v > 3.5) return false;
        }
        return true;
    }

    private static MealType RequireMealType(IEnumerable<MealType> types, string name) =>
        types.FirstOrDefault(mt => mt.Name == name)
        ?? throw new InvalidOperationException($"Тип приёма пищи '{name}' не найден в БД.");

    private static NutritionGoal MapGoal(string goal) => goal.ToLowerInvariant() switch
    {
        "lose" => NutritionGoal.Deficit,
        "lose_fast" => NutritionGoal.Deficit,
        "gain" => NutritionGoal.Surplus,
        "gain_lean" => NutritionGoal.Surplus,
        "maintain" => NutritionGoal.Maintain,
        _ => NutritionGoal.Maintain
    };

    private record RecipeStats(double KcalMin, double KcalRange, double CostMin, double CostRange);
    
    public async Task<IActionResult> History()
    {
        if (!CurrentUserId.HasValue) return RedirectToAction("AuthPage", "Account");

        var history = await _mealPlanService.GetHistoryAsync(CurrentUserId.Value);

        var model = history.Select(p => new
        {
            Id        = p.Id,
            StartDate = p.StartDate,
            EndDate   = p.StartDate.AddDays(6),
            TotalKcal = p.Slots.SelectMany(s => s.Items)
                .Sum(m => (int)(m.Recipe?.CaloriesPerServing * m.Servings ?? 0)),
            AvgKcal   = p.Slots.Any()
                ? p.Slots.SelectMany(s => s.Items)
                    .Sum(m => (int)(m.Recipe?.CaloriesPerServing * m.Servings ?? 0)) / 7
                : 0,
            MealCount = p.Slots.SelectMany(s => s.Items).Count(),
            Vegetarian  = p.Vegetarian,
            Vegan       = p.Vegan,
            HighProtein = p.HighProtein,
            LowCarb     = p.LowCarb,
        }).ToList();

        return View(model);
    }

    public async Task<IActionResult> ViewArchived(Guid id)
    {
        if (!CurrentUserId.HasValue) return RedirectToAction("AuthPage", "Account");

        var plan = await _mealPlanService.GetByIdAsync(id);
        if (plan == null || plan.UserId != CurrentUserId.Value) return NotFound();

        var model = new WeekPlanViewModel
        {
            MealPlanId = plan.Id,
            StartDate  = plan.StartDate,
            Plan       = plan,
            Days       = new List<DayPlanViewModel>()
        };

        for (int offset = 0; offset < 7; offset++)
        {
            var dayMeals = plan.Slots
                .Where(s => s.DayOffset == offset)
                .OrderBy(s => s.MealType.Order)
                .SelectMany(s => s.Items)
                .ToList();

            model.Days.Add(new DayPlanViewModel
            {
                Date  = plan.StartDate.AddDays(offset),
                Meals = _mapper.Map<List<PlannedMealViewModel>>(dayMeals)
            });
        }

        ViewBag.IsArchived = true;
        return View("Week", model); 
    }
    
    [HttpPost]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();

        var plan = await _mealPlanService.GetByIdAsync(id);
        if (plan == null || plan.UserId != CurrentUserId.Value) return NotFound();

        await _mealPlanService.DeleteAsync(id);
        return RedirectToAction("History");
    }

    /// <summary>
    /// POST /MealPlan/DeleteAllPlans — удалить все archived-планы пользователя.
    /// Активный план (IsArchived = false) не удаляется.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllPlans()
    {
        if (!CurrentUserId.HasValue) return RedirectToAction("AuthPage", "Account");

        await _mealPlanService.DeleteAllForUserAsync(CurrentUserId.Value);

        TempData["SuccessMessage"] = "Все архивные планы удалены.";
        return RedirectToAction("History");
    }
}
