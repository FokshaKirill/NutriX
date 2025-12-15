using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Presentation.Helpers;
using Services;
using Services.Interfaces;
using Services.Services;

var builder = WebApplication.CreateBuilder(args);

// Удаляем ненужное, если пока не используете
// builder.Services.AddDistributedMemoryCache();
// builder.Services.AddHttpClient();

// Подключаем DbContext из Infrastructure
builder.Services.AddDbContext<DatabaseContext>(options =>
    //options.UseNpgsql(
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAutoMapper(typeof(MappingProfile));

// TODO: Здесь регистрируем репозитории и сервисы приложения. Лушче отдельный экстеншн  builder.Services.AddApplicationServices();
// Пример:
// builder.Services.AddScoped<IProductRepository, ProductRepository>();
// builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
// builder.Services.AddScoped<IMealPlanService, MealPlanService>();

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IMealTypeService, MealTypeService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();

builder.Services.AddControllersWithViews();

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