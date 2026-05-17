using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.UserService.Services.Interfaces;

namespace Presentation.Controllers;

public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;
    private readonly IUserService               _userService;
    private readonly IFavoriteService           _favoriteService;

    public AccountController(
        ILogger<AccountController> logger,
        IUserService               userService,
        IFavoriteService           favoriteService)
    {
        _logger          = logger;
        _userService     = userService;
        _favoriteService = favoriteService;
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

        ViewBag.CurrentUser = user;
        ViewBag.Favorites   = favorites;

        ViewData["Title"] = "Аккаунт";
        return View();
    }
}