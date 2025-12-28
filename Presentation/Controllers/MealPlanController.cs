using AutoMapper;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers
{
    public class MealPlanController : Controller
    {
        private readonly IMealPlanService _mealPlanService;
        private readonly IMealTypeService _mealTypeService;
        private readonly IRecipeService _recipeService;
        private readonly IMapper _mapper;

        public MealPlanController(
            IMealPlanService mealPlanService,
            IMealTypeService mealTypeService,
            IRecipeService recipeService,
            IMapper mapper)
        {
            _mealPlanService = mealPlanService;
            _mealTypeService = mealTypeService;
            _recipeService = recipeService;
            _mapper = mapper;
        }

        // GET: /MealPlan/Week — детальная неделя
        public async Task<IActionResult> Week()
        {
            var plan = await _mealPlanService.GetCurrentWeekPlanAsync();

            if (plan == null)
            {
                return View("EmptyWeek");
            }

            var model = new WeekPlanViewModel
            {
                MealPlanId = plan.Id,
                StartDate = plan.StartDate,
                Days = new List<DayPlanViewModel>()
            };

            for (int offset = 0; offset < 7; offset++)
            {
                var date = plan.StartDate.AddDays(offset);
                var dayMeals = plan.Meals
                    .Where(m => m.DayOffset == offset)
                    .OrderBy(m => m.MealType.Order)
                    .ToList();

                model.Days.Add(new DayPlanViewModel
                {
                    Date = date,
                    Meals = _mapper.Map<List<PlannedMealViewModel>>(dayMeals)
                });
            }

            return View(model);
        }

        // GET: /MealPlan/GenerateWeek — форма генерации
        public IActionResult GenerateWeek()
        {
            return View();
        }

        // POST: /MealPlan/GenerateWeek
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateWeek(GenerateWeekViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var allRecipes = await _recipeService.GetAllRecipesAsync();

            // Фильтрация по исключённым продуктам
            var excluded = model.ExcludedProducts?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.ToLowerInvariant())
                .ToHashSet() ?? new HashSet<string>();

            var suitableRecipes = allRecipes
                .Where(r => !r.Ingredients.Any(i => excluded.Contains(i.Product.Name.ToLowerInvariant())))
                .ToList();

            if (model.Vegetarian || model.Vegan)
            {
                suitableRecipes = suitableRecipes
                    .Where(r => !r.Ingredients.Any(i => 
                        i.Product.Name.ToLowerInvariant().Contains("курица") ||
                        i.Product.Name.ToLowerInvariant().Contains("говядина") ||
                        i.Product.Name.ToLowerInvariant().Contains("свинина") ||
                        i.Product.Name.ToLowerInvariant().Contains("рыба")))
                    .ToList();
            }

            if (model.Vegan)
            {
                suitableRecipes = suitableRecipes
                    .Where(r => !r.Ingredients.Any(i => 
                        i.Product.Name.ToLowerInvariant().Contains("яйцо") ||
                        i.Product.Name.ToLowerInvariant().Contains("молоко") ||
                        i.Product.Name.ToLowerInvariant().Contains("сыр") ||
                        i.Product.Name.ToLowerInvariant().Contains("мёд")))
                    .ToList();
            }

            if (!suitableRecipes.Any())
            {
                ModelState.AddModelError("", "Нет подходящих рецептов под ваши предпочтения.");
                return View(model);
            }

            // Целевые калории
            int targetDaily = model.DailyCalories;
            if (model.Goal == "lose") targetDaily -= 400;
            if (model.Goal == "gain") targetDaily += 400;

            int breakfastTarget = (int)(targetDaily * 0.25);
            int lunchTarget = (int)(targetDaily * 0.35);
            int dinnerTarget = (int)(targetDaily * 0.35);
            int snackTarget = (int)(targetDaily * 0.05);

            // Получаем типы приёма пищи из БД
            var mealTypes = await _mealTypeService.GetAllMealTypesAsync();
            var breakfastType = mealTypes.FirstOrDefault(mt => mt.Name == "Завтрак") 
                ?? throw new Exception("Тип 'Завтрак' не найден в БД");
            var lunchType = mealTypes.FirstOrDefault(mt => mt.Name == "Обед") 
                ?? throw new Exception("Тип 'Обед' не найден в БД");
            var dinnerType = mealTypes.FirstOrDefault(mt => mt.Name == "Ужин") 
                ?? throw new Exception("Тип 'Ужин' не найден в БД");
            var snackType = mealTypes.FirstOrDefault(mt => mt.Name == "Перекус");

            // Текущая неделя
            var today = DateTime.Today;
            var monday = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);

            // Удаляем старый план, если был
            var existingPlan = await _mealPlanService.GetCurrentWeekPlanAsync();
            if (existingPlan != null)
            {
                await _mealPlanService.DeleteAsync(existingPlan.Id);
            }
            
            var planName = $"Меню на неделю с {monday:dd MMMM yyyy}";

            var newPlan = new MealPlan 
            { 
                Id = Guid.NewGuid(),
                Name = planName,
                StartDate = monday,
                Meals = new List<PlannedMeal>()
            };

            var usedRecipes = new HashSet<Guid>();

            for (int dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                // Завтрак
                var breakfastRecipe = PickRecipe(suitableRecipes, breakfastTarget, usedRecipes);
                if (breakfastRecipe != null)
                {
                    AddMeal(newPlan, dayOffset, breakfastType, breakfastRecipe, 1);
                    usedRecipes.Add(breakfastRecipe.Id);
                }

                // Обед
                var lunchRecipe = PickRecipe(suitableRecipes, lunchTarget, usedRecipes);
                if (lunchRecipe != null)
                {
                    AddMeal(newPlan, dayOffset, lunchType, lunchRecipe, 1);
                    usedRecipes.Add(lunchRecipe.Id);
                }

                // Ужин
                var dinnerRecipe = PickRecipe(suitableRecipes, dinnerTarget, usedRecipes);
                if (dinnerRecipe != null)
                {
                    AddMeal(newPlan, dayOffset, dinnerType, dinnerRecipe, 1);
                    usedRecipes.Add(dinnerRecipe.Id);
                }

                // Перекус (если калорий достаточно)
                if (targetDaily > 1800 && snackType != null)
                {
                    var snackRecipe = PickRecipe(suitableRecipes, snackTarget, usedRecipes);
                    if (snackRecipe != null)
                    {
                        AddMeal(newPlan, dayOffset, snackType, snackRecipe, 1);
                        usedRecipes.Add(snackRecipe.Id);
                    }
                }
            }

            await _mealPlanService.CreateAsync(newPlan);

            return RedirectToAction("Week");
        }

        // Вспомогательные методы
        private Recipe? PickRecipe(List<Recipe> recipes, int targetCalories, HashSet<Guid> used)
        {
            return recipes
                .Where(r => !used.Contains(r.Id))
                .OrderBy(r => Math.Abs(r.CaloriesPerServing - targetCalories))
                .ThenBy(_ => Guid.NewGuid())
                .FirstOrDefault();
        }

        private void AddMeal(MealPlan plan, int dayOffset, MealType mealType, Recipe recipe, int servings)
        {
            plan.Meals.Add(new PlannedMeal
            {
                Id = Guid.NewGuid(),
                MealPlanId = plan.Id,
                MealTypeId = mealType.Id,
                MealType = mealType,
                RecipeId = recipe.Id,
                Recipe = recipe,
                DayOffset = dayOffset,
                Servings = servings
            });
        }

        // GET: /MealPlan/ShoppingList/{id}
        public async Task<IActionResult> ShoppingList(Guid id)
        {
            var ingredients = await _mealPlanService.GenerateShoppingListAsync(id);

            if (ingredients == null || !ingredients.Any())
            {
                return View("EmptyShoppingList");
            }

            var model = new ShoppingListViewModel
            {
                MealPlanId = id,
                Ingredients = ingredients
                    .GroupBy(i => new { i.ProductId, i.Unit })
                    .Select(g => new ShoppingIngredientViewModel
                    {
                        Product = g.First().Product,
                        Amount = g.Sum(x => x.Amount),
                        Unit = g.Key.Unit,
                        Comment = string.Join("; ", g.Where(x => !string.IsNullOrWhiteSpace(x.Comment)).Select(x => x.Comment).Distinct())
                    })
                    .OrderBy(i => i.Product?.Name)
                    .ToList()
            };

            return View(model);
        }
    }
}