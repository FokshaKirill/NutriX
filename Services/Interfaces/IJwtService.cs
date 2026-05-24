using System.Security.Claims;

namespace Services.Interfaces;

public interface IJwtService
{
    string GenerateJwtToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
}
