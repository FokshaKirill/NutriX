using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers;

public class HomeController : Controller
{
    private readonly IRecipeService   _recipeService;
    private readonly IMealPlanService _mealPlanService;
    private readonly IUserService     _userService;
    private readonly IMapper          _mapper;

    public HomeController(
        IRecipeService   recipeService,
        IMealPlanService mealPlanService,
        IUserService     userService,
        IMapper          mapper)
    {
        _recipeService   = recipeService;
        _mealPlanService = mealPlanService;
        _userService     = userService;
        _mapper          = mapper;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public async Task<IActionResult> Index()
    {
        // ── Последние рецепты ──────────────────────────────────────────
        var allRecipes = await _recipeService.GetAllRecipesAsync();
        var latest     = allRecipes.OrderByDescending(r => r.CreatedAt).Take(8).ToList();
        ViewBag.LatestRecipes = _mapper.Map<List<RecipeListViewModel>>(latest);

        if (User.Identity?.IsAuthenticated == true && CurrentUserId.HasValue)
        {
            var user = await _userService.GetByIdAsync(CurrentUserId.Value);
            ViewBag.CurrentUser = user;

            // ── Цели питания ───────────────────────────────────────────
            ViewBag.CalGoal  = (int)(user?.DailyCalorieGoal ?? 2000);
            ViewBag.ProtGoal = (int)(user?.DailyProteinGoal ?? 120m);
            ViewBag.FatGoal  = (int)(user?.DailyFatGoal     ?? 70m);
            ViewBag.CarbGoal = (int)(user?.DailyCarbsGoal   ?? 250m);

            // ── Меню на сегодня ────────────────────────────────────────
            var todayMeals = await _mealPlanService.GetTodayMealsAsync(CurrentUserId.Value);
            ViewBag.TodayMeals = todayMeals;

            // Группируем PlannedMeal по слотам для _DayMenuBody
            var todaySlots = todayMeals
                .Where(m => m.MealSlot != null)
                .GroupBy(m => m.MealSlot)
                .Select(g => g.Key)
                .OrderBy(s => s.MealType?.Order ?? 0)
                .ToList();

            ViewBag.TodaySlots = todaySlots;
        }

        return View();
    }

    public IActionResult Contact() => View();
    public IActionResult Faq()    => View();
    public IActionResult Privacy() => View();
}