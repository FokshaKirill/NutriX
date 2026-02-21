using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Domain.Entities;
using Services.Interfaces;

namespace Services.Services
{
    public class RecipeParserService : IRecipeParserService
    {
        private readonly HttpClient _httpClient;
        private readonly IProductService _productService;
        private readonly INutritionApiService _nutritionService;

        public RecipeParserService(
            HttpClient httpClient, 
            IProductService productService,
            INutritionApiService nutritionService)
        {
            _httpClient = httpClient;
            _productService = productService;
            _nutritionService = nutritionService;
        }

        public async Task<ParsedRecipeDto> ParseRecipeFromUrlAsync(string url)
        {
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var result = new ParsedRecipeDto();

            // Парсинг названия
            result.Name = ParseName(doc);

            // Парсинг описания
            result.Description = ParseDescription(doc);

            // Парсинг порций
            result.Servings = ParseServings(doc);

            // Парсинг изображения
            result.ImageUrl = ParseMainImage(doc);

            // Парсинг ингредиентов (новая структура)
            result.Ingredients = ParseIngredientsNew(doc);

            // Парсинг шагов (новая структура)
            result.Steps = ParseStepsNew(doc);

            // Парсинг общих БЖУ и калорий
            ParseNutrition(doc, result);

            return result;
        }

        private string ParseName(HtmlDocument doc)
        {
            // Название в h1
            var nameNode = doc.DocumentNode.SelectSingleNode("//h1[@itemprop='name']") 
                          ?? doc.DocumentNode.SelectSingleNode("//h1");

            return nameNode?.InnerText.Trim() ?? "Неизвестный рецепт";
        }

        private string? ParseDescription(HtmlDocument doc)
        {
            // Описание может быть в meta description или в начале страницы
            var descNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            var description = descNode?.GetAttributeValue("content", null);

            if (string.IsNullOrEmpty(description))
            {
                descNode = doc.DocumentNode.SelectSingleNode("//div[@itemprop='description']");
                description = descNode?.InnerText.Trim();
            }

            return description;
        }

        private int ParseServings(HtmlDocument doc)
        {
            // Ищем информацию о порциях в recipeYield
            var servingsNode = doc.DocumentNode.SelectSingleNode("//*[@itemprop='recipeYield']");

            if (servingsNode != null)
            {
                var text = servingsNode.InnerText;
                var match = Regex.Match(text, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int servings))
                {
                    return servings;
                }
            }

            return 4; // По умолчанию
        }

