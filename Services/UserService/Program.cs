// using System.Security.Claims;
// using System.Text;
// using Common.Middleware;
// using Microsoft.AspNetCore.Authentication.Cookies;
// using Microsoft.AspNetCore.Authentication.Google;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging;
// using Microsoft.IdentityModel.Tokens;
// using Microsoft.OpenApi;
// using Services.UserService.Data;
// using UserService;
//
// var builder = WebApplication.CreateBuilder(args);
//
// // Добавление конфигурации, включая User Secrets
// builder.Configuration.AddUserSecrets<Program>();
//
// // Регистрация DbContext с подключением к базе данных
// builder.Services.AddDbContext<UserServiceContext>(options =>
// {
//     options.UseNpgsql(builder.Configuration.GetConnectionString("UserDatabase"));
// });
//
// builder.Services.AddHttpContextAccessor();
//
// // Добавление контроллеров
// builder.Services.AddControllers();
// builder.Services.AddEndpointsApiExplorer();
//
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new OpenApiInfo
//     {
//         Title = "UserService API",
//         Version = "v1",
//         Description = "API для управления пользователями и аутентификацией"
//     });
//     c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//     {
//         In = ParameterLocation.Header,
//         Description = "Введите JWT-токен в формате 'Bearer {token}'",
//         Name = "Authorization",
//         Type = SecuritySchemeType.ApiKey,
//         Scheme = "Bearer"
//     });
//     c.AddSecurityRequirement(new OpenApiSecurityRequirement
//     {
//         {
//             new OpenApiSecurityScheme
//             {
//                 Reference = new OpenApiReference
//                 {
//                     Type = ReferenceType.SecurityScheme,
//                     Id = "Bearer"
//                 },
//                 Scheme = "oauth2",
//                 Name = "Bearer",
//                 In = ParameterLocation.Header,
//             },
//             new List<string>()
//         }
//     });
// });
//
// // Настройка CORS
// var allowedOrigins = new[] {
//     "http://localhost:5208",
//     "http://localhost:3000",
//     "http://localhost:5000",
//     "http://localhost:5001",
//     "http://localhost:5002",
//     "http://localhost:5003",
//     "http://localhost:5004"
// };
//
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("SharedCORS", policy =>
//     {
//         policy
//             .WithOrigins(allowedOrigins)
//             .AllowAnyHeader()
//             .AllowAnyMethod()
//             .AllowCredentials();
//     });
// });
//
// // Настройка аутентификации
// var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "8YzN+R7vK9mQ4xJ6wE2pL5sF3hB0nC7uT1yV8oI9rA6dG4kM3jP2lZ5qW8eR7tY0uI9oP6aS3dF5gH8jK1lQ4wE";
// var key = Encoding.UTF8.GetBytes(jwtSecretKey);
//
// builder.Services.AddAuthentication(options =>
//     {
//         options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//         options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//         options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//     })
//     .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
//     {
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuerSigningKey = true,
//             IssuerSigningKey = new SymmetricSecurityKey(key),
//             ValidateIssuer = true,
//             ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "UserService",
//             ValidateAudience = true,
//             ValidAudience = builder.Configuration["Jwt:Audience"] ?? "WebApp",
//             ValidateLifetime = true,
//             ClockSkew = TimeSpan.Zero
//         };
//     })
//     .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
//     {
//         options.LoginPath = "/api/auth/google";
//         options.AccessDeniedPath = "/access-denied";
//         options.Cookie.SameSite = SameSiteMode.Lax;
//         options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
//         options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
//     })
//     .AddGoogle(GoogleDefaults.AuthenticationScheme, googleOptions =>
//     {
//         googleOptions.ClientId = builder.Configuration["Google:ClientId"]
//                                  ?? throw new InvalidOperationException("Google ClientId не настроен");
//         googleOptions.ClientSecret = builder.Configuration["Google:ClientSecret"]
//                                      ?? throw new InvalidOperationException("Google ClientSecret не настроен");
//
//         googleOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//         
//         googleOptions.CallbackPath = "/api/auth/google/callback";
//     
//         googleOptions.Scope.Add("email");
//         googleOptions.Scope.Add("profile");
//         
//         googleOptions.Events.OnCreatingTicket = context =>
//         {
//             var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
//             logger.LogInformation("Google OAuth ticket created for user: {Email}", 
//                 context.Principal?.FindFirst(ClaimTypes.Email)?.Value);
//             return Task.CompletedTask;
//         };
//     });
//
// builder.Services.AddAuthorization();
//
// // Регистрация сервисов
// builder.Services.AddApplicationServices();
//
// var app = builder.Build();
//
// app.UseCors("SharedCORS");
//
// app.UseStaticFiles();
//
// app.UseRouting();
//
// // Настройка Swagger UI
// app.UseSwagger();
// app.UseSwaggerUI(c =>
// {
//     c.SwaggerEndpoint("/swagger/v1/swagger.json", "RouteService API v1");
//     c.RoutePrefix = string.Empty;
// });
//
// app.UseAuthentication();
// app.UseAuthorization();
//
// app.MapControllers();
//
// // Миграция базы данных
// using (var scope = app.Services.CreateScope())
// {
//     var context = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
//     try
//     {
//         context.Database.Migrate();
//         var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
//         logger.LogInformation("Database migration completed successfully");
//     }
//     catch (Exception ex)
//     {
//         var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
//         logger.LogError(ex, "Ошибка при миграции базы данных");
//     }
// }
//
// app.Run();