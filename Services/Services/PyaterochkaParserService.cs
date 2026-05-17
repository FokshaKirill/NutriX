// using System.Text.Json;
// using Domain.Entities;
// using Microsoft.Extensions.Logging;
// using Services.Interfaces;
//
// namespace Services.Services;
//
//
// public class PyaterochkaParserService : IPyaterochkaParserService
// {
//     private readonly HttpClient _http;
//     private readonly ILogger<PyaterochkaParserService> _log;
//
//     private const string StoreId = "1000298611";
//
//     public PyaterochkaParserService(HttpClient http, ILogger<PyaterochkaParserService> log)
//     {
//         _http = http;
//         _http.DefaultRequestHeaders.Clear();
//         _http.DefaultRequestHeaders.Add("User-Agent",
//             "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36");
//         _http.DefaultRequestHeaders.Add("Accept",          "application/json");
//         _http.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9");
//         _http.DefaultRequestHeaders.Add("Origin",          "https://5ka.ru");
//         _http.DefaultRequestHeaders.Add("Referer",         "https://5ka.ru/");
//         _http.Timeout = TimeSpan.FromSeconds(30);
//         _log = log;
//     }
//
//     public async Task<List<ParsedProduct>> SearchProductsAsync(string query, int limit = 20)
//     {
//         var results = new List<ParsedProduct>();
//         try
//         {
//             var url = $"https://5ka.ru/api/v2/search/?records_per_page={limit}&page=1&query={Uri.EscapeDataString(query)}&store={StoreId}&format=json";
//             _log.LogInformation("5ka search: {Url}", url);
//
//             var response = await _http.GetAsync(url);
//             if (!response.IsSuccessStatusCode)
//             {
//                 _log.LogWarning("5ka вернула {Code}", response.StatusCode);
//                 return results;
//             }
//
//             var json = await response.Content.ReadAsStringAsync();
//             var doc  = JsonDocument.Parse(json);
//
//             if (!doc.RootElement.TryGetProperty("results", out var arr)) return results;
//
//             foreach (var item in arr.EnumerateArray())
//             {
//                 var p = ParseItem(item);
//                 if (p != null) results.Add(p);
//             }
//         }
//         catch (Exception ex) { _log.LogError(ex, "SearchProductsAsync failed"); }
//         return results;
//     }
//
//     public async Task<List<ParsedProduct>> GetByCategoryAsync(int categoryId, int limit = 50)
//     {
//         var results = new List<ParsedProduct>();
//         var page    = 1;
//
//         try
//         {
//             while (results.Count < limit)
//             {
//                 var perPage  = Math.Min(24, limit - results.Count);
//                 var url      = $"https://5ka.ru/api/v2/search/?records_per_page={perPage}&page={page}&cat={categoryId}&store={StoreId}&format=json";
//                 var response = await _http.GetAsync(url);
//                 if (!response.IsSuccessStatusCode) break;
//
//                 var json  = await response.Content.ReadAsStringAsync();
//                 var doc   = JsonDocument.Parse(json);
//                 if (!doc.RootElement.TryGetProperty("results", out var arr)) break;
//
//                 var items = arr.EnumerateArray().ToList();
//                 if (!items.Any()) break;
//
//                 foreach (var item in items)
//                 {
//                     var p = ParseItem(item);
//                     if (p != null) results.Add(p);
//                 }
//
//                 page++;
//                 await Task.Delay(300);
//             }
//         }
//         catch (Exception ex) { _log.LogError(ex, "GetByCategoryAsync failed"); }
//         return results;
//     }
//
//     private static ParsedProduct? ParseItem(JsonElement item)
//     {
//         try
//         {
//             var name = item.Str("name") ?? item.Str("plu_name");
//             if (string.IsNullOrWhiteSpace(name)) return null;
//
//             var price = 0m;
//             if (item.TryGetProperty("prices", out var prices))
//                 price = prices.Dec("price_reg__min") ?? prices.Dec("price_promo__min") ?? 0m;
//             if (price == 0m) price = item.Dec("price_reg") ?? 0m;
//
//             var image = "";
//             if (item.TryGetProperty("img", out var imgEl))       image = imgEl.GetString() ?? "";
//             if (string.IsNullOrEmpty(image) &&
//                 item.TryGetProperty("images", out var imgs) &&
//                 imgs.ValueKind == JsonValueKind.Array)
//                 image = imgs.EnumerateArray().FirstOrDefault().Str("high") ?? "";
//
//             var (kcal, prot, fat, carb) = ParseNutrients(item);
//             var unit   = NormalizeUnit(item.Str("uom") ?? "г");
//             var weight = item.Dec("weight_netto") ?? item.Dec("net_weight") ?? 100m;
//
//             return new ParsedProduct
//             {
//                 Name            = name.Trim(),
//                 PricePerUnit    = price,
//                 ImageUrl        = image,
//                 Unit            = unit,
//                 WeightGrams     = weight,
//                 CaloriesPer100g = kcal,
//                 ProteinPer100g  = prot,
//                 FatPer100g      = fat,
//                 CarbsPer100g    = carb,
//                 ExternalId      = item.Str("plu_id") ?? item.Str("id") ?? ""
//             };
//         }
//         catch { return null; }
//     }
//
//     private static (decimal kcal, decimal prot, decimal fat, decimal carb) ParseNutrients(JsonElement item)
//     {
//         decimal kcal = 0, prot = 0, fat = 0, carb = 0;
//
//         foreach (var key in new[] { "params", "properties" })
//         {
//             if (!item.TryGetProperty(key, out var el)) continue;
//
//             if (el.ValueKind == JsonValueKind.Array)
//             {
//                 foreach (var p in el.EnumerateArray())
//                 {
//                     var code = p.Str("code")?.ToLower() ?? "";
//                     var val  = p.Dec("value") ?? 0m;
//                     switch (code)
//                     {
//                         case "energy_val" or "calories" or "energy": kcal = val; break;
//                         case "protein"    or "proteins":             prot = val; break;
//                         case "fat"        or "fats":                 fat  = val; break;
//                         case "carbohydrate" or "carbs":              carb = val; break;
//                     }
//                 }
//             }
//             else if (el.ValueKind == JsonValueKind.Object)
//             {
//                 kcal = el.Dec("energy_val") ?? el.Dec("calories") ?? 0m;
//                 prot = el.Dec("protein")    ?? 0m;
//                 fat  = el.Dec("fat")        ?? 0m;
//                 carb = el.Dec("carbohydrate") ?? el.Dec("carbs") ?? 0m;
//             }
//         }
//
//         return (kcal, prot, fat, carb);
//     }
//
//     private static string NormalizeUnit(string unit) => unit.ToLower() switch
//     {
//         "кг" or "kg"          => "кг",
//         "л"  or "l"           => "л",
//         "мл" or "ml"          => "мл",
//         "шт" or "pcs"         => "шт",
//         _                     => "г"
//     };
// }
//
// // ── DTO ──────────────────────────────────────────────────────────────────
// public class ParsedProduct
// {
//     public string  Name            { get; set; } = "";
//     public decimal PricePerUnit    { get; set; }
//     public string  ImageUrl        { get; set; } = "";
//     public string  Unit            { get; set; } = "г";
//     public decimal WeightGrams     { get; set; } = 100;
//     public decimal CaloriesPer100g { get; set; }
//     public decimal ProteinPer100g  { get; set; }
//     public decimal FatPer100g      { get; set; }
//     public decimal CarbsPer100g    { get; set; }
//     public string  ExternalId      { get; set; } = "";
// }
//
// // ── Json helpers ─────────────────────────────────────────────────────────
// internal static class JsonExt
// {
//     public static string? Str(this JsonElement el, string prop) =>
//         el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
//
//     public static decimal? Dec(this JsonElement el, string prop)
//     {
//         if (!el.TryGetProperty(prop, out var v)) return null;
//         return v.ValueKind switch
//         {
//             JsonValueKind.Number => v.GetDecimal(),
//             JsonValueKind.String => decimal.TryParse(v.GetString(), out var d) ? d : null,
//             _ => null
//         };
//     }
// }