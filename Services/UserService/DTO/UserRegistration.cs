using System.ComponentModel.DataAnnotations;

namespace Services.UserService.DTO;

public class UserRegistration
{
    public string? Username { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [MinLength(6)]
    public string? Password { get; set; }
}