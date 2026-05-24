using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Domain.Entities;
using Services.DTO;
using Services.Helpers;
using Services.Interfaces;
using Services.Services;

namespace Services.Services
{
    public class RecipeParserService : IRecipeParserService
    {
        private readonly HttpClient _httpClient;
        private readonly IProductService _productService;

        public RecipeParserService(
            HttpClient httpClient, 
            IProductService productService)
        {
            _httpClient = httpClient;
            _productService = productService;
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
            // Приоритет 1: Прямой input с 1000menu
            var yieldInput = doc.DocumentNode.SelectSingleNode("//input[@id='yield_num_input']");
            if (yieldInput != null)
            {
                var val = yieldInput.GetAttributeValue("value", "");
                if (int.TryParse(val, out int servings) && servings > 0)
                    return servings;
            }

            // Приоритет 2: Microdata
            var recipeYield = doc.DocumentNode.SelectSingleNode("//*[@itemprop='recipeYield']");
            if (recipeYield != null)
            {
                var match = Regex.Match(recipeYield.InnerText, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int s))
                    return s;
            }

            // Приоритет 3: Текст на странице
            var textNodes = doc.DocumentNode.SelectNodes("//*[contains(text(),'порц') or contains(text(),'Порц')]");
            if (textNodes != null)
            {
                foreach (var node in textNodes)
                {
                    var match = Regex.Match(node.InnerText, @"(\d+)\s*порц", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int s))
                        return s;
                }
            }

            return 4; // fallback
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

    // Берём все узлы внутри формы списка — и group-name, и ingredient
    var container = doc.DocumentNode.SelectSingleNode(
        "//form[contains(@class,'dotted-list')]");
    
    if (container == null) return ingredients;

    var nodes = container.ChildNodes
        .Where(n => n.NodeType == HtmlNodeType.Element)
        .ToList();

    string? currentGroup = null;

