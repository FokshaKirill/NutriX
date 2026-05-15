using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Entities;

namespace Services.Helpers
{
    public class PriceRuParser
    {
        private readonly HttpClient _http;
        private const string ServiceBase = "http://localhost:8765"; 

        public PriceRuParser()
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

        public async Task<string> ProxyGetProductsAsync(string slug, int page = 1)
        {
            var resp = await _http.GetAsync(
                $"{ServiceBase}/products?slug={Uri.EscapeDataString(slug)}&page={page}");
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Сервис вернул {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
            return await resp.Content.ReadAsStringAsync();
        }

        public async Task<string> SearchAsync(string query, int page = 1)
        {
            var resp = await _http.GetAsync(
                $"{ServiceBase}/search?q={Uri.EscapeDataString(query)}&page={page}");
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Сервис вернул {(int)resp.StatusCode}");
            return await resp.Content.ReadAsStringAsync();
        }
    }
}