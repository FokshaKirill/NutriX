using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Services.Interfaces;

namespace Presentation.Controllers;

public class HomeController : Controller
{
    private readonly IRecipeService   _recipeService;
    private readonly INutritionSummaryService _nutritionSummary;
    private readonly IMealPlanService _mealPlanService;
    private readonly IUserService     _userService;
    private readonly IFavoriteService _favoriteService;
    private readonly IMapper          _mapper;

    public HomeController(
        IRecipeService   recipeService,
        INutritionSummaryService nutritionSummary,
        IMealPlanService mealPlanService,
        IUserService     userService,
        IFavoriteService favoriteService,
        IMapper          mapper)
    {
        _recipeService   = recipeService;
        _nutritionSummary = nutritionSummary;
        _mealPlanService = mealPlanService;
        _userService     = userService;
        _favoriteService = favoriteService;
        _mapper          = mapper;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public async Task<IActionResult> Index()
    {
        var allRecipes = await _recipeService.GetAllRecipesAsync();
        var latest     = allRecipes.OrderByDescending(r => r.CreatedAt).Take(8).ToList();
        var latestVms  = _mapper.Map<List<RecipeListViewModel>>(latest);

        if (CurrentUserId.HasValue)
        {
            var favIds = (await _favoriteService.GetFavoritesAsync(CurrentUserId.Value))
                .Select(r => r.Id).ToHashSet();
            foreach (var r in latestVms)
                r.IsFavorite = favIds.Contains(r.Id);
        }

        ViewBag.LatestRecipes = latestVms;

        if (User.Identity?.IsAuthenticated == true && CurrentUserId.HasValue)
        {
            var user = await _userService.GetByIdAsync(CurrentUserId.Value);
            ViewBag.CurrentUser = user;

            ViewBag.CalGoal  = (int)(user?.DailyCalorieGoal ?? 2000);
            ViewBag.ProtGoal = (int)(user?.DailyProteinGoal ?? 120m);
            ViewBag.FatGoal  = (int)(user?.DailyFatGoal     ?? 70m);
            ViewBag.CarbGoal = (int)(user?.DailyCarbsGoal   ?? 250m);

            var todayMeals = await _mealPlanService.GetTodayMealsAsync(CurrentUserId.Value);
            ViewBag.TodayMeals = todayMeals;

            var todaySlots = todayMeals
                .Where(m => m.MealSlot != null)
                .GroupBy(m => m.MealSlot)
                .Select(g => g.Key)
                .OrderBy(s => s.MealType?.Order ?? 0)
                .ToList();
            ViewBag.TodaySlots = todaySlots;

            var summary = await _nutritionSummary.GetTodayAsync(CurrentUserId.Value);
            ViewBag.TodayKcal = summary.Kcal;
            ViewBag.TodayP    = summary.Protein;
            ViewBag.TodayF    = summary.Fat;
            ViewBag.TodayC    = summary.Carbs;
        }

        return View();
    }

    public IActionResult Contact() => View();
    public IActionResult Faq()    => View();
    public IActionResult Privacy() => View();
}