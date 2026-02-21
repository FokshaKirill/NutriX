using System.Text.Json;
using Domain.Entities;
using Services.Interfaces;

namespace Services.Services
{
    /// <summary>
    /// Сервис для получения информации о пищевой ценности продуктов
    /// Использует открытые API или внутреннюю базу данных
    /// </summary>
    public class NutritionApiService : INutritionApiService
    {
        private readonly HttpClient _httpClient;
        
        // Встроенная база данных для популярных продуктов
        private static readonly Dictionary<string, ProductNutrition> NutritionDatabase = new()
        {
            // Мясо и птица
            { "куриная грудка", new ProductNutrition(113, 23.6m, 1.9m, 0.4m) },
            { "говядина", new ProductNutrition(187, 18.9m, 12.4m, 0) },
            { "свинина", new ProductNutrition(242, 16.0m, 21.6m, 0) },
            { "индейка", new ProductNutrition(157, 21.6m, 12.0m, 0) },
            { "фарш говяжий", new ProductNutrition(254, 17.2m, 20.0m, 0) },
            
            // Рыба и морепродукты
            { "лосось", new ProductNutrition(142, 19.8m, 6.3m, 0) },
            { "тунец", new ProductNutrition(96, 23.0m, 0.6m, 0) },
            { "креветки", new ProductNutrition(95, 20.0m, 1.8m, 0) },
            
            // Молочные продукты
            { "молоко", new ProductNutrition(64, 3.2m, 3.6m, 4.8m) },
            { "творог", new ProductNutrition(169, 16.7m, 9.0m, 2.0m) },
            { "сыр", new ProductNutrition(363, 26.0m, 26.5m, 0.3m) },
            { "йогурт", new ProductNutrition(68, 5.0m, 3.2m, 3.5m) },
            { "сметана", new ProductNutrition(193, 2.8m, 20.0m, 3.2m) },
            { "кефир", new ProductNutrition(56, 2.8m, 3.2m, 4.0m) },
            
            // Яйца
            { "яйцо", new ProductNutrition(157, 12.7m, 11.5m, 0.7m) },
            
            // Крупы
            { "рис", new ProductNutrition(344, 7.0m, 0.6m, 77.3m) },
            { "гречка", new ProductNutrition(313, 12.6m, 3.3m, 62.1m) },
            { "овсянка", new ProductNutrition(342, 12.3m, 6.1m, 59.5m) },
            { "макароны", new ProductNutrition(338, 10.4m, 1.1m, 69.7m) },
            
            // Мука и выпечка
            { "мука", new ProductNutrition(364, 9.2m, 1.2m, 74.9m) },
            { "хлеб", new ProductNutrition(266, 8.1m, 1.0m, 50.0m) },
            
            // Овощи
            { "картофель", new ProductNutrition(77, 2.0m, 0.4m, 16.1m) },
            { "морковь", new ProductNutrition(35, 1.3m, 0.1m, 6.9m) },
            { "лук", new ProductNutrition(41, 1.4m, 0.0m, 8.2m) },
            { "помидор", new ProductNutrition(20, 1.1m, 0.2m, 3.7m) },
            { "огурец", new ProductNutrition(15, 0.8m, 0.1m, 2.8m) },
            { "капуста", new ProductNutrition(27, 1.8m, 0.1m, 4.7m) },
            { "перец", new ProductNutrition(27, 1.3m, 0.0m, 5.3m) },
            { "баклажан", new ProductNutrition(24, 1.2m, 0.1m, 4.5m) },
            { "кабачок", new ProductNutrition(24, 0.6m, 0.3m, 4.6m) },
            
            // Фрукты
            { "яблоко", new ProductNutrition(47, 0.4m, 0.4m, 9.8m) },
            { "банан", new ProductNutrition(96, 1.5m, 0.5m, 21.0m) },
            { "апельсин", new ProductNutrition(43, 0.9m, 0.2m, 8.1m) },
            
            // Масла и жиры
            { "масло растительное", new ProductNutrition(899, 0, 99.9m, 0) },
            { "масло сливочное", new ProductNutrition(748, 0.5m, 82.5m, 0.8m) },
            
            // Сахар и сладости
            { "сахар", new ProductNutrition(398, 0, 0, 99.7m) },
            { "мед", new ProductNutrition(329, 0.8m, 0, 80.3m) },
            
            // Приправы
            { "соль", new ProductNutrition(0, 0, 0, 0) },
            { "перец черный", new ProductNutrition(251, 10.4m, 3.3m, 38.6m) },
        };

