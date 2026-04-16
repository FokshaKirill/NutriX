using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Domain.Entities; // предполагаю, что Product отсюда

namespace Services.Helpers
{
    /// <summary>
    /// Парсер продуктов Пятёрочки через публичное API[](https://5ka.ru/api/v2/)
    /// Поддерживает получение магазина по координатам, категории, списки товаров, детали с БЖУ
    /// </summary>
    public class PyaterochkaParser
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://5ka.ru/api/v2/";
        private string _sapCodeStoreId;

        public PyaterochkaParser()
        {
            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9");
            _httpClient.Timeout = TimeSpan.FromSeconds(40);
        }

        /// <summary>
        /// Получает sap_code магазина по координатам (автоматически выбирает ближайший)
        /// Для Тирасполя: lat ≈ 46.84, lon ≈ 29.62
        /// </summary>
        public async Task<bool> InitializeStoreAsync(double latitude = 46.84, double longitude = 29.62, int radiusMeters = 100000)
        {
            try
            {
                var query = $"?lat={latitude.ToString(CultureInfo.InvariantCulture)}" +
                            $"&lon={longitude.ToString(CultureInfo.InvariantCulture)}" +
                            $"&radius={radiusMeters}";

                var response = await _httpClient.GetAsync($"{BaseUrl}stores/{query}");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ошибка получения магазинов: {response.StatusCode}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    var firstStore = results.EnumerateArray().FirstOrDefault();
                    if (firstStore.TryGetProperty("sapCode", out var sapCodeElem))
                    {
                        _sapCodeStoreId = sapCodeElem.GetString();
                        Console.WriteLine($"Успешно выбран магазин: sap_code = {_sapCodeStoreId}");
                        return true;
                    }
                }

                Console.WriteLine("Магазины не найдены по координатам");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Исключение при инициализации магазина: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает дерево категорий
        /// </summary>
        private async Task<List<CategoryNode>> GetCategoryTreeAsync()
        {
            if (string.IsNullOrEmpty(_sapCodeStoreId))
                throw new InvalidOperationException("Сначала вызовите InitializeStoreAsync или задайте _sapCodeStoreId вручную");

            var query = $"?sap_code_store_id={_sapCodeStoreId}";
            var response = await _httpClient.GetAsync($"{BaseUrl}catalog/tree{query}");

            if (!response.IsSuccessStatusCode) return new List<CategoryNode>();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            // Предполагаем структуру: массив объектов с id, name, children[...]
            return ParseCategoryNodes(doc.RootElement);
        }

        private List<CategoryNode> ParseCategoryNodes(JsonElement element)
        {
            var nodes = new List<CategoryNode>();

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idElem) &&
                        item.TryGetProperty("name", out var nameElem))
                    {
                        var node = new CategoryNode
                        {
                            Id = idElem.GetString(),
                            Name = nameElem.GetString()
                        };

                        if (item.TryGetProperty("children", out var childrenElem) &&
                            childrenElem.ValueKind == JsonValueKind.Array)
                        {
                            node.Children = ParseCategoryNodes(childrenElem);
                        }

                        nodes.Add(node);
                    }
                }
            }

            return nodes;
        }

        /// <summary>
        /// Парсит все доступные продукты (по всем категориям верхнего уровня)
        /// </summary>
        public async Task<List<Product>> ParseAllProductsAsync(int maxPagesPerCategory = 5)
        {
            if (string.IsNullOrEmpty(_sapCodeStoreId) && !await InitializeStoreAsync())
            {
                throw new Exception("Не удалось инициализировать магазин. Укажите sap_code вручную.");
            }

            var allProducts = new List<Product>();
            var categories = await GetCategoryTreeAsync();

            foreach (var topCategory in categories.Where(c => c.Children?.Any() == true || true)) // все, включая листья
            {
                Console.WriteLine($"Парсинг категории: {topCategory.Name} ({topCategory.Id})");

                var products = await GetProductsFromCategoryAsync(topCategory.Id, maxPagesPerCategory);
                allProducts.AddRange(products);

                await Task.Delay(1500); // анти-бан
            }

            return allProducts;
        }

        /// <summary>
        /// Получает продукты из одной категории (с пагинацией)
        /// </summary>
        public async Task<List<Product>> GetProductsFromCategoryAsync(string categoryId, int maxPages = 10)
        {
            var products = new List<Product>();

            for (int page = 1; page <= maxPages; page++)
            {
                var query = $"?category_id={categoryId}" +
                            $"&sap_code_store_id={_sapCodeStoreId}" +
                            $"&page={page}" +
                            "&records_per_page=48"; // стандартный размер страницы

                var response = await _httpClient.GetAsync($"{BaseUrl}catalog/products_list{query}");
                if (!response.IsSuccessStatusCode) break;

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("products", out var prodsElem) ||
                    prodsElem.ValueKind != JsonValueKind.Array)
                    break;

                foreach (var prodElem in prodsElem.EnumerateArray())
                {
                    var product = ParseProductFromJson(prodElem);
                    if (product != null)
                    {
                        // Можно сразу обогатить БЖУ
                        await EnrichProductDetailsAsync(product, prodElem.GetProperty("plu").GetString());
                        products.Add(product);
                    }
                }

                if (prodsElem.GetArrayLength() < 48) break; // последняя страница

                await Task.Delay(800);
            }

            return products;
        }

        private Product ParseProductFromJson(JsonElement elem)
        {
            try
            {
                string name = elem.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) return null;

                decimal price = 0;
                if (elem.TryGetProperty("regular_price", out var rp) && rp.ValueKind == JsonValueKind.Number)
                    price = rp.GetDecimal();
                else if (elem.TryGetProperty("current_prices", out var cp) && cp.ValueKind == JsonValueKind.Object)
                {
                    // иногда current_prices → price → value
                    if (cp.TryGetProperty("price", out var inner) && inner.TryGetProperty("value", out var val))
                        price = val.GetDecimal();
                }

                string unit = elem.TryGetProperty("measure", out var m) ? m.GetString() : "шт";
                string image = elem.TryGetProperty("image", out var img) ? img.GetString() : null;

                if (!string.IsNullOrEmpty(image) && !image.StartsWith("http"))
                    image = "https:" + image;

                return new Product
                {
                    Id = Guid.NewGuid(),
                    Name = CleanProductName(name),
                    PricePerUnit = price,
                    Unit = NormalizeUnit(unit),
                    ImageUrl = image,
                    // БЖУ заполнится позже
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Обогащает продукт деталями (БЖУ, калории и т.д.)
        /// </summary>
        private async Task EnrichProductDetailsAsync(Product product, string pluId)
        {
            if (string.IsNullOrEmpty(pluId)) return;

            var query = $"?plu_id={pluId}&sap_code_store_id={_sapCodeStoreId}";
            var response = await _httpClient.GetAsync($"{BaseUrl}catalog/product/info{query}");

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("nutrition_facts", out var nf) ||
                doc.RootElement.TryGetProperty("characteristics", out nf)) // структура может варьироваться
            {
                // Пример: ищем поля типа "energy_value", "proteins", "fats", "carbohydrates"
                if (nf.TryGetProperty("energy_value", out var cal) && cal.ValueKind == JsonValueKind.Number)
                    product.CaloriesPer100 = cal.GetDecimal();

                if (nf.TryGetProperty("proteins", out var prot) && prot.ValueKind == JsonValueKind.Number)
                    product.ProteinPer100 = prot.GetDecimal();

                if (nf.TryGetProperty("fats", out var fat) && fat.ValueKind == JsonValueKind.Number)
                    product.FatPer100 = fat.GetDecimal();

                if (nf.TryGetProperty("carbohydrates", out var carb) && carb.ValueKind == JsonValueKind.Number)
                    product.CarbsPer100 = carb.GetDecimal();
            }
        }

        // ──────────────────────────────────────────────
        // Твои вспомогательные методы (оставил почти без изменений)
        // ──────────────────────────────────────────────

        private string CleanProductName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            name = Regex.Replace(name, @"\s+", " ");
            return System.Net.WebUtility.HtmlDecode(name).Trim();
        }

        private string NormalizeUnit(string unit)
        {
            unit = unit?.ToLowerInvariant().Trim() ?? "шт";
            if (unit.Contains("кг") || unit.Contains("кило")) return "кг";
            if (unit.Contains("г") && !unit.Contains("кг")) return "г";
            if (unit.Contains("л") && !unit.Contains("мл")) return "л";
            if (unit.Contains("мл")) return "мл";
            return "шт";
        }

        // Если нужно парсить по старому URL (извлечь plu из https://5ka.ru/product/123456/)
        public async Task<Product> ParseProductByUrlAsync(string url)
        {
            var match = Regex.Match(url, @"/product/(\d+)/?");
            if (!match.Success) return null;

            string plu = match.Groups[1].Value;

            // Получаем детали напрямую
            var product = new Product { Id = Guid.NewGuid() };
            await EnrichProductDetailsAsync(product, plu);

            // Можно дополнить name, price и т.д. из /product/info
            return product;
        }

        // Вспомогательный класс для категорий
        private class CategoryNode
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<CategoryNode> Children { get; set; }
        }
    }
}