        private string? ParseMainImage(HtmlDocument doc)
        {
            // Главное изображение рецепта
            var imgNode = doc.DocumentNode.SelectSingleNode("//img[@itemprop='image']")
                         ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'recipe-photo')]//img")
                         ?? doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");

            var imgUrl = imgNode?.GetAttributeValue("src", null) 
                        ?? imgNode?.GetAttributeValue("content", null);

            if (!string.IsNullOrEmpty(imgUrl) && !imgUrl.StartsWith("http"))
            {
                // Убираем //static и делаем полный URL
                if (imgUrl.StartsWith("//"))
                {
                    imgUrl = "https:" + imgUrl;
                }
                else
                {
                    imgUrl = $"https://1000.menu{imgUrl}";
                }
            }

            return imgUrl;
        }

        private List<ParsedIngredientDto> ParseIngredientsNew(HtmlDocument doc)
        {
            var ingredients = new List<ParsedIngredientDto>();

            // Новая структура: div.ingredient с meta itemprop="recipeIngredient"
            var ingredientNodes = doc.DocumentNode.SelectNodes("//div[@class='ingredient list-item']");

            if (ingredientNodes == null) return ingredients;

            foreach (var node in ingredientNodes)
            {
                try
                {
                    // Название продукта из <a class="name">
                    var nameNode = node.SelectSingleNode(".//a[@class='name']");
                    var name = nameNode?.InnerText.Trim();

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    // Количество из <span class="squant value">
                    var quantityNode = node.SelectSingleNode(".//span[@class='squant value']");
                    var quantityStr = quantityNode?.InnerText.Trim();

                    // Единица измерения из <select class="recalc_s_num">
                    var unitNode = node.SelectSingleNode(".//select[@class='recalc_s_num']/option[@selected]");
                    var unit = unitNode?.InnerText.Trim();

                    // Проверка на "по вкусу"
                    var tasteNode = node.SelectSingleNode(".//span[@class='type']");
                    var isByTaste = tasteNode?.InnerText.Trim() == "по вкусу";

                    // Комментарий из span.ingredient-info
                    var commentNode = node.SelectSingleNode(".//span[@class='ingredient-info mr-1']");
                    var comment = commentNode?.InnerText.Trim().TrimStart('(').TrimEnd(')');

                    decimal amount = 0;
                    if (!isByTaste && !string.IsNullOrEmpty(quantityStr))
                    {
                        decimal.TryParse(quantityStr.Replace(',', '.'), out amount);
                    }

                    // Нормализация единиц
                    var normalizedUnit = NormalizeUnit(unit ?? "г");
                    
                    // Конвертация в граммы
                    if (amount > 0)
                    {
                        amount = ConvertToGrams(amount, normalizedUnit);
                    }

                    ingredients.Add(new ParsedIngredientDto
                    {
                        Name = name,
                        Amount = amount,
                        Unit = "г",
                        Comment = isByTaste ? "по вкусу" : comment,
                        OriginalText = $"{name} - {quantityStr} {unit}"
                    });
                }
                catch (Exception ex)
                {
                    // Логируем и пропускаем проблемный ингредиент
                    Console.WriteLine($"Error parsing ingredient: {ex.Message}");
                    continue;
                }
            }

            return ingredients;
        }

        private string NormalizeUnit(string unit)
        {
            unit = unit.ToLower().Trim().Replace(".", "");
            
            return unit switch
            {
                "гр" or "г" => "г",
                "кг" => "кг",
                "л" => "л",
                "мл" => "мл",
                "шт" or "штук" or "штука" => "шт",
                "столл" or "стол л" or "стол.л" => "ст.л.",
                "чайнл" or "чайн л" or "чайн.л" => "ч.л.",
                "десертл" or "десерт л" or "десерт.л" => "д.л.",
                "стак" or "стакан" => "стакан",
                _ => "г"
            };
        }

        private decimal ConvertToGrams(decimal amount, string unit)
        {
            return unit switch
            {
                "кг" => amount * 1000,
                "л" => amount * 1000,
                "мл" => amount,
                "ст.л." => amount * 15,
                "ч.л." => amount * 5,
                "д.л." => amount * 10,
                "стакан" => amount * 200,
                "шт" => amount * 50, // Примерная масса
                _ => amount
            };
        }

        private List<ParsedStepDto> ParseStepsNew(HtmlDocument doc)
        {
            var steps = new List<ParsedStepDto>();

            // Новая структура: ol.instructions > li
            var stepNodes = doc.DocumentNode.SelectNodes("//ol[@class='instructions']//li[not(contains(@class, 'as-ad-step'))]");

            if (stepNodes == null) return steps;

            int order = 1;
            foreach (var node in stepNodes)
            {
                try
                {
                    // Заголовок шага из h3.r-section-header
                    var headerNode = node.SelectSingleNode(".//h3[@class='r-section-header']");
                    var header = headerNode?.InnerText.Trim();

                    // Описание из p.instruction
                    var descNode = node.SelectSingleNode(".//p[@class='instruction']");
                    var description = descNode?.InnerText.Trim();

                    if (string.IsNullOrWhiteSpace(description)) continue;

                    // Изображение шага из a.step-img
                    var imgNode = node.SelectSingleNode(".//a[@class='step-img foto_gallery']");
                    var imgUrl = imgNode?.GetAttributeValue("href", null);

                    if (!string.IsNullOrEmpty(imgUrl) && imgUrl.StartsWith("//"))
                    {
                        imgUrl = "https:" + imgUrl;
                    }

                    // Извлекаем таймер из текста
                    var timerSeconds = ExtractTimerFromText(description);

                    steps.Add(new ParsedStepDto
                    {
                        Order = order++,
                        Description = description,
                        TimerSeconds = timerSeconds,
                        ImageUrl = imgUrl
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing step: {ex.Message}");
                    continue;
                }
            }

            return steps;
        }

        private int? ExtractTimerFromText(string text)
        {
            // Ищем время в тексте
            var patterns = new[]
            {
                (@"(\d+)\s*(?:минут[ыа]?|мин\.?)", 60),
                (@"(\d+)\s*(?:час[аов]?)", 3600),
                (@"(\d+)\s*(?:секунд[ыа]?|сек\.?)", 1),
                (@"(\d+)-(\d+)\s*(?:минут[ыа]?|мин\.?)", 60) // Диапазон минут
            };

            foreach (var (pattern, multiplier) in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (match.Groups.Count > 2 && int.TryParse(match.Groups[2].Value, out int max))
                    {
                        // Для диапазона берем среднее
                        if (int.TryParse(match.Groups[1].Value, out int min))
                        {
                            return ((min + max) / 2) * multiplier;
                        }
                    }
                    else if (int.TryParse(match.Groups[1].Value, out int time))
                    {
                        return time * multiplier;
                    }
                }
            }

            return null;
        }

        private void ParseNutrition(HtmlDocument doc, ParsedRecipeDto result)
        {
            // Ищем информацию о пищевой ценности
            var nutritionNodes = doc.DocumentNode.SelectNodes("//*[@itemprop='nutrition']")
                                ?? doc.DocumentNode.SelectNodes("//*[contains(@class, 'nutrition-info')]")
                                ?? doc.DocumentNode.SelectNodes("//div[@class='nutr-data']");

            if (nutritionNodes == null) return;

            foreach (var node in nutritionNodes)
            {
                var text = node.InnerText;

                // Калории
                var caloriesMatch = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*(?:ккал|кал)", RegexOptions.IgnoreCase);
                if (caloriesMatch.Success && decimal.TryParse(caloriesMatch.Groups[1].Value.Replace(',', '.'), out decimal calories))
                {
                    result.TotalCalories = calories;
                }

                // Белки
                var proteinMatch = Regex.Match(text, @"Белк[иоа]?[:\s]+(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (proteinMatch.Success && decimal.TryParse(proteinMatch.Groups[1].Value.Replace(',', '.'), out decimal protein))
                {
                    result.TotalProtein = protein;
                }

                // Жиры
                var fatMatch = Regex.Match(text, @"Жир[ыоа]?[:\s]+(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (fatMatch.Success && decimal.TryParse(fatMatch.Groups[1].Value.Replace(',', '.'), out decimal fat))
                {
                    result.TotalFat = fat;
                }

                // Углеводы
                var carbsMatch = Regex.Match(text, @"Углевод[ыоа]?[:\s]+(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (carbsMatch.Success && decimal.TryParse(carbsMatch.Groups[1].Value.Replace(',', '.'), out decimal carbs))
                {
                    result.TotalCarbs = carbs;
                }
            }
        }

        public async Task<Recipe> ConvertToRecipeAsync(ParsedRecipeDto parsedRecipe)
        {
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Name = parsedRecipe.Name,
                Description = parsedRecipe.Description,
                DefaultServings = parsedRecipe.Servings,
                ImageUrl = parsedRecipe.ImageUrl
            };

            // Конвертируем ингредиенты
            var ingredients = new List<RecipeIngredient>();
            decimal totalCost = 0;

            foreach (var parsedIng in parsedRecipe.Ingredients)
            {
                // Ищем или создаем продукт
                var product = await FindOrCreateProductAsync(parsedIng.Name);

                var ingredient = new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    ProductId = product.Id,
                    Product = product,
                    Amount = parsedIng.Amount,
                    Unit = parsedIng.Unit,
                    Comment = parsedIng.Comment
                };

                ingredients.Add(ingredient);

                // Рассчитываем стоимость
                if (product.PricePerUnit != 0 && parsedIng.Amount > 0)
                {
                    totalCost += (parsedIng.Amount / 100) * product.PricePerUnit;
                }
            }

            recipe.Ingredients = ingredients;
            recipe.TotalCost = totalCost;

            // Конвертируем шаги
            recipe.Steps = parsedRecipe.Steps.Select(s => new RecipeStep
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Order = s.Order,
                Description = s.Description,
                TimerSeconds = s.TimerSeconds,
                ImageUrl = s.ImageUrl
            }).ToList();

            return recipe;
        }

        private async Task<Product> FindOrCreateProductAsync(string name)
        {
            // Ищем продукт в базе
            var products = await _productService.GetAllProductsAsync();
            
            // Нормализуем название для поиска
            var normalizedName = NormalizeProductName(name);
            
            // Точное совпадение
            var product = products.FirstOrDefault(p => 
                NormalizeProductName(p.Name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

            if (product != null) return product;

            // Частичное совпадение
            product = products.FirstOrDefault(p => 
            {
                var pName = NormalizeProductName(p.Name);
                return pName.Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                       normalizedName.Contains(pName, StringComparison.OrdinalIgnoreCase);
            });

            if (product != null) return product;

            // Создаем новый продукт
            var newProduct = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Unit = "г",
                PricePerUnit = 0,
            };

            // Получаем БЖУ из API
            newProduct = await _nutritionService.EnrichProductWithNutritionAsync(newProduct);

            await _productService.CreateAsync(newProduct);
            return newProduct;
        }

        private string NormalizeProductName(string name)
        {
            name = name.ToLower().Trim();
            
            // Убираем прилагательные и лишние слова
            var wordsToRemove = new[] 
            { 
                "свежий", "свежая", "свежее", "свежие",
                "крупный", "крупная", "крупное", "крупные",
                "средний", "средняя", "среднее", "средние",
                "мелкий", "мелкая", "мелкое", "мелкие",
                "белый", "белая", "белое", "белые",
                "красный", "красная", "красное", "красные",
                "зеленый", "зеленая", "зеленое", "зеленые",
                "куриное", "куриная", "куриные", "куриный",
                "говяжий", "говяжья", "говяжье", "говяжьи",
                "свиной", "свиная", "свиное", "свиные",
                "молотый", "молотая", "молотое", "молотые",
                "тертый", "тертая", "тертое", "тертые",
                "пшеничная", "пшеничный", "пшеничное"
            };
            
            foreach (var word in wordsToRemove)
            {
                name = Regex.Replace(name, $@"\b{word}\b", "", RegexOptions.IgnoreCase).Trim();
            }

            // Убираем множественные пробелы
            name = Regex.Replace(name, @"\s+", " ");
            
            return name;
        }
    }

    // DTO для парсинга
    public class ParsedRecipeDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Servings { get; set; } = 4;
        public string? ImageUrl { get; set; }
        public List<ParsedIngredientDto> Ingredients { get; set; } = new();
        public List<ParsedStepDto> Steps { get; set; } = new();
        public decimal? TotalCalories { get; set; }
        public decimal? TotalProtein { get; set; }
        public decimal? TotalFat { get; set; }
        public decimal? TotalCarbs { get; set; }
    }

    public class ParsedIngredientDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Unit { get; set; } = "г";
        public string? Comment { get; set; }
        public string OriginalText { get; set; } = string.Empty;
    }

    public class ParsedStepDto
    {
        public int Order { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? TimerSeconds { get; set; }
        public string? ImageUrl { get; set; }
    }
}
