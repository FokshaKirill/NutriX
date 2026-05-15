using System.Security.Claims;
using Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.UserService.DTO;
using Services.UserService.Services;
using Services.UserService.Services.Interfaces;

namespace Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IJwtService  _jwtService;

    public AuthController(IUserService userService, IJwtService jwtService)
    {
        _userService = userService;
        _jwtService  = jwtService;
    }

    // ── Регистрация ───────────────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Email)    ||
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Все поля обязательны" });

            if (request.Password.Length < 6)
                return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });

            if (await _userService.GetByEmailAsync(request.Email) != null)
                return BadRequest(new { message = "Пользователь с таким email уже существует" });

            var user = new User
            {
                Username         = request.Username,
                Email            = request.Email,
                PasswordHash     = PasswordHasher.HashPassword(request.Password),
                Role             = UserRole.User,
                SubscriptionType = SubscriptionType.Free,
                IsGoogleUser     = false,
                IsEmailConfirmed = false,
                CreatedAt        = DateTime.UtcNow,
                LastLoginAt      = DateTime.UtcNow
            };

            await _userService.CreateAsync(user);
            await SignInUserAsync(user);

            return Ok(new
            {
                message = "Регистрация прошла успешно",
                user    = new { id = user.Id, username = user.Username, email = user.Email }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Register] {ex}");
            return StatusCode(500, new { message = "Ошибка при регистрации" });
        }
    }

    // ── Вход ─────────────────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Email и пароль обязательны" });

            var user = await _userService.GetByEmailAsync(request.Email);
            if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
                return BadRequest(new { message = "Неверный email или пароль" });

            user.LastLoginAt = DateTime.UtcNow;
            await _userService.UpdateAsync(user);
            await SignInUserAsync(user);

            return Ok(new
            {
                message = "Вход выполнен успешно",
                user    = new { id = user.Id, username = user.Username, email = user.Email }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Login] {ex}");
            return StatusCode(500, new { message = "Ошибка при входе" });
        }
    }

    // ── Google: шаг 1 — редирект на Google ───────────────────────────────
    // ВАЖНО: используем промежуточную cookie-схему "External" для хранения
    // данных от Google до момента когда мы их обработаем в callback.
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        // redirectUri — куда Google вернёт пользователя после согласия
        var redirectUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme);

        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

        // Challenge запускает Google OIDC flow
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    // ── Google: шаг 2 — callback от Google ───────────────────────────────
    // Google сюда вернёт code, ASP.NET его обменяет на токены.
    // Данные временно лежат в схеме "Identity.External" (или в cookie).
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            // Читаем результат внешней аутентификации.
            // Схема "Identity.External" — стандартная временная cookie ASP.NET
            // которую Google middleware записывает после успешного callback.
            var result = await HttpContext.AuthenticateAsync("Identity.External");

            // Если не сработало — пробуем прочитать напрямую из Google-схемы.
            // Это нужно если в Program.cs не настроена отдельная External схема.
            if (!result.Succeeded)
            {
                // Fallback: читаем external info иначе
                result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            if (!result.Succeeded || result.Principal == null)
            {
                Console.WriteLine($"[GoogleCallback] Auth failed: {result.Failure?.Message}");
                return Redirect("/account/authpage?error=google_auth_failed");
            }

            var claims = result.Principal.Claims.ToList();

            // Google кладёт email в стандартный ClaimTypes.Email
            var email    = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
            var name     = claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            // Google ID — в NameIdentifier
            var googleId = claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

            Console.WriteLine($"[GoogleCallback] email={email}, name={name}, googleId={googleId}");

            if (string.IsNullOrWhiteSpace(email))
                return Redirect("/account/authpage?error=no_email");

            // Найти или создать пользователя
            var user = await _userService.GetByEmailAsync(email);
            if (user == null)
            {
                user = new User
                {
                    Username         = name ?? email.Split('@')[0],
                    Email            = email,
                    GoogleId         = googleId,
                    Role             = UserRole.User,
                    SubscriptionType = SubscriptionType.Free,
                    IsGoogleUser     = true,
                    IsEmailConfirmed = true,
                    CreatedAt        = DateTime.UtcNow,
                    LastLoginAt      = DateTime.UtcNow
                };
                await _userService.CreateAsync(user);
                Console.WriteLine($"[GoogleCallback] Создан новый пользователь: {user.Id}");
            }
            else
            {
                user.LastLoginAt = DateTime.UtcNow;
                if (string.IsNullOrWhiteSpace(user.GoogleId))
                {
                    user.GoogleId     = googleId;
                    user.IsGoogleUser = true;
                }
                await _userService.UpdateAsync(user);
                Console.WriteLine($"[GoogleCallback] Обновлён: {user.Id}");
            }

            // Записываем нашу Cookie с правильным GUID в NameIdentifier
            await SignInUserAsync(user);

            // Удаляем временную external cookie если она была
            await HttpContext.SignOutAsync("Identity.External");

            return Redirect("/account");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GoogleCallback] Exception: {ex}");
            return Redirect("/account/authpage?error=server_error");
        }
    }

    // ── Выход ─────────────────────────────────────────────────────────────
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Выход выполнен" });
    }

    // ── Текущий пользователь ─────────────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var user = await _userService.GetByIdAsync(GetUserIdFromClaims());
            if (user == null) return NotFound(new { message = "Пользователь не найден" });

            return Ok(new
            {
                id                    = user.Id,
                username              = user.Username,
                email                 = user.Email,
                role                  = user.Role.ToString(),
                subscriptionType      = user.SubscriptionType.ToString(),
                isGoogleUser          = user.IsGoogleUser,
                isEmailConfirmed      = user.IsEmailConfirmed,
                createdAt             = user.CreatedAt,
                lastLoginAt           = user.LastLoginAt,
                subscriptionExpiresAt = user.SubscriptionExpiresAt
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetCurrentUser] {ex}");
            return StatusCode(500, new { message = "Ошибка получения данных пользователя" });
        }
    }

    [HttpGet("check")]
    public IActionResult Check() =>
        Ok(new { isAuthenticated = User.Identity?.IsAuthenticated ?? false });

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users.Select(u => new
        {
            id                    = u.Id,
            username              = u.Username,
            email                 = u.Email,
            role                  = u.Role.ToString(),
            subscriptionType      = u.SubscriptionType.ToString(),
            isGoogleUser          = u.IsGoogleUser,
            isEmailConfirmed      = u.IsEmailConfirmed,
            createdAt             = u.CreatedAt,
            lastLoginAt           = u.LastLoginAt,
            subscriptionExpiresAt = u.SubscriptionExpiresAt
        }));
    }

    [HttpDelete("delete/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null) return NotFound(new { message = "Пользователь не найден" });
        await _userService.DeleteAsync(id);
        return Ok(new { message = "Пользователь успешно удалён" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            // ВАЖНО: NameIdentifier содержит наш GUID, не Google ID
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name,           user.Username),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.Role,           user.Role.ToString())
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTime.UtcNow.AddDays(14)
            });

        HttpContext.Session.SetString("UserId", user.Id.ToString());
    }

    private Guid GetUserIdFromClaims()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("UserId claim not found");
        return Guid.Parse(claim.Value);
    }
}