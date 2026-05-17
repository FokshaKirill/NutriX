using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services.Services;

/// <summary>
/// Парсер продуктов через USDA FoodData Central API.
///
/// БЕСПЛАТНЫЙ ключ за 30 секунд:
///   1. Зайди на https://fdc.nal.usda.gov/api-guide.html
///   2. Нажми "Get an API Key" → введи email → ключ придёт сразу на экране
///   3. Добавь в appsettings.json: "Usda": { "ApiKey": "ВАШ_КЛЮЧ" }
///
/// Без ключа работает с DEMO_KEY (лимит: 30 запросов/час).
/// </summary>
public interface IUsdaFoodService
{
    Task<List<FoodProduct>> SearchAsync(string query, int limit = 50);
    Task<List<FoodProduct>> GetByFoodTypeAsync(string foodType, int limit = 50);
}

public class UsdaFoodService : IUsdaFoodService
{
    private readonly HttpClient    _http;
    private readonly ILogger<UsdaFoodService> _log;
    private readonly string        _apiKey;

    private const string Base = "https://api.nal.usda.gov/fdc/v1";

    public UsdaFoodService(HttpClient http, IConfiguration config, ILogger<UsdaFoodService> log)
    {
        _http   = http;
        _log    = log;
        _apiKey = config["Usda:ApiKey"] ?? "DEMO_KEY";

        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("User-Agent", "VNutri/1.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ── Поиск по названию ──────────────────────────────────────────────
    public async Task<List<FoodProduct>> SearchAsync(string query, int limit = 50)
    {
        var results = new List<FoodProduct>();
        try
        {
            // dataType=Foundation — базовые продукты с лучшими данными по КБЖУ
            // SR Legacy — старая база, тоже хорошая
            var url = $"{Base}/foods/search" +
                      $"?query={Uri.EscapeDataString(query)}" +
                      $"&dataType=Foundation,SR%20Legacy,Branded" +
                      $"&pageSize={Math.Min(limit, 200)}" +
                      $"&pageNumber=1" +
                      $"&api_key={_apiKey}";

            _log.LogInformation("USDA search: {Query}", query);
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("USDA вернул {Code}", response.StatusCode);
                return results;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc  = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("foods", out var foods)) return results;

            foreach (var item in foods.EnumerateArray())
            {
                var p = ParseFood(item);
                if (p != null) results.Add(p);
            }

            _log.LogInformation("USDA: найдено {Count} для '{Query}'", results.Count, query);
        }
        catch (Exception ex) { _log.LogError(ex, "USDA SearchAsync failed"); }
        return results;
    }

    // ── По типу продукта ───────────────────────────────────────────────
    public async Task<List<FoodProduct>> GetByFoodTypeAsync(string foodType, int limit = 50)
        => await SearchAsync(foodType, limit);

    // ── Парсинг одного продукта ────────────────────────────────────────
    private static FoodProduct? ParseFood(JsonElement item)
    {
        try
        {
            var name = item.Str("description") ?? item.Str("lowercaseDescription");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120) return null;

            // Переводим английское название в читабельное (базовый маппинг)
            name = TranslateCommon(name.Trim());

            // Нутриенты: массив foodNutrients
            decimal kcal = 0, prot = 0, fat = 0, carb = 0;

            if (item.TryGetProperty("foodNutrients", out var nutrients))
            {
                foreach (var n in nutrients.EnumerateArray())
                {
                    // nutrientId — стандартные коды USDA
                    var id  = n.IntVal("nutrientId") ?? n.IntVal("nutrientNumber") ?? 0;
                    var val = n.Dec("value") ?? 0m;

                    switch (id)
                    {
                        case 1008 or 208: kcal = val; break; // Energy (kcal)
                        case 1003 or 203: prot = val; break; // Protein
                        case 1004 or 204: fat  = val; break; // Total fat
                        case 1005 or 205: carb = val; break; // Carbohydrates
                    }
                }
            }

            // Пропускаем если нет данных
            if (kcal == 0 && prot == 0) return null;

            var image = ""; // USDA не даёт картинки — ок

            return new FoodProduct
            {
                Name            = CapFirst(name),
                CaloriesPer100g = Math.Round(kcal, 1),
                ProteinPer100g  = Math.Round(prot, 1),
                FatPer100g      = Math.Round(fat,  1),
                CarbsPer100g    = Math.Round(carb, 1),
                ImageUrl        = image,
                Unit            = "г",
                PricePerUnit    = 0
            };
        }
        catch { return null; }
    }

