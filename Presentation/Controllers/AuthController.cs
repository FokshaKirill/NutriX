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
    private readonly IJwtService _jwtService;

    public AuthController(IUserService userService, IJwtService jwtService)
    {
        _userService = userService;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Все поля обязательны" });

            if (request.Password.Length < 6)
                return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });

            var existingUser = await _userService.GetByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest(new { message = "Пользователь с таким email уже существует" });

            var hashedPassword = PasswordHasher.HashPassword(request.Password);

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hashedPassword,
                Role = UserRole.User,
                SubscriptionType = SubscriptionType.Free,
                IsGoogleUser = false,
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            
            await _userService.CreateAsync(user);
        
            await SignInUserAsync(user); 

            return Ok(new { 
                message = "Регистрация прошла успешно",
                user = new { id = user.Id, username = user.Username, email = user.Email }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка при регистрации" });
        }
    }
    
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

            return Ok(new { 
                message = "Вход выполнен успешно",
                user = new { id = user.Id, username = user.Username, email = user.Email }
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Ошибка при входе" });
        }
    }
    
    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTime.UtcNow.AddDays(14) });
    }
    
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme);
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
    
        // Явно вызываем Google, а не DefaultChallenge
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                var error = result.Failure?.Message ?? "Неизвестная ошибка";
                Console.WriteLine($"Google auth failed: {error}");
                return Redirect("/account?error=google_auth_failed");
            }

            var claims = result.Principal.Claims;
            var email = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            var googleId = claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(email))
            {
                return Redirect("/account?error=no_email");
            }

            var user = await _userService.GetByEmailAsync(email);
            if (user == null)
            {
                user = new User
                {
                    Username = name ?? email.Split('@')[0],
                    Email = email,
                    GoogleId = googleId,
                    Role = UserRole.User,
                    SubscriptionType = SubscriptionType.Free,
                    IsGoogleUser = true,
                    IsEmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };
                await _userService.CreateAsync(user);
            }
            else
            {
                user.LastLoginAt = DateTime.UtcNow;
                if (string.IsNullOrWhiteSpace(user.GoogleId))
                {
                    user.GoogleId = googleId;
                    user.IsGoogleUser = true;
                }
                await _userService.UpdateAsync(user);
            }
            
            await SignInUserAsync(user); 

            var token = _jwtService.GenerateJwtToken(user);
            return Redirect($"/account?token={token}");
        }
        catch (Exception ex)
        {
            return Redirect("/account?error=server_error");
        }
    }

    // Остальные методы остаются без изменений
    [HttpGet("callback")]
    public IActionResult Callback([FromQuery] string token)
    {
        return Redirect($"/account?token={token}");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = GetUserIdFromToken();
            var user = await _userService.GetByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound(new { message = "Пользователь не найден" });
            }

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                role = user.Role.ToString(),
                subscriptionType = user.SubscriptionType.ToString(),
                isGoogleUser = user.IsGoogleUser,
                isEmailConfirmed = user.IsEmailConfirmed,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,
                subscriptionExpiresAt = user.SubscriptionExpiresAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка получения данных пользователя" });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    
        return Ok(new { message = "Выход выполнен" });
    }

    [HttpGet("check")]
    public IActionResult Check()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        return Ok(new { isAuthenticated });
    }
    
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllAsync();

            var result = users.Select(user => new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                role = user.Role.ToString(),
                subscriptionType = user.SubscriptionType.ToString(),
                isGoogleUser = user.IsGoogleUser,
                isEmailConfirmed = user.IsEmailConfirmed,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,
                subscriptionExpiresAt = user.SubscriptionExpiresAt
            });

            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Ошибка получения списка пользователей" });
        }
    }

    [HttpDelete("delete/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Пользователь не найден" });
            }

            await _userService.DeleteAsync(id);
            return Ok(new { message = "Пользователь успешно удалён" });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Ошибка при удалении пользователя" });
        }
    }
    
    private Guid GetUserIdFromToken()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdClaim.Value);
    }
}

// TODO: ПОсле регистрации Гугл не сохраняется пользотватель, а значит проблема может быть и в JWT сервисах хотя я их не трогал