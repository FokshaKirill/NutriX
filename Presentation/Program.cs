using System.Security.Claims;
using System.Text;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Presentation.Helpers;
using PuppeteerSharp;
using Services;
using Services.Interfaces;
using Services.Services;
using Services.UserService.Services.Implementations;
using Services.UserService.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Подключаем DbContext из Infrastructure
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IMealTypeService, MealTypeService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IRecipeParserService, RecipeParserService>();

// HttpClient для nutrition API
builder.Services.AddHttpClient<INutritionApiService, NutritionApiService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<IRecipeParserService, RecipeParserService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Настройка аутентификации
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "8YzN+R7vK9mQ4xJ6wE2pL5sF3hB0nC7uT1yV8oI9rA6dG4kM3jP2lZ5qW8eR7tY0uI9oP6aS3dF5gH8jK1lQ4wE";
var key = Encoding.UTF8.GetBytes(jwtSecretKey);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // HTTP localhost
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "UserService",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "WebApp",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/api/auth/google";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // ← HTTP
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Google:ClientId"]!;
        googleOptions.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        googleOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        googleOptions.CallbackPath = "/api/auth/google/callback";
    
        googleOptions.CorrelationCookie.SameSite = SameSiteMode.Lax;
        googleOptions.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None; // ← HTTP
        googleOptions.CorrelationCookie.HttpOnly = true;
        googleOptions.CorrelationCookie.Name = ".AspNetCore.Correlation.Google."; // ← фиксированный префикс
    
        googleOptions.Scope.Add("email");
        googleOptions.Scope.Add("profile");
        googleOptions.SaveTokens = true;
    });

var app = builder.Build();

// Применяем миграции при старте (с обработкой ошибок)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DatabaseContext>();
        context.Database.Migrate(); // Применяет все pending миграции
        
        // Опционально: добавьте seed-данные
        // DatabaseInitializer.Seed(context);
        var mealTypes = new[]
        {
            new MealType { Id = Guid.NewGuid(), Name = "Завтрак", Order = 0 },
            new MealType { Id = Guid.NewGuid(), Name = "Обед", Order = 1 },
            new MealType { Id = Guid.NewGuid(), Name = "Ужин", Order = 2 },
            new MealType { Id = Guid.NewGuid(), Name = "Перекус", Order = 3 }
        };

        context.MealTypes.AddRange(mealTypes);
        context.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        // В продакшене можно добавить уведомление админу и т.п.
    }
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    // Лучше так (современный подход)
    app.UseExceptionHandler("/Home/Error");
    // Или для подробной страницы ошибок разработчика:
    // app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHttpsRedirection(); 
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();          // ← до Authentication
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();