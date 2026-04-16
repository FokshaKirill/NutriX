using System.ComponentModel.DataAnnotations;

namespace Services.UserService.DTO;

public class UserLogin
{
    [EmailAddress]
    public string? Email { get; set; }

    [MinLength(6)]
    public string? Password { get; set; }
}