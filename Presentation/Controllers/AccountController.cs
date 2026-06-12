using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Presentation.Controllers;

public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;
    private readonly IUserService               _userService;
    private readonly IFavoriteService           _favoriteService;
    private readonly IRecipeService           _recipeService;
    private readonly IMealPlanService _mealPlanService;

    public AccountController(
        ILogger<AccountController> logger,
        IUserService               userService,
        IFavoriteService           favoriteService,
        IRecipeService             recipeService,
        IMealPlanService           mealPlanService)   // ← добавить
    {
        _logger          = logger;
        _userService     = userService;
        _favoriteService = favoriteService;
        _recipeService   = recipeService;
        _mealPlanService = mealPlanService;           // ← добавить
    }

    private Guid? CurrentUserId
    {
        get
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public IActionResult Index() => RedirectToAction("Account");

    [AllowAnonymous]
    public IActionResult AuthPage()
    {
        ViewData["Title"] = "Страница аутентификации";

        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Account");

        var error = Request.Query["error"].ToString();
        if (!string.IsNullOrEmpty(error))
        {
            ViewData["Error"] = error switch
            {
                "google_auth_failed" => "Ошибка входа через Google",
                "no_email"           => "Google не предоставил email",
                "server_error"       => "Ошибка сервера, попробуйте позже",
                _                    => "Неизвестная ошибка"
            };
        }

        return View();
    }

    [AllowAnonymous]
    public async Task<IActionResult> Account()
    {
        // Если не авторизован — на страницу входа
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("AuthPage");

        // Если пришёл token в query — сохраняем в сессию и редиректим чисто
        var token = Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            HttpContext.Session.SetString("AuthToken", token);
            return RedirectToAction("Account");
        }

        // Загружаем пользователя
        if (!CurrentUserId.HasValue)
            return RedirectToAction("AuthPage");

        var user      = await _userService.GetByIdAsync(CurrentUserId.Value);
        var favorites = await _favoriteService.GetFavoritesAsync(CurrentUserId.Value);
        var myRecipes = await _recipeService.GetByAuthorAsync(CurrentUserId.Value);

        var todayPlan = await _mealPlanService.GetCurrentWeekPlanAsync(CurrentUserId.Value);

        int todayKcal = 0, todayP = 0, todayF = 0, todayC = 0;
        if (todayPlan != null)
        {
            var today = DateTime.Today;
            var diff   = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = today.AddDays(-diff).Date;
            var offset = (today - monday).Days;

            var todayMeals = todayPlan.Slots
                .Where(s => s.DayOffset == offset)
                .SelectMany(s => s.Items)
                .Where(m => m.Recipe != null)
                .ToList();

            todayKcal = (int)Math.Round(todayMeals.Sum(m => m.Recipe!.CaloriesPerServing * m.Servings));
            todayP    = (int)Math.Round(todayMeals.Sum(m => m.Recipe!.ProteinPerServing  * m.Servings));
            todayF    = (int)Math.Round(todayMeals.Sum(m => m.Recipe!.FatPerServing      * m.Servings));
            todayC    = (int)Math.Round(todayMeals.Sum(m => m.Recipe!.CarbsPerServing    * m.Servings));
        }

        ViewBag.TodayKcal = todayKcal;
        ViewBag.TodayP    = todayP;
        ViewBag.TodayF    = todayF;
        ViewBag.TodayC    = todayC;
        ViewBag.CurrentUser = user;
        ViewBag.Favorites   = favorites;
        ViewBag.MyRecipes   = myRecipes;

        ViewData["Title"] = "Аккаунт";
        ViewBag.IsAdmin = User.IsInRole("Admin"); 
        
        return View();
    }
}