    foreach (var node in nodes)
    {
        // Встретили заголовок группы
        if (node.HasClass("group-name"))
        {
            currentGroup = node.InnerText
                .Trim()
                .TrimEnd(':', ' ')  // убираем "Для блинов:: " → "Для блинов"
                .Trim();
            continue;
        }

        // Встретили ингредиент
        if (!node.HasClass("ingredient") || !node.HasClass("list-item"))
            continue;

        try
        {
            var nameNode = node.SelectSingleNode(
                ".//a[contains(@class,'name')]");
            var name = nameNode?.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var quantityNode = node.SelectSingleNode(
                ".//span[contains(@class,'squant') and contains(@class,'value')]");
            var quantityStr = quantityNode?.InnerText.Trim();

            var unitNode = node.SelectSingleNode(
                ".//select[contains(@class,'recalc_s_num')]/option[@selected]");
            var unit = unitNode?.InnerText.Trim();

            var tasteNode = node.SelectSingleNode(".//span[@class='type']");
            var isByTaste = tasteNode?.InnerText.Trim() == "по вкусу";

            var commentNode = node.SelectSingleNode(
                ".//span[contains(@class,'ingredient-info')]");
            var rawComment = commentNode?.InnerText.Trim()
                .TrimStart('(').TrimEnd(')');

            decimal amount = 0;
            if (!isByTaste && !string.IsNullOrEmpty(quantityStr))
                decimal.TryParse(quantityStr.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out amount);

            var normalizedUnit = NormalizeUnit(unit ?? "г");
            if (amount > 0)
                amount = ConvertToGrams(amount, normalizedUnit);

            // Собираем comment: группа + скобочный комментарий + по вкусу
            var commentParts = new List<string>();
            if (currentGroup != null)      commentParts.Add($"[{currentGroup}]");
            if (isByTaste)                 commentParts.Add("по вкусу");
            else if (!string.IsNullOrWhiteSpace(rawComment)) commentParts.Add(rawComment);

            ingredients.Add(new ParsedIngredientDto
            {
                Name         = name,
                Amount       = amount,
                Unit         = "г",
                Comment      = commentParts.Count > 0
                                   ? string.Join(" ", commentParts)
                                   : null,
                OriginalText = $"{name} - {quantityStr} {unit}"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing ingredient: {ex.Message}");
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

    var stepNodes = doc.DocumentNode.SelectNodes(
        "//ol[contains(@class,'instructions')]//li[not(contains(@class,'as-ad-step')) and not(contains(@class,'as-video-step'))]");

    if (stepNodes == null) return steps;

    int order = 1;
    foreach (var node in stepNodes)
    {
        try
        {
            // Берём div.instruction и убираем infoblock-параграфы (советы, секреты и т.д.)
            var instrDiv = node.SelectSingleNode(".//div[@class='instruction']");
            if (instrDiv == null) continue;

            // Клонируем чтобы не портить оригинал
            var instrClone = instrDiv.CloneNode(true);

            // Удаляем блоки infoblock (советы, секреты шеф-повара и т.д.)
            var infoblocks = instrClone.SelectNodes(".//*[contains(@class,'infoblock')]");
            if (infoblocks != null)
                foreach (var ib in infoblocks.ToList())
                    ib.Remove();

            // Берём первый <p> — это основное описание шага
            var firstP = instrClone.SelectSingleNode(".//p");
            var description = firstP != null
                ? HtmlEntity.DeEntitize(firstP.InnerText).Trim()
                : HtmlEntity.DeEntitize(instrClone.InnerText).Trim();

            // Убираем лишние пробелы и переносы
            description = System.Text.RegularExpressions.Regex
                .Replace(description, @"\s{2,}", " ").Trim();

            if (string.IsNullOrWhiteSpace(description)) continue;

            // Изображение шага из a.step-img
            var imgNode = node.SelectSingleNode(".//a[contains(@class,'step-img')]");
            var imgUrl  = imgNode?.GetAttributeValue("href", null);
            if (!string.IsNullOrEmpty(imgUrl) && imgUrl.StartsWith("//"))
                imgUrl = "https:" + imgUrl;

            steps.Add(new ParsedStepDto
            {
                Order        = order++,
                Description  = description,
                TimerSeconds = ExtractTimerFromText(description),
                ImageUrl     = imgUrl
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing step {order}: {ex.Message}");
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
            }

            recipe.Ingredients = ingredients;

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

        // ── Замена FindOrCreateProductAsync в RecipeParserService ───────────────

        private async Task<Product> FindOrCreateProductAsync(string rawName)
        {
            // 1. Пробуем получить каноническое имя через нормализатор
            var canonical = ProductNormalizer.Normalize(rawName);

            var products = await _productService.GetAllProductsAsync();

            // 2. Ищем по каноническому имени (точное)
            if (!string.IsNullOrEmpty(canonical))
            {
                var byCanonical = products.FirstOrDefault(p =>
                    p.Name.Equals(canonical, StringComparison.OrdinalIgnoreCase));
                if (byCanonical != null) return byCanonical;
            }

            // 3. Ищем по исходному названию (вдруг оно уже в базе как есть)
            var byRaw = products.FirstOrDefault(p =>
                p.Name.Equals(rawName, StringComparison.OrdinalIgnoreCase));
            if (byRaw != null) return byRaw;

            // 4. Частичное совпадение по нормализованному
            //    Используем уже существующий NormalizeProductName (убирает прилагательные)
            var normalized = NormalizeProductName(rawName);
            var byPartial = products.FirstOrDefault(p =>
            {
                var pn = NormalizeProductName(p.Name);
                return pn.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                       || normalized.Contains(pn, StringComparison.OrdinalIgnoreCase);
            });
            if (byPartial != null) return byPartial;

            // 5. Ничего не нашли — создаём заглушку.
            //    Имя сохраняем сырым, чтобы потом можно было вручную смапить.
            //    INutritionApiService убрали — нет смысла в онлайн-запросе,
            //    если база из 999 продуктов + нормализатор закрывают 95% случаев.
            var stub = new Product
            {
                Id           = Guid.NewGuid(),
                Name         = rawName.Trim(),
                Unit         = "г",
                PricePerUnit = 0,
                Source       = "parser:unknown",   // легко найти в админке
                CreatedAt    = DateTime.UtcNow,
            };

            await _productService.CreateAsync(stub);
            return stub;
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
}
