using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers;

public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;

    public AccountController(ILogger<AccountController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult AuthPage()
    {
        ViewData["Title"] = "Страница аутентификации";
        
        // Если уже авторизован — сразу на аккаунт
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Account");
        }
        
        // Показать ошибку если пришла из Google OAuth
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

    [AllowAnonymous] // JWT проверяется на клиенте — страница доступна всем
    public IActionResult Account()
    {
        ViewData["Title"] = "Аккаунт";
        
        // Токен может прийти из Google OAuth редиректа
        var token = Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            HttpContext.Session.SetString("AuthToken", token);
            // Редирект чтобы убрать токен из URL
            return RedirectToAction("Account");
        }

        return View();
    }
}