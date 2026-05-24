using System.Security.Claims;
using Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTO;
using Services.Helpers;
using Services.Interfaces;

namespace Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService              _userService;
    private readonly IJwtService               _jwtService;
    private readonly IConfiguration            _config;      // ← NEW
    private readonly ILogger<AuthController>   _log;         // ← NEW

    public AuthController(
        IUserService            userService,
        IJwtService             jwtService,
        IConfiguration          config,
        ILogger<AuthController> log)
    {
        _userService = userService;
        _jwtService  = jwtService;
        _config      = config;
        _log         = log;
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
            _log.LogError(ex, "[Register]");
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
            _log.LogError(ex, "[Login]");
            return StatusCode(500, new { message = "Ошибка при входе" });
        }
    }

    // ── Google OAuth ──────────────────────────────────────────────────────
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme);
        var properties  = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync("Identity.External");
            if (!result.Succeeded)
                result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded || result.Principal == null)
            {
                _log.LogWarning("[GoogleCallback] Auth failed: {Err}", result.Failure?.Message);
                return Redirect("/account/authpage?error=google_auth_failed");
            }

            var claims   = result.Principal.Claims.ToList();
            var email    = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
            var name     = claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            var googleId = claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(email))
                return Redirect("/account/authpage?error=no_email");

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
            }

            await SignInUserAsync(user);
            await HttpContext.SignOutAsync("Identity.External");

            return Redirect("/account");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[GoogleCallback]");
            return Redirect("/account/authpage?error=server_error");
        }
    }

    // ── Активация Admin-роли ──────────────────────────────────────────────
    // Секретный код хранится в appsettings.json: "AdminSecret": "ВашКод"
    [HttpPost("activate-admin")]
    public async Task<IActionResult> ActivateAdmin([FromBody] ActivateAdminRequest request)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { message = "Войдите в аккаунт" });
        
        try
        {
            var expected = _config["AdminSecret"];

            if (string.IsNullOrWhiteSpace(expected))
            {
                _log.LogError("[ActivateAdmin] AdminSecret не задан в конфиге!");
                return StatusCode(500, new { message = "Сервер не настроен" });
            }

            if (request.SecretCode != expected)
            {
                _log.LogWarning("[ActivateAdmin] Неверный код. UserId={Id}", GetUserIdFromClaims());
                return BadRequest(new { message = "Неверный код активации" });
            }

            var userId = GetUserIdFromClaims();
            var user   = await _userService.GetByIdAsync(userId);

            if (user == null)
                return NotFound(new { message = "Пользователь не найден" });

            if (user.Role == UserRole.Admin)
                return Ok(new { message = "Вы уже администратор", alreadyAdmin = true });

            // Роль в БД — навсегда
            user.Role = UserRole.Admin;
            await _userService.UpdateAsync(user);

            _log.LogInformation("[ActivateAdmin] Admin выдан: {Id} ({Email})", user.Id, user.Email);

            // Перевыписываем cookie — чтобы сразу работало без перелогина
            await SignInUserAsync(user);

            return Ok(new { message = "Добро пожаловать, администратор!", success = true });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[ActivateAdmin]");
            return StatusCode(500, new { message = "Внутренняя ошибка сервера" });
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

    // ── /api/auth/me ──────────────────────────────────────────────────────
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
            _log.LogError(ex, "[GetCurrentUser]");
            return StatusCode(500, new { message = "Ошибка получения данных" });
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
        return Ok(new { message = "Пользователь удалён" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name,           user.Username),
            new(ClaimTypes.Email,          user.Email),
            new(ClaimTypes.Role,           user.Role.ToString())   // "User" или "Admin"
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