    // Базовый перевод самых частых слов
    private static string TranslateCommon(string name)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"milk, whole"            , "Молоко цельное"          },
            {"milk, reduced fat"      , "Молоко 2.5%"             },
            {"chicken, breast"        , "Куриная грудка"          },
            {"chicken breast"         , "Куриная грудка"          },
            {"beef, ground"           , "Говяжий фарш"            },
            {"beef"                   , "Говядина"                },
            {"egg, whole"             , "Яйцо куриное"            },
            {"eggs, whole"            , "Яйца куриные"            },
            {"rice, white"            , "Рис белый"               },
            {"rice, brown"            , "Рис коричневый"          },
            {"oats"                   , "Овсянка"                 },
            {"buckwheat"              , "Гречка"                  },
            {"potato"                 , "Картофель"               },
            {"potatoes"               , "Картофель"               },
            {"tomato"                 , "Помидор"                 },
            {"tomatoes"               , "Помидоры"                },
            {"apple"                  , "Яблоко"                  },
            {"apples"                 , "Яблоки"                  },
            {"banana"                 , "Банан"                   },
            {"bananas"                , "Бананы"                  },
            {"orange"                 , "Апельсин"                },
            {"bread, white"           , "Хлеб белый"              },
            {"bread, whole-wheat"     , "Хлеб цельнозерновой"     },
            {"butter"                 , "Масло сливочное"         },
            {"oil, olive"             , "Масло оливковое"         },
            {"oil, sunflower"         , "Масло подсолнечное"      },
            {"salmon"                 , "Лосось"                  },
            {"tuna"                   , "Тунец"                   },
            {"cheese"                 , "Сыр"                     },
            {"yogurt"                 , "Йогурт"                  },
            {"cottage cheese"         , "Творог"                  },
            {"sugar"                  , "Сахар"                   },
            {"flour, wheat"           , "Мука пшеничная"          },
            {"pasta"                  , "Макароны"                },
            {"carrot"                 , "Морковь"                 },
            {"carrots"                , "Морковь"                 },
            {"onion"                  , "Лук репчатый"            },
            {"garlic"                 , "Чеснок"                  },
            {"cucumber"               , "Огурец"                  },
            {"cabbage"                , "Капуста"                 },
            {"spinach"                , "Шпинат"                  },
            {"lentils"                , "Чечевица"                },
            {"beans"                  , "Фасоль"                  },
            {"chickpeas"              , "Нут"                     },
            {"pork"                   , "Свинина"                 },
            {"turkey"                 , "Индейка"                 },
            {"shrimp"                 , "Креветки"                },
            {"walnuts"                , "Грецкие орехи"           },
            {"almonds"                , "Миндаль"                 },
            {"peanuts"                , "Арахис"                  },
            {"honey"                  , "Мёд"                     },
            {"chocolate, dark"        , "Шоколад тёмный"          },
        };

        // Точное совпадение
        if (map.TryGetValue(name, out var exact)) return exact;

        // Частичное — если название начинается с ключа
        foreach (var (eng, rus) in map)
            if (name.StartsWith(eng, StringComparison.OrdinalIgnoreCase))
                return rus;

        return name; // оставляем английское если перевода нет
    }

    private static string CapFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..].ToLower();
}

// ── DTO ───────────────────────────────────────────────────────────────────
public class FoodProduct
{
    public string  Name            { get; set; } = "";
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g  { get; set; }
    public decimal FatPer100g      { get; set; }
    public decimal CarbsPer100g    { get; set; }
    public string  ImageUrl        { get; set; } = "";
    public string  Unit            { get; set; } = "г";
    public decimal PricePerUnit    { get; set; } = 0;
}

// ── Json helpers ──────────────────────────────────────────────────────────
internal static class JsonExt
{
    public static string? Str(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public static decimal? Dec(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String => decimal.TryParse(v.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null,
            _ => null
        };
    }

    public static int? IntVal(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    }
}