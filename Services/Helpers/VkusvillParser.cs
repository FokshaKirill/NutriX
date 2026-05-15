using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Entities;

namespace Services.Helpers
{
    public class VkusvillParser
    {
        private readonly HttpClient _http;

        // ← сюда вставишь IP своего российского VPS
        private const string ServiceBase = "http://ВАШ_VPS_IP:8765";

        public VkusvillParser()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<List<(string Slug, string Name)>> GetCategoriesAsync()
        {
            var resp = await _http.GetAsync($"{ServiceBase}/categories");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var result = new List<(string, string)>();
            foreach (var el in doc.RootElement.EnumerateArray())
                result.Add((el.GetProperty("slug").GetString()!, el.GetProperty("name").GetString()!));
            return result;
        }

        public async Task<string> ProxyGetProductsAsync(string categorySlug, int page = 1)
        {
            var resp = await _http.GetAsync(
                $"{ServiceBase}/products?category={Uri.EscapeDataString(categorySlug)}&page={page}");

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Сервис вернул {(int)resp.StatusCode}. {body[..Math.Min(body.Length, 200)]}");
            }

            return await resp.Content.ReadAsStringAsync();
        }

        public async Task<List<Product>> GetProductsAsync(string categorySlug, int maxPages = 3)
        {
            var products = new List<Product>();

            for (int page = 1; page <= maxPages; page++)
            {
                var json = await ProxyGetProductsAsync(categorySlug, page);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("products", out var arr)) break;

                foreach (var item in arr.EnumerateArray())
                {
                    var p = ParseProduct(item);
                    if (p != null) products.Add(p);
                }

                // Проверяем есть ли следующая страница
                if (!doc.RootElement.TryGetProperty("has_next", out var hasNext) || 
                    !hasNext.GetBoolean()) break;

                await Task.Delay(300);
            }

            return products;
        }

        private static Product? ParseProduct(JsonElement el)
        {
            try
            {
                var name = el.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name)) return null;

                decimal price = 0;
                if (el.TryGetProperty("price", out var p) && p.ValueKind != JsonValueKind.Null)
                    price = p.GetDecimal();

                return new Product
                {
                    Name         = name,
                    PricePerUnit = price,
                    Unit         = el.TryGetProperty("unit", out var u) ? u.GetString() ?? "шт" : "шт",
                    ImageUrl     = el.TryGetProperty("img", out var img) ? img.GetString() : null,
                };
            }
            catch { return null; }
        }
    }
}