using Microsoft.Extensions.DependencyInjection;
using Services.UserService.Services.Implementations;
using Services.UserService.Services.Interfaces;

namespace Services.UserService;

public static class Initializer
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, Services.Implementations.UserService>();
        services.AddScoped<IJwtService, JwtService>();
        return services;
    }
}