        public NutritionApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ProductNutrition?> GetNutritionInfoAsync(string productName)
        {
            // Сначала ищем во встроенной базе
            var normalized = NormalizeProductName(productName);
            
            foreach (var (key, nutrition) in NutritionDatabase)
            {
                if (normalized.Contains(key) || key.Contains(normalized))
                {
                    return nutrition;
                }
            }

            // Если не нашли, можно попробовать использовать API
            // Например, USDA FoodData Central API (требует регистрацию)
            // return await GetFromUsdaApiAsync(productName);

            return null;
        }

        private string NormalizeProductName(string name)
        {
            name = name.ToLower().Trim();
            
            // Убираем прилагательные
            var wordsToRemove = new[] 
            { 
                "свежий", "свежая", "свежее", 
                "крупный", "крупная", "средний", "средняя", 
                "мелкий", "мелкая", "белый", "красный",
                "зеленый", "куриное", "куриная", "говяжий",
                "свиной", "молотый", "тертый"
            };
            
            foreach (var word in wordsToRemove)
            {
                name = name.Replace(word, "").Trim();
            }
            
            return name;
        }

        // Опционально: получение данных из USDA API
        private async Task<ProductNutrition?> GetFromUsdaApiAsync(string productName)
        {
            // Требуется API ключ: https://fdc.nal.usda.gov/api-key-signup.html
            var apiKey = "YOUR_API_KEY_HERE";
            var url = $"https://api.nal.usda.gov/fdc/v1/foods/search?api_key={apiKey}&query={Uri.EscapeDataString(productName)}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<UsdaSearchResponse>(json);

                if (data?.Foods == null || data.Foods.Length == 0) return null;

                var food = data.Foods[0];
                var nutrients = food.FoodNutrients;

                // Извлекаем нужные нутриенты
                var calories = GetNutrientValue(nutrients, "Energy");
                var protein = GetNutrientValue(nutrients, "Protein");
                var fat = GetNutrientValue(nutrients, "Total lipid (fat)");
                var carbs = GetNutrientValue(nutrients, "Carbohydrate, by difference");

                return new ProductNutrition(
                    calories ?? 0,
                    protein ?? 0,
                    fat ?? 0,
                    carbs ?? 0
                );
            }
            catch
            {
                return null;
            }
        }

        private decimal? GetNutrientValue(UsdaFoodNutrient[] nutrients, string nutrientName)
        {
            var nutrient = nutrients.FirstOrDefault(n => 
                n.NutrientName?.Contains(nutrientName, StringComparison.OrdinalIgnoreCase) == true);
            
            return (decimal?)nutrient?.Value;
        }

        public async Task<Product> EnrichProductWithNutritionAsync(Product product)
        {
            var nutrition = await GetNutritionInfoAsync(product.Name);
            
            if (nutrition != null)
            {
                product.CaloriesPer100 = nutrition.Calories;
                product.ProteinPer100 = nutrition.Protein;
                product.FatPer100 = nutrition.Fat;
                product.CarbsPer100 = nutrition.Carbs;
            }

            return product;
        }
    }

    // DTO классы для парсинга
    public record ProductNutrition(
        decimal Calories,
        decimal Protein,
        decimal Fat,
        decimal Carbs
    );

    // USDA API Response classes
    public class UsdaSearchResponse
    {
        public UsdaFood[]? Foods { get; set; }
    }

    public class UsdaFood
    {
        public string? Description { get; set; }
        public UsdaFoodNutrient[]? FoodNutrients { get; set; }
    }

    public class UsdaFoodNutrient
    {
        public string? NutrientName { get; set; }
        public double? Value { get; set; }
        public string? UnitName { get; set; }
    }
}