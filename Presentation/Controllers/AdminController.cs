using Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Services;

namespace Presentation.Controllers;

[Route("Admin/[action]")]
public class AdminController : Controller
{
    private readonly IUsdaFoodService         _usda;
    private readonly IProductService          _products;
    private readonly ILogger<AdminController> _log;

    // Популярные запросы — кнопки быстрого импорта
    private static readonly Dictionary<string, string> QuickImports = new()
    {
        { "🥛 Молочные",    "milk dairy cheese yogurt"     },
        { "🍗 Мясо/птица",  "chicken beef pork turkey"     },
        { "🐟 Рыба",        "salmon tuna fish shrimp"      },
        { "🥦 Овощи",       "vegetable carrot potato onion tomato" },
        { "🍎 Фрукты",      "apple banana orange fruit"    },
        { "🌾 Крупы",       "rice oats buckwheat pasta"    },
        { "🥚 Яйца/масла",  "egg butter oil"               },
        { "🥜 Орехи/бобы",  "nuts beans lentils chickpeas" },
    };

    public AdminController(
        IUsdaFoodService usda,
        IProductService products,
        ILogger<AdminController> log)
    {
        _usda     = usda;
        _products = products;
        _log      = log;
    }

    [HttpGet("/Admin/Parser")]
    public IActionResult Parser()
    {
        ViewBag.QuickImports = QuickImports;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ParseSearch(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Json(new { error = "Введите запрос" });

        var parsed = await _usda.SearchAsync(query, limit);
        var saved  = await SaveAsync(parsed);

        return Json(new
        {
            found   = parsed.Count,
            saved,
            skipped = parsed.Count - saved,
            preview = parsed.Take(10).Select(p => new
            {
                p.Name,
                kcal = $"{p.CaloriesPer100g} ккал",
                bjy  = $"Б{p.ProteinPer100g} Ж{p.FatPer100g} У{p.CarbsPer100g}"
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> ParseQuick(string label, string queries, int limit = 40)
    {
        var allParsed = new List<FoodProduct>();

        foreach (var q in queries.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = await _usda.SearchAsync(q, limit / 4 + 5);
            allParsed.AddRange(part);
            await Task.Delay(200);
        }

        var saved = await SaveAsync(allParsed);
        return Json(new { group = label, found = allParsed.Count, saved, skipped = allParsed.Count - saved });
    }

    [HttpPost]
    public async Task<IActionResult> ParseAll(int limitPerGroup = 40)
    {
        var results    = new List<object>();
        var totalSaved = 0;

        foreach (var (label, queries) in QuickImports)
        {
            var allParsed = new List<FoodProduct>();
            foreach (var q in queries.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var part = await _usda.SearchAsync(q, limitPerGroup / 4 + 5);
                allParsed.AddRange(part);
                await Task.Delay(300);
            }
            var saved = await SaveAsync(allParsed);
            totalSaved += saved;
            results.Add(new { group = label, found = allParsed.Count, saved });
            _log.LogInformation("Импорт {Label}: найдено {F}, сохранено {S}", label, allParsed.Count, saved);
        }

        return Json(new { totalSaved, groups = results });
    }

    private async Task<int> SaveAsync(List<FoodProduct> parsed)
    {
        var existing = (await _products.GetAllProductsAsync())
            .Select(p => p.Name.Trim().ToLower())
            .ToHashSet();

        var saved = 0;
        foreach (var fp in parsed)
        {
            var key = fp.Name.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(key) || existing.Contains(key)) continue;

            try
            {
                await _products.CreateAsync(new Product
                {
                    Id             = Guid.NewGuid(),
                    Name           = fp.Name,
                    PricePerUnit   = 0,
                    ImageUrl       = fp.ImageUrl,
                    Unit           = "г",
                    CaloriesPer100 = fp.CaloriesPer100g > 0 ? fp.CaloriesPer100g : null,
                    ProteinPer100  = fp.ProteinPer100g  > 0 ? fp.ProteinPer100g  : null,
                    FatPer100      = fp.FatPer100g      > 0 ? fp.FatPer100g      : null,
                    CarbsPer100    = fp.CarbsPer100g    > 0 ? fp.CarbsPer100g    : null,
                });
                existing.Add(key);
                saved++;
            }
            catch (Exception ex) { _log.LogWarning(ex, "Не сохранён: {Name}", fp.Name); }
        }
        return saved;
    }
}