using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Presentation.Helpers;
using PuppeteerSharp;
using Services;
using Services.Interfaces;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Подключаем DbContext из Infrastructure
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

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
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// app.UseAuthorization(); // Добавьте позже, когда будет авторизация

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();