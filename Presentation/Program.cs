using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Presentation;
using Presentation.Helpers;
using Services.Interfaces;
using Services.Jobs;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Подключаем DbContext из Infrastructure
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IMealTypeService, MealTypeService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<IMealPlanGeneratorService, MealPlanGeneratorService>();

builder.Services.AddScoped<INutritionSummaryService, NutritionSummaryService>();

builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<PriceUpdateJob>();

builder.Services.AddHttpClient<IRecipeParserService, RecipeParserService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
 
builder.Services.AddHangfireServer();

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; 
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/account/authpage";
        options.Cookie.SameSite    = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        options.CallbackPath = "/api/auth/google/callback";
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/account/authpage?error=google_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Hangfire Dashboard (только для разработки или для админов)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminFilter() }
});
 
RecurringJob.AddOrUpdate<PriceUpdateJob>(
    "update-prices-every-5-days",
    job => job.ExecuteAsync(5m, 15m),
    "0 3 */5 * *" 
);

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DatabaseContext>();
        context.Database.Migrate();
        
        if (!context.MealTypes.Any())
        {
            var mealTypes = new[]
            {
                new MealType { Id = Guid.NewGuid(), Name = "Завтрак", Order = 0 },
                new MealType { Id = Guid.NewGuid(), Name = "Обед", Order = 1 },
                new MealType { Id = Guid.NewGuid(), Name = "Ужин", Order = 2 },
                new MealType { Id = Guid.NewGuid(), Name = "Перекус", Order = 3 }
            };
            
            context.MealTypes.AddRange(mealTypes);
        }
        context.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();