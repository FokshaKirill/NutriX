using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Domain.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;

namespace Services.Helpers
{
    public class PyaterochkaParser
    {
        private readonly HttpClient _http;
        private const string PythonBase = "http://127.0.0.1:8765";

        public string StoreId { get; private set; } = "";

        public PyaterochkaParser()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<bool> InitializeStoreAsync(double lat = 0, double lon = 0)
        {
            try
            {
                var resp = await _http.GetAsync($"{PythonBase}/init");
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                StoreId = doc.RootElement.GetProperty("storeId").GetString() ?? "2237";
                Console.WriteLine($"[5ka] Магазин: {StoreId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[5ka] Ошибка инициализации: {ex.Message}");
                StoreId = "2237";
                return false;
            }
        }

        public async Task<string> ProxyGetAsync(string relativeUrl)
        {
            // Парсим category_id и page из relativeUrl для перенаправления к Python
            var uri = new Uri("http://x/" + relativeUrl);
            var qs  = System.Web.HttpUtility.ParseQueryString(uri.Query);

            var categoryId = qs["category_id"] ?? "";
            var page       = qs["page"] ?? "1";

            var resp = await _http.GetAsync(
                $"{PythonBase}/products?category_id={Uri.EscapeDataString(categoryId)}&page={page}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Python сервис вернул {(int)resp.StatusCode}. Тело: {body[..Math.Min(body.Length, 300)]}");
            }

            return await resp.Content.ReadAsStringAsync();
        }

        public async Task<List<Product>> GetProductsAsync(string categorySlug, int maxPages = 3)
        {
            if (string.IsNullOrEmpty(StoreId)) await InitializeStoreAsync();
            var products = new List<Product>();

            for (int page = 1; page <= maxPages; page++)
            {
                var json = await ProxyGetAsync(
                    $"catalog/products_list/?category_id={categorySlug}&page={page}&records_per_page=48");

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("products", out var arr)) break;

                foreach (var item in arr.EnumerateArray())
                {
                    var p = ParseProduct(item);
                    if (p != null) products.Add(p);
                }
                if (arr.GetArrayLength() < 48) break;
                await Task.Delay(400);
            }
            return products;
        }

        private Product ParseProduct(JsonElement elem)
        {
            try
            {
                var name = elem.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name)) return null;

                decimal price = 0;
                if (elem.TryGetProperty("prices", out var prices) &&
                    prices.TryGetProperty("price_reg__min", out var reg) &&
                    reg.ValueKind != JsonValueKind.Null)
                    price = reg.GetDecimal();

                string image = null;
                if (elem.TryGetProperty("main_image", out var img) && img.ValueKind == JsonValueKind.String)
                    image = img.GetString();
                if (!string.IsNullOrEmpty(image) && !image.StartsWith("http"))
                    image = "https:" + image;

                return new Product
                {
                    Name         = Regex.Replace(WebUtility.HtmlDecode(name), @"\s+", " ").Trim(),
                    PricePerUnit = price,
                    Unit         = GetUnit(elem),
                    ImageUrl     = image,
                    ExternalId   = elem.TryGetProperty("plu", out var plu) ? plu.GetString() : null
                };
            }
            catch { return null; }
        }

        private static string GetUnit(JsonElement elem)
        {
            if (!elem.TryGetProperty("measure", out var m)) return "шт";
            var u = m.GetString()?.ToLower() ?? "";
            if (u.Contains("кг"))                       return "кг";
            if (u.Contains("г") && !u.Contains("кг"))   return "г";
            if (u.Contains("мл"))                        return "мл";
            if (u.Contains("л") && !u.Contains("мл"))   return "л";
            return "шт";
        }
